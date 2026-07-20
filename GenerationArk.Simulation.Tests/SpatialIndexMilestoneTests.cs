using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.Persistence;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Tests;

public static class SpatialIndexMilestoneTests
{
    private static readonly MapCellDefinitionId VoidDefinition = new(1);

    public static void SpatialIndexesUseCanonicalCellAndEntityOrder()
    {
        WorldState world = CreateWorld(4);
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId first = CreateActive(world, scheduler, 1);
        EntityId second = CreateActive(world, scheduler, 3);
        EntityId third = CreateActive(world, scheduler, 5);

        world.Mutations.EnqueueSetPosition(third, new EntityPosition(new MapCellId(3)));
        world.Mutations.EnqueueSetPosition(second, new EntityPosition(new MapCellId(1)));
        world.Mutations.EnqueueSetPosition(first, new EntityPosition(new MapCellId(1)));
        world.CommitMutations(scheduler, new SimTick(7));

        TestAssert.Equal("1|2|3", string.Join("|", world.Spatial.PositionedEntityIds.Select(static id => id.Value)));
        TestAssert.Equal("1|3", string.Join("|", world.Spatial.OccupiedCells.Select(static cell => cell.Value)));
        TestAssert.Equal("1|2", string.Join("|", world.Spatial.GetEntities(new MapCellId(1)).Select(static id => id.Value)));
        TestAssert.Equal(new EntityPosition(new MapCellId(3)), GetPosition(world, third));
        world.ValidateEntityInvariants(scheduler);
    }

    public static void PositionMutationsRemainInvisibleUntilCommit()
    {
        WorldState world = CreateWorld(2);
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entity = CreateActive(world, scheduler, 1);

        world.Mutations.EnqueueSetPosition(entity, new EntityPosition(new MapCellId(1)));
        TestAssert.Equal(1, world.Mutations.PositionMutationCount);
        TestAssert.True(!world.Spatial.TryGetPosition(entity, out _));

        MutationCommitResult result = world.CommitMutations(scheduler, new SimTick(3));
        TestAssert.Equal(1, result.AppliedMutationCount);
        TestAssert.Equal(1, result.AppliedPositionMutationCount);
        TestAssert.Equal(new EntityPosition(new MapCellId(1)), GetPosition(world, entity));
        TestAssert.Equal(0, world.Mutations.Count);
    }

    public static void SetMoveAndClearUpdateBothIndexesAtomically()
    {
        WorldState world = CreateWorld(3);
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entity = CreateActive(world, scheduler, 1);

        world.Mutations.EnqueueSetPosition(entity, new EntityPosition(new MapCellId(0)));
        world.CommitMutations(scheduler, new SimTick(3));
        world.Mutations.EnqueueSetPosition(entity, new EntityPosition(new MapCellId(2)));
        world.CommitMutations(scheduler, new SimTick(4));
        TestAssert.Equal(0, world.Spatial.GetEntities(new MapCellId(0)).Count);
        TestAssert.Equal(entity, world.Spatial.GetEntities(new MapCellId(2)).Single());

        world.Mutations.EnqueueClearPosition(entity);
        world.CommitMutations(scheduler, new SimTick(5));
        TestAssert.True(!world.Spatial.TryGetPosition(entity, out _));
        TestAssert.Equal(0, world.Spatial.GetEntities(new MapCellId(2)).Count);
        world.ValidateEntityInvariants(scheduler);
    }

    public static void InvalidEntityLifecycleAndCellTargetsFailBeforeMutation()
    {
        WorldState missing = CreateWorld(2);
        missing.Mutations.EnqueueSetPosition(new EntityId(99), new EntityPosition(new MapCellId(0)));
        TestAssert.Throws<PositionMutationValidationException>(() =>
            missing.CommitMutations(CreateScheduler(), new SimTick(1)));
        TestAssert.Equal(1, missing.Mutations.PositionMutationCount);

        WorldState pending = CreateWorld(2);
        DeterministicScheduler pendingScheduler = CreateScheduler();
        EntityId pendingEntity = CommitCreate(pending, pendingScheduler, 1);
        pending.Mutations.EnqueueSetPosition(pendingEntity, new EntityPosition(new MapCellId(0)));
        TestAssert.Throws<PositionMutationValidationException>(() =>
            pending.CommitMutations(pendingScheduler, new SimTick(2)));
        TestAssert.True(!pending.Spatial.TryGetPosition(pendingEntity, out _));

        WorldState invalidCell = CreateWorld(2);
        DeterministicScheduler invalidScheduler = CreateScheduler();
        EntityId active = CreateActive(invalidCell, invalidScheduler, 1);
        invalidCell.Mutations.EnqueueSetPosition(active, new EntityPosition(new MapCellId(2)));
        TestAssert.Throws<PositionMutationValidationException>(() =>
            invalidCell.CommitMutations(invalidScheduler, new SimTick(3)));
        TestAssert.True(!invalidCell.Spatial.TryGetPosition(active, out _));
    }

