using System;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.Movement;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Tests;

internal static class ComponentReplacementMilestoneTests
{
    public static void ReplacementRemainsInvisibleUntilCommitAndThenApplies()
    {
        WorldState world = CreateWorld();
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entityId = CreateMovementEntity(world, scheduler);
        var original = (MovementAgentState)world.Components.Get(entityId, MovementAgentState.ComponentTypeId);
        var replacement = new MovementAgentState(new MapCellId(1), new MapCellId(3), 1);

        world.Mutations.EnqueueReplace(
            entityId,
            new ComponentValue(MovementAgentState.ComponentTypeId, replacement));

        TestAssert.Equal(original, world.Components.Get(entityId, MovementAgentState.ComponentTypeId));
        world.CommitMutations(scheduler, new SimTick(2));
        TestAssert.Equal(replacement, world.Components.Get(entityId, MovementAgentState.ComponentTypeId));
    }

    public static void ConflictingReplacementsRejectAtomically()
    {
        WorldState world = CreateWorld();
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entityId = CreateMovementEntity(world, scheduler);
        var original = (MovementAgentState)world.Components.Get(entityId, MovementAgentState.ComponentTypeId);

        world.Mutations.EnqueueReplace(
            entityId,
            new ComponentValue(
                MovementAgentState.ComponentTypeId,
                new MovementAgentState(new MapCellId(1), new MapCellId(3), 1)));
        world.Mutations.EnqueueReplace(
            entityId,
            new ComponentValue(
                MovementAgentState.ComponentTypeId,
                new MovementAgentState(new MapCellId(2), new MapCellId(3), 2)));

        TestAssert.Throws<MutationValidationException>(
            () => world.CommitMutations(scheduler, new SimTick(2)));
        TestAssert.Equal(original, world.Components.Get(entityId, MovementAgentState.ComponentTypeId));
    }

    public static void MovementReplacementChangesCanonicalChecksum()
    {
        WorldState world = CreateWorld();
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entityId = CreateMovementEntity(world, scheduler);
        ulong before = StateChecksum.Compute(new SimTick(1), world, scheduler);

        world.Mutations.EnqueueReplace(
            entityId,
            new ComponentValue(
                MovementAgentState.ComponentTypeId,
                new MovementAgentState(new MapCellId(1), new MapCellId(3), 1)));
        world.CommitMutations(scheduler, new SimTick(2));
        ulong after = StateChecksum.Compute(new SimTick(2), world, scheduler);

        TestAssert.True(before != after, "Movement replacement must change the canonical checksum.");
    }

    private static EntityId CreateMovementEntity(WorldState world, DeterministicScheduler scheduler)
    {
        world.Mutations.EnqueueCreate(new[]
        {
            new ComponentValue(
                MovementAgentState.ComponentTypeId,
                new MovementAgentState(new MapCellId(0), new MapCellId(3), 0))
        });
        MutationCommitResult result = world.CommitMutations(scheduler, new SimTick(1));
        return result.CreatedEntityIds[0];
    }

    private static WorldState CreateWorld()
    {
        var definitions = new MapCellDefinitionRegistry(new[]
        {
            new MapCellDefinition(new MapCellDefinitionId(1), ParticipatesInRoomTopology: true)
        });
        var map = new MapState(4, 1, definitions, new MapCellDefinitionId(1));
        return new WorldState(
            componentRegistrations: new[] { MovementAgentState.CreateRegistration() },
            map: map);
    }

    private static DeterministicScheduler CreateScheduler() =>
        new(new ScheduledEventHandlerRegistry(Array.Empty<IScheduledEventHandler>()));
}
