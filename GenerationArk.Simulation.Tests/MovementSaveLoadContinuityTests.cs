using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.Movement;
using GenerationArk.Simulation.Persistence;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Tests;

internal static class MovementSaveLoadContinuityTests
{
    private static readonly MapCellDefinitionId FloorDefinition = new(1);

    public static void MovementEntityRestorePreservesCanonicalComponentState()
    {
        IReadOnlyList<ComponentRegistration> registrations = Registrations();
        WorldState world = CreateWorld(registrations);
        DeterministicScheduler scheduler = CreateScheduler();
        var expected = new MovementAgentState(new MapCellId(2), new MapCellId(7), 11UL);
        EntityId entityId = CreateMovementEntity(world, scheduler, expected);

        byte[] beforeBytes = EntityStateSerializer.ToUtf8(world);
        WorldState restored = EntityStateSerializer.Restore(beforeBytes, registrations);
        byte[] afterBytes = EntityStateSerializer.ToUtf8(restored);
        var actual = (MovementAgentState)restored.Components.Get(
            entityId,
            MovementAgentState.ComponentTypeId);

        TestAssert.Equal(expected.CurrentCell, actual.CurrentCell);
        TestAssert.Equal(expected.DestinationCell, actual.DestinationCell);
        TestAssert.Equal(expected.RouteRevision, actual.RouteRevision);
        TestAssert.Equal(expected, actual);
        TestAssert.Equal(Convert.ToHexString(beforeBytes), Convert.ToHexString(afterBytes));
        TestAssert.Equal(
            StateChecksum.Compute(new SimTick(1), world),
            StateChecksum.Compute(new SimTick(1), restored));
    }

    public static void SaveLoadResumedMovementMatchesUninterruptedMovement()
    {
        IReadOnlyList<ComponentRegistration> registrations = Registrations();
        MapState map = CreateMap(6, 2);
        var initial = new MovementAgentState(
            Cell(0, 0, map),
            Cell(5, 1, map),
            0UL);
        const int midpointTick = 3;
        const int finalTick = 6;

        WorldState uninterruptedWorld = CreateWorld(registrations);
        DeterministicScheduler uninterruptedScheduler = CreateScheduler();
        EntityId uninterruptedEntity = CreateMovementEntity(
            uninterruptedWorld,
            uninterruptedScheduler,
            initial);
        AdvanceMovement(
            uninterruptedWorld,
            uninterruptedScheduler,
            uninterruptedEntity,
            map,
            firstTick: 2,
            finalTick);

        MovementAgentState uninterruptedState = GetMovement(uninterruptedWorld, uninterruptedEntity);
        byte[] uninterruptedBytes = EntityStateSerializer.ToUtf8(uninterruptedWorld);
        ulong uninterruptedChecksum = StateChecksum.Compute(
            new SimTick(finalTick),
            uninterruptedWorld,
            uninterruptedScheduler);

        WorldState resumedWorld = CreateWorld(registrations);
        DeterministicScheduler resumedScheduler = CreateScheduler();
        EntityId resumedEntity = CreateMovementEntity(resumedWorld, resumedScheduler, initial);
        AdvanceMovement(
            resumedWorld,
            resumedScheduler,
            resumedEntity,
            map,
            firstTick: 2,
            midpointTick);

        byte[] midpointBytes = EntityStateSerializer.ToUtf8(resumedWorld);
        resumedWorld = EntityStateSerializer.Restore(midpointBytes, registrations);
        resumedScheduler = CreateScheduler();
        AdvanceMovement(
            resumedWorld,
            resumedScheduler,
            resumedEntity,
            map,
            firstTick: midpointTick + 1,
            finalTick);

        MovementAgentState resumedState = GetMovement(resumedWorld, resumedEntity);
        byte[] resumedBytes = EntityStateSerializer.ToUtf8(resumedWorld);
        ulong resumedChecksum = StateChecksum.Compute(
            new SimTick(finalTick),
            resumedWorld,
            resumedScheduler);

        TestAssert.Equal(uninterruptedState.CurrentCell, resumedState.CurrentCell);
        TestAssert.Equal(uninterruptedState.DestinationCell, resumedState.DestinationCell);
        TestAssert.Equal(uninterruptedState.RouteRevision, resumedState.RouteRevision);
        TestAssert.Equal(uninterruptedState, resumedState);
        TestAssert.Equal(
            Convert.ToHexString(uninterruptedBytes),
            Convert.ToHexString(resumedBytes));
        TestAssert.Equal(uninterruptedChecksum, resumedChecksum);
    }

    private static void AdvanceMovement(
        WorldState world,
        DeterministicScheduler scheduler,
        EntityId entityId,
        MapState map,
        int firstTick,
        int finalTick)
    {
        for (int tick = firstTick; tick <= finalTick; tick++)
        {
            MovementAgentState current = GetMovement(world, entityId);
            MovementAgentState next = AuthoritativeMovementPlanner.PlanNext(
                map,
                current,
                static _ => true);
            world.Mutations.EnqueueReplace(
                entityId,
                new ComponentValue(MovementAgentState.ComponentTypeId, next));
            world.CommitMutations(scheduler, new SimTick(tick));
        }
    }

    private static MovementAgentState GetMovement(WorldState world, EntityId entityId) =>
        (MovementAgentState)world.Components.Get(entityId, MovementAgentState.ComponentTypeId);

    private static EntityId CreateMovementEntity(
        WorldState world,
        DeterministicScheduler scheduler,
        MovementAgentState state)
    {
        world.Mutations.EnqueueCreate(new[]
        {
            new ComponentValue(MovementAgentState.ComponentTypeId, state)
        });
        return world.CommitMutations(scheduler, new SimTick(1)).CreatedEntityIds[0];
    }

    private static IReadOnlyList<ComponentRegistration> Registrations() =>
        new[] { MovementAgentState.CreateRegistration() };

    private static WorldState CreateWorld(IReadOnlyList<ComponentRegistration> registrations) =>
        new(registrations);

    private static MapState CreateMap(int width, int height)
    {
        var registry = new MapCellDefinitionRegistry(new[]
        {
            new MapCellDefinition(FloorDefinition, ParticipatesInRoomTopology: true)
        });
        return new MapState(width, height, registry, FloorDefinition);
    }

    private static MapCellId Cell(int x, int y, MapState map) =>
        MapCellId.FromPosition(new GridPosition(x, y), map.Width, map.Height);

    private static DeterministicScheduler CreateScheduler() =>
        new(new ScheduledEventHandlerRegistry(Array.Empty<IScheduledEventHandler>()));
}