    public static void ConflictingPositionBatchFailsBeforePartialApplication()
    {
        WorldState world = CreateWorld(3);
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entity = CreateActive(world, scheduler, 1);
        ulong before = StateChecksum.Compute(new SimTick(2), world);

        world.Mutations.EnqueueSetPosition(entity, new EntityPosition(new MapCellId(0)));
        world.Mutations.EnqueueSetPosition(entity, new EntityPosition(new MapCellId(1)));
        PositionMutationValidationException exception = TestAssert.Throws<PositionMutationValidationException>(() =>
            world.CommitMutations(scheduler, new SimTick(3)));

        TestAssert.Equal(2, exception.ConflictingMutations.Count);
        TestAssert.True(!world.Spatial.TryGetPosition(entity, out _));
        TestAssert.Equal(2, world.Mutations.PositionMutationCount);
        TestAssert.Equal(before, StateChecksum.Compute(new SimTick(2), world));
    }

    public static void RequestOrderDoesNotChangeCanonicalSpatialState()
    {
        WorldState first = CreateWorld(3);
        WorldState second = CreateWorld(3);
        DeterministicScheduler firstScheduler = CreateScheduler();
        DeterministicScheduler secondScheduler = CreateScheduler();
        EntityId firstOne = CreateActive(first, firstScheduler, 1);
        EntityId firstTwo = CreateActive(first, firstScheduler, 3);
        EntityId secondOne = CreateActive(second, secondScheduler, 1);
        EntityId secondTwo = CreateActive(second, secondScheduler, 3);

        first.Mutations.EnqueueSetPosition(firstTwo, new EntityPosition(new MapCellId(2)));
        first.Mutations.EnqueueSetPosition(firstOne, new EntityPosition(new MapCellId(0)));
        second.Mutations.EnqueueSetPosition(secondOne, new EntityPosition(new MapCellId(0)));
        second.Mutations.EnqueueSetPosition(secondTwo, new EntityPosition(new MapCellId(2)));
        first.CommitMutations(firstScheduler, new SimTick(5));
        second.CommitMutations(secondScheduler, new SimTick(5));

        TestAssert.Equal(
            Convert.ToHexString(SpatialStateSerializer.ToUtf8(first.Spatial)),
            Convert.ToHexString(SpatialStateSerializer.ToUtf8(second.Spatial)));
        TestAssert.Equal(
            StateChecksum.Compute(new SimTick(5), first),
            StateChecksum.Compute(new SimTick(5), second));
    }

    public static void DestroyEntityRemovesSpatialMembershipDuringCommit()
    {
        WorldState world = CreateWorld(2);
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entity = CreateActive(world, scheduler, 1);
        world.Mutations.EnqueueSetPosition(entity, new EntityPosition(new MapCellId(1)));
        world.CommitMutations(scheduler, new SimTick(3));

        world.Mutations.EnqueueDestroy(entity);
        world.CommitMutations(scheduler, new SimTick(4));
        TestAssert.True(world.Entities.IsRetired(entity));
        TestAssert.True(!world.Spatial.TryGetPosition(entity, out _));
        TestAssert.Equal(0, world.Spatial.GetEntities(new MapCellId(1)).Count);
        world.ValidateEntityInvariants(scheduler);
    }

    public static void SpatialStateSaveLoadRoundTripIsCanonical()
    {
        WorldState source = CreateWorld(3);
        DeterministicScheduler sourceScheduler = CreateScheduler();
        EntityId first = CreateActive(source, sourceScheduler, 1);
        EntityId second = CreateActive(source, sourceScheduler, 3);
        source.Mutations.EnqueueSetPosition(second, new EntityPosition(new MapCellId(2)));
        source.Mutations.EnqueueSetPosition(first, new EntityPosition(new MapCellId(0)));
        source.CommitMutations(sourceScheduler, new SimTick(5));

        byte[] firstBytes = SpatialStateSerializer.ToUtf8(source.Spatial);
        SpatialStateSnapshot permuted = SpatialStateSerializer.FromUtf8(firstBytes);
        WorldState restored = CreateWorld(3);
        DeterministicScheduler restoredScheduler = CreateScheduler();
        CreateActive(restored, restoredScheduler, 1);
        CreateActive(restored, restoredScheduler, 3);
        SpatialStateSerializer.Restore(permuted, restored);
        byte[] secondBytes = SpatialStateSerializer.ToUtf8(restored.Spatial);

        TestAssert.Equal(Convert.ToHexString(firstBytes), Convert.ToHexString(secondBytes));
        TestAssert.Equal(StateChecksum.Compute(new SimTick(5), source), StateChecksum.Compute(new SimTick(5), restored));

        var metadata = new SimulationSaveMetadata(
            simulationSchemaVersion: 1,
            replayFormatVersion: 1,
            checksumFormatVersion: 4,
            randomAlgorithmVersion: 1,
            rootSeed: 0x1234UL,
            currentTick: 5,
            requestedSpeedMultiplier: 1,
            isPaused: false,
            nextEntityId: 3,
            nextCommandSequence: 0,
            nextSchedulerEventId: 1,
            nextSchedulerCreationSequence: 0,
            calendarDefinitionId: "ship-calendar-360-v1",
            buildVersion: "step-11-test");
        var envelope = new SimulationSaveEnvelope(
            metadata,
            StateChecksum.Compute(new SimTick(5), source),
            new byte[] { 0x01, 0x02, 0x03 },
            SpatialStateSerializer.Capture(source.Spatial));
        byte[] firstEnvelopeBytes = SimulationSaveEnvelopeJson.ToUtf8(envelope);
        SimulationSaveEnvelope restoredEnvelope = SimulationSaveEnvelopeJson.FromUtf8(firstEnvelopeBytes);
        byte[] secondEnvelopeBytes = SimulationSaveEnvelopeJson.ToUtf8(restoredEnvelope);

        TestAssert.Equal(
            Convert.ToHexString(firstEnvelopeBytes),
            Convert.ToHexString(secondEnvelopeBytes));
        SpatialStateSnapshot restoredSpatialState = restoredEnvelope.SpatialState
            ?? throw new InvalidOperationException("Spatial state was not preserved through the save envelope.");
        TestAssert.Equal(
            Convert.ToHexString(SpatialStateSerializer.ToUtf8(envelope.SpatialState!)),
            Convert.ToHexString(SpatialStateSerializer.ToUtf8(restoredSpatialState)));

        string canonicalEnvelopeJson = SimulationSaveEnvelopeJson.ToJson(envelope);
        int spatialPropertyIndex = canonicalEnvelopeJson.LastIndexOf(
            ",\"spatialStateBase64\"",
            StringComparison.Ordinal);
        TestAssert.True(spatialPropertyIndex >= 0);
        string missingSpatialState = canonicalEnvelopeJson[..spatialPropertyIndex] + "}";
        TestAssert.Throws<KeyNotFoundException>(() =>
            SimulationSaveEnvelopeJson.FromJson(missingSpatialState));

        TestAssert.Throws<InvalidOperationException>(() => SpatialStateSerializer.Restore(
            new SpatialStateSnapshot(1, new[]
            {
                new SpatialStateEntrySnapshot(first, new MapCellId(0)),
                new SpatialStateEntrySnapshot(first, new MapCellId(1))
            }),
            restored));
    }

    public static void SpatialStateParticipatesInComponentAndGlobalChecksums()
    {
        WorldState empty = CreateWorld(2);
        WorldState positioned = CreateWorld(2);
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entity = CreateActive(positioned, scheduler, 1);
        positioned.Mutations.EnqueueSetPosition(entity, new EntityPosition(new MapCellId(1)));
        positioned.CommitMutations(scheduler, new SimTick(3));

        TickChecksum detailed = StateChecksum.ComputeDetailed(new SimTick(3), positioned);
        TestAssert.Equal<ulong>(4, detailed.FormatVersion.Value);
        TestAssert.True(detailed.Components.Any(static checksum =>
            checksum.ComponentId == new ChecksumComponentId("spatial")));
        TestAssert.True(
            StateChecksum.Compute(new SimTick(3), empty) != StateChecksum.Compute(new SimTick(3), positioned));
    }

    public static void SpatialReplayFramePatternsAndChurnMatchChecksums()
    {
        SpatialChurnResult stable = RunChurn(new[] { 1 });
        SpatialChurnResult repeated = RunChurn(new[] { 1 });
        SpatialChurnResult varied = RunChurn(new[] { 4, 1, 7, 2 });

        AssertEquivalent(stable, repeated);
        AssertEquivalent(stable, varied);
        TestAssert.True(stable.RetainedCheckpoints.Count <= 8);
    }

    private static SpatialChurnResult RunChurn(IReadOnlyList<int> framePattern)
    {
        WorldState world = CreateWorld(8);
        DeterministicScheduler scheduler = CreateScheduler();
        var checkpoints = new Queue<ulong>();
        long tick = 0;
        int frame = 0;
        while (tick < 96)
        {
            int budget = framePattern[frame % framePattern.Count];
            frame++;
            for (int step = 0; step < budget && tick < 96; step++)
            {
                tick++;
                SimTick simTick = new(tick);
                world.ActivatePendingEntities(simTick);
                world.Mutations.EnqueueCreate();
                if (tick > 2)
                {
                    EntityId target = new((ulong)(tick - 2));
                    if (world.Entities.GetLifecycleState(target) == EntityLifecycleState.Active)
                    {
                        world.Mutations.EnqueueSetPosition(target, new EntityPosition(new MapCellId((int)(tick % 8))));
                    }
                }
                if (tick > 8 && tick % 3 == 0)
                {
                    EntityId target = new((ulong)(tick - 7));
                    if (world.Spatial.TryGetPosition(target, out _))
                    {
                        world.Mutations.EnqueueClearPosition(target);
                    }
                }
                if (tick > 16)
                {
                    world.Mutations.EnqueueDestroy(new EntityId((ulong)(tick - 16)));
                }
                world.CommitMutations(scheduler, simTick);

                if (tick % 24 == 0)
                {
                    byte[] saved = SpatialStateSerializer.ToUtf8(world.Spatial);
                    SpatialStateSerializer.Restore(saved, world);
                }
                if (tick % 12 == 0)
                {
                    checkpoints.Enqueue(StateChecksum.Compute(simTick, world, scheduler));
                    while (checkpoints.Count > 8)
                    {
                        checkpoints.Dequeue();
                    }
                }
                world.ValidateEntityInvariants(scheduler);
            }
        }

        TickChecksum finalChecksum = StateChecksum.ComputeDetailed(new SimTick(tick), world, scheduler);
        return new SpatialChurnResult(
            finalChecksum.GlobalChecksum,
            string.Join(
                "|",
                finalChecksum.Components.Select(static component =>
                    $"{component.ComponentId.Value}:{component.Checksum:X16}")),
            checkpoints.ToArray(),
            Convert.ToHexString(SpatialStateSerializer.ToUtf8(world.Spatial)));
    }

    private static void AssertEquivalent(SpatialChurnResult expected, SpatialChurnResult actual)
    {
        TestAssert.Equal(expected.FinalChecksum, actual.FinalChecksum);
        TestAssert.Equal(expected.ComponentChecksums, actual.ComponentChecksums);
        TestAssert.Equal(string.Join("|", expected.RetainedCheckpoints), string.Join("|", actual.RetainedCheckpoints));
        TestAssert.Equal(expected.CanonicalSpatialHex, actual.CanonicalSpatialHex);
    }

    private static WorldState CreateWorld(int width)
        => new(map: new MapState(width, 1, new MapCellDefinitionRegistry(new[]
        {
            new MapCellDefinition(VoidDefinition, ParticipatesInRoomTopology: false)
        }), VoidDefinition));

    private static DeterministicScheduler CreateScheduler()
        => new(new ScheduledEventHandlerRegistry(Array.Empty<IScheduledEventHandler>()));

    private static EntityId CreateActive(WorldState world, DeterministicScheduler scheduler, long tick)
    {
        EntityId entity = CommitCreate(world, scheduler, tick);
        world.ActivatePendingEntities(new SimTick(tick + 1));
        return entity;
    }

    private static EntityId CommitCreate(WorldState world, DeterministicScheduler scheduler, long tick)
    {
        world.Mutations.EnqueueCreate();
        return world.CommitMutations(scheduler, new SimTick(tick)).CreatedEntityIds.Single();
    }

    private static EntityPosition GetPosition(WorldState world, EntityId entity)
        => world.Spatial.TryGetPosition(entity, out EntityPosition position)
            ? position
            : throw new InvalidOperationException($"Entity {entity} was expected to have a position.");

    private sealed record SpatialChurnResult(
        ulong FinalChecksum,
        string ComponentChecksums,
        IReadOnlyList<ulong> RetainedCheckpoints,
        string CanonicalSpatialHex);
}
