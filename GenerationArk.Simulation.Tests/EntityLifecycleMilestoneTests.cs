using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Persistence;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Tests;

internal static class EntityLifecycleMilestoneTests
{
    private static readonly ComponentTypeId CounterType = new("counter-v1");
    private static readonly ComponentTypeId FlagType = new("flag-v1");
    private static readonly ScheduledEventTypeId LifecycleNoOpType = new("lifecycle-noop-v1");

    public static void EntityIdsAreMonotonicAndNeverReused()
    {
        WorldState world = CreateWorld();
        DeterministicScheduler scheduler = CreateScheduler();

        MutationCommitResult first = CommitCreate(
            world,
            scheduler,
            new SimTick(1),
            new ComponentValue(CounterType, new CounterComponent(10)));
        EntityId firstId = first.CreatedEntityIds.Single();

        world.Mutations.EnqueueDestroy(firstId);
        world.CommitEntityMutations(scheduler, new SimTick(2));

        MutationCommitResult second = CommitCreate(
            world,
            scheduler,
            new SimTick(3),
            new ComponentValue(CounterType, new CounterComponent(20)));
        EntityId secondId = second.CreatedEntityIds.Single();

        TestAssert.Equal(new EntityId(1), firstId);
        TestAssert.Equal(new EntityId(2), secondId);
        TestAssert.True(world.Entities.IsRetired(firstId));
        TestAssert.True(!world.Entities.Contains(firstId));
        TestAssert.Equal<ulong>(3, world.Entities.NextEntityId);
    }

    public static void EntityIterationIsCanonicalRegardlessOfInsertionOrder()
    {
        const string firstJson =
            "{\"schemaVersion\":1,\"nextEntityId\":\"0x0000000000000004\",\"retiredEntityIds\":[],\"entities\":[" +
            "{\"entityId\":\"0x0000000000000003\",\"lifecycleState\":2,\"components\":[]}," +
            "{\"entityId\":\"0x0000000000000001\",\"lifecycleState\":2,\"components\":[]}," +
            "{\"entityId\":\"0x0000000000000002\",\"lifecycleState\":2,\"components\":[]}]}";
        const string secondJson =
            "{\"schemaVersion\":1,\"nextEntityId\":\"0x0000000000000004\",\"retiredEntityIds\":[],\"entities\":[" +
            "{\"entityId\":\"0x0000000000000002\",\"lifecycleState\":2,\"components\":[]}," +
            "{\"entityId\":\"0x0000000000000003\",\"lifecycleState\":2,\"components\":[]}," +
            "{\"entityId\":\"0x0000000000000001\",\"lifecycleState\":2,\"components\":[]}]}";

        WorldState first = EntityStateSerializer.Restore(
            firstJson,
            Array.Empty<ComponentRegistration>());
        WorldState second = EntityStateSerializer.Restore(
            secondJson,
            Array.Empty<ComponentRegistration>());

        string expectedOrder = "1|2|3";
        TestAssert.Equal(
            expectedOrder,
            string.Join("|", first.Entities.AllEntityIds.Select(static id => id.Value)));
        TestAssert.Equal(
            expectedOrder,
            string.Join("|", second.Entities.AllEntityIds.Select(static id => id.Value)));
        TestAssert.Equal(
            EntityStateSerializer.ToJson(first),
            EntityStateSerializer.ToJson(second));
    }

    public static void DuplicateEntityAndComponentTypeRegistrationFailsFast()
    {
        const string duplicateEntityJson =
            "{\"schemaVersion\":1,\"nextEntityId\":\"0x0000000000000003\",\"retiredEntityIds\":[],\"entities\":[" +
            "{\"entityId\":\"0x0000000000000001\",\"lifecycleState\":2,\"components\":[]}," +
            "{\"entityId\":\"0x0000000000000001\",\"lifecycleState\":2,\"components\":[]}]}";

        TestAssert.Throws<InvalidOperationException>(() =>
            EntityStateSerializer.Restore(
                duplicateEntityJson,
                Array.Empty<ComponentRegistration>()));

        ComponentRegistration counter = CounterRegistration();
        TestAssert.Throws<InvalidOperationException>(() =>
            new ComponentRegistry(new[] { counter, CounterRegistration() }));

        ComponentRegistration alternateCounterId = ComponentRegistration.Create<CounterComponent>(
            new ComponentTypeId("counter-alternate-v1"),
            static value => value.Value.ToString(CultureInfo.InvariantCulture),
            static payload => new CounterComponent(long.Parse(payload, CultureInfo.InvariantCulture)),
            static (writer, value) => writer.AddInt64(value.Value));
        TestAssert.Throws<InvalidOperationException>(() =>
            new ComponentRegistry(new[] { counter, alternateCounterId }));
    }

    public static void StructuralMutationsRemainInvisibleUntilCommit()
    {
        WorldState world = CreateWorld();
        DeterministicScheduler scheduler = CreateScheduler();

        world.Mutations.EnqueueCreate(new[]
        {
            new ComponentValue(CounterType, new CounterComponent(7))
        });

        TestAssert.Equal(0, world.Entities.LiveCount);
        TestAssert.Equal(0, world.Components.GetStore(CounterType).Count);
        TestAssert.Equal(1, world.Mutations.Count);

        MutationCommitResult result = world.CommitEntityMutations(scheduler, new SimTick(1));
        EntityId entityId = result.CreatedEntityIds.Single();

        TestAssert.Equal(1, world.Entities.LiveCount);
        TestAssert.Equal(EntityLifecycleState.PendingActivation, world.Entities.GetLifecycleState(entityId));
        TestAssert.Equal(1, world.Components.GetStore(CounterType).Count);
        TestAssert.Equal(0, world.Mutations.Count);
    }

    public static void CreatedEntitiesActivateAtNextPreSimulationPhase()
    {
        var observedActiveCounts = new List<int>();
        WorldState world = CreateWorld();
        var fixture = new SimulationFixture(
            new ISimSystem[]
            {
                new ObserveActiveEntitiesSystem(observedActiveCounts),
                new CreateEntityOnFirstCommitSystem()
            },
            world: world);

        fixture.Runner.RunOneTick();
        TestAssert.Equal("0", string.Join("|", observedActiveCounts));
        TestAssert.Equal(0, world.Entities.ActiveCount);
        TestAssert.Equal(1, world.Entities.PendingActivationCount);

        fixture.Runner.RunOneTick();
        TestAssert.Equal("0|1", string.Join("|", observedActiveCounts));
        TestAssert.Equal(1, world.Entities.ActiveCount);
        TestAssert.Equal(0, world.Entities.PendingActivationCount);
    }

    public static void CommitAppliesMutationsInStableBufferedOrder()
    {
        WorldState world = CreateWorld();
        DeterministicScheduler scheduler = CreateScheduler();
        MutationCommitResult creates = CommitCreate(world, scheduler, new SimTick(1));
        EntityId first = creates.CreatedEntityIds.Single();
        EntityId second = CommitCreate(world, scheduler, new SimTick(1)).CreatedEntityIds.Single();

        world.Mutations.EnqueueAdd(
            second,
            new ComponentValue(CounterType, new CounterComponent(20)));
        world.Mutations.EnqueueAdd(
            first,
            new ComponentValue(FlagType, new FlagComponent(true)));
        world.CommitEntityMutations(scheduler, new SimTick(2));

        EntityLifecycleEvent[] bufferedEvents = world.LifecycleEvents.Events
            .Where(static item => item.Tick == 2)
            .ToArray();
        TestAssert.Equal(2, bufferedEvents.Length);
        TestAssert.Equal(second, bufferedEvents[0].EntityId);
        TestAssert.Equal(first, bufferedEvents[1].EntityId);
        TestAssert.True(bufferedEvents[0].MutationSequence < bufferedEvents[1].MutationSequence);

        WorldState initialWorld = CreateWorld();
        initialWorld.Mutations.EnqueueCreate(new[]
        {
            new ComponentValue(FlagType, new FlagComponent(true)),
            new ComponentValue(CounterType, new CounterComponent(1))
        });
        initialWorld.CommitEntityMutations(CreateScheduler(), new SimTick(1));
        ComponentTypeId[] initialComponentOrder = initialWorld.LifecycleEvents.Events
            .Where(static item => item.Kind == EntityLifecycleEventKind.ComponentAdded)
            .Select(static item => item.ComponentTypeId!.Value)
            .ToArray();
        TestAssert.Equal(
            "counter-v1|flag-v1",
            string.Join("|", initialComponentOrder.Select(static id => id.Value)));
    }

    public static void DestroyEntityRemovesComponentsAndCancelsOwnedEvents()
    {
        WorldState world = CreateWorld();
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entityId = CommitCreate(
            world,
            scheduler,
            new SimTick(1),
            new ComponentValue(CounterType, new CounterComponent(99)),
            new ComponentValue(FlagType, new FlagComponent(true)))
            .CreatedEntityIds.Single();

        scheduler.ScheduleAt(
            new SimTick(100),
            SimPhase.AgentAction,
            priority: 0,
            eventData: new ScheduledEventData(
                LifecycleNoOpType,
                new ScheduledEventOwnerId(entityId.Value),
                payload: "owned"));

        world.Mutations.EnqueueDestroy(entityId);
        MutationCommitResult result = world.CommitEntityMutations(scheduler, new SimTick(2));

        TestAssert.True(world.Entities.IsRetired(entityId));
        TestAssert.Equal(0, world.Components.GetStore(CounterType).Count);
        TestAssert.Equal(0, world.Components.GetStore(FlagType).Count);
        TestAssert.Equal(0, scheduler.PendingCount);
        TestAssert.Equal(1, result.CancelledScheduledEventCount);
        world.ValidateEntityInvariants(scheduler);
    }

    public static void ConflictingMutationBatchFailsBeforePartialApplication()
    {
        WorldState world = CreateWorld();
        DeterministicScheduler scheduler = CreateScheduler();
        EntityId entityId = CommitCreate(
            world,
            scheduler,
            new SimTick(1),
            new ComponentValue(CounterType, new CounterComponent(5)))
            .CreatedEntityIds.Single();

        world.Mutations.EnqueueDestroy(entityId);
        world.Mutations.EnqueueRemove(entityId, CounterType);

        MutationValidationException exception = TestAssert.Throws<MutationValidationException>(() =>
            world.CommitEntityMutations(scheduler, new SimTick(2)));

        TestAssert.Equal(2, exception.ConflictingMutations.Count);
        TestAssert.Equal(2, world.Mutations.Count);
        TestAssert.True(world.Entities.Contains(entityId));
        TestAssert.True(world.Components.Contains(entityId, CounterType));
        TestAssert.Equal(0, world.Entities.RetiredCount);
        TestAssert.Equal(
            EntityLifecycleEventKind.MutationRejected,
            world.LifecycleEvents.Events.Last().Kind);
    }

    public static void EntityStateSaveLoadRoundTripIsCanonical()
    {
        IReadOnlyList<ComponentRegistration> registrations = Registrations();
        WorldState world = new(registrations);
        DeterministicScheduler scheduler = CreateScheduler();

        EntityId retiredId = CommitCreate(
            world,
            scheduler,
            new SimTick(1),
            new ComponentValue(CounterType, new CounterComponent(11)))
            .CreatedEntityIds.Single();
        world.ActivatePendingEntities(new SimTick(2));
        EntityId pendingId = CommitCreate(
            world,
            scheduler,
            new SimTick(2),
            new ComponentValue(FlagType, new FlagComponent(true)))
            .CreatedEntityIds.Single();
        world.Mutations.EnqueueDestroy(retiredId);
        world.CommitEntityMutations(scheduler, new SimTick(3));

        byte[] firstBytes = EntityStateSerializer.ToUtf8(world);
        WorldState restored = EntityStateSerializer.Restore(firstBytes, registrations);
        byte[] secondBytes = EntityStateSerializer.ToUtf8(restored);

        TestAssert.Equal(
            Convert.ToHexString(firstBytes),
            Convert.ToHexString(secondBytes));
        TestAssert.True(restored.Entities.IsRetired(retiredId));
        TestAssert.Equal(
            EntityLifecycleState.PendingActivation,
            restored.Entities.GetLifecycleState(pendingId));
        TestAssert.Equal(
            StateChecksum.Compute(new SimTick(3), world),
            StateChecksum.Compute(new SimTick(3), restored));
    }

    public static void EntityLifecycleReplayFramePatternsAndChurnSoakMatchChecksums()
    {
        ChurnResult stableA = RunChurn(new[] { 1 });
        ChurnResult stableB = RunChurn(new[] { 1 });
        ChurnResult variedA = RunChurn(new[] { 4, 1, 7, 2 });
        ChurnResult variedB = RunChurn(new[] { 13, 3, 5 });

        AssertChurnEquivalent(stableA, stableB);
        AssertChurnEquivalent(stableA, variedA);
        AssertChurnEquivalent(stableA, variedB);
        TestAssert.Equal(30, stableA.LiveCount);
        TestAssert.Equal(570, stableA.RetiredCount);
        TestAssert.Equal<ulong>(601, stableA.NextEntityId);
        TestAssert.Equal(30, stableA.PendingScheduledEvents);
        TestAssert.True(stableA.LifecycleEventCount <= 64);
    }

    private static ChurnResult RunChurn(IReadOnlyList<int> framePattern)
    {
        if (framePattern.Count == 0 || framePattern.Any(static value => value <= 0))
        {
            throw new ArgumentException("Frame pattern must contain positive tick budgets.", nameof(framePattern));
        }

        IReadOnlyList<ComponentRegistration> registrations = Registrations();
        WorldState world = new(registrations, lifecycleEventRetentionCapacity: 64);
        DeterministicScheduler scheduler = CreateScheduler();
        var checkpoints = new List<ulong>();
        int tick = 0;
        int frame = 0;

        while (tick < 600)
        {
            int budget = framePattern[frame % framePattern.Count];
            frame++;
            for (int step = 0; step < budget && tick < 600; step++)
            {
                tick++;
                SimTick simTick = new(tick);
                world.ActivatePendingEntities(simTick);
                world.Mutations.EnqueueCreate(new[]
                {
                    new ComponentValue(CounterType, new CounterComponent(tick * 17L))
                });

                if (tick >= 2 && tick % 2 == 0)
                {
                    EntityId addTarget = new((ulong)(tick - 1));
                    world.Mutations.EnqueueAdd(
                        addTarget,
                        new ComponentValue(FlagType, new FlagComponent(true)));
                }
                if (tick >= 4 && tick % 2 == 0)
                {
                    EntityId removeTarget = new((ulong)(tick - 3));
                    world.Mutations.EnqueueRemove(removeTarget, FlagType);
                }
                if (tick > 30)
                {
                    world.Mutations.EnqueueDestroy(new EntityId((ulong)(tick - 30)));
                }

                MutationCommitResult commit = world.CommitEntityMutations(scheduler, simTick);
                EntityId createdId = commit.CreatedEntityIds.Single();
                scheduler.ScheduleAt(
                    new SimTick(tick + 100_000L),
                    SimPhase.Narrative,
                    priority: 0,
                    new ScheduledEventData(
                        LifecycleNoOpType,
                        new ScheduledEventOwnerId(createdId.Value),
                        payload: tick.ToString(CultureInfo.InvariantCulture)));

                world.ValidateEntityInvariants(scheduler);

                if (tick % 97 == 0)
                {
                    byte[] savedWorld = EntityStateSerializer.ToUtf8(world);
                    SchedulerSnapshot savedScheduler = scheduler.CaptureSnapshot();
                    world = EntityStateSerializer.Restore(
                        savedWorld,
                        registrations,
                        lifecycleEventRetentionCapacity: 64);
                    scheduler = CreateScheduler();
                    scheduler.Restore(savedScheduler);
                    world.ValidateEntityInvariants(scheduler);
                }

                if (tick % 50 == 0)
                {
                    checkpoints.Add(StateChecksum.Compute(simTick, world, scheduler));
                }
            }
        }

        return new ChurnResult(
            checkpoints.ToArray(),
            StateChecksum.Compute(new SimTick(tick), world, scheduler),
            world.Entities.LiveCount,
            world.Entities.RetiredCount,
            world.Entities.NextEntityId,
            scheduler.PendingCount,
            world.LifecycleEvents.Count);
    }

    private static void AssertChurnEquivalent(ChurnResult expected, ChurnResult actual)
    {
        TestAssert.Equal(
            string.Join("|", expected.Checkpoints),
            string.Join("|", actual.Checkpoints));
        TestAssert.Equal(expected.FinalChecksum, actual.FinalChecksum);
        TestAssert.Equal(expected.LiveCount, actual.LiveCount);
        TestAssert.Equal(expected.RetiredCount, actual.RetiredCount);
        TestAssert.Equal(expected.NextEntityId, actual.NextEntityId);
        TestAssert.Equal(expected.PendingScheduledEvents, actual.PendingScheduledEvents);
    }

    private static WorldState CreateWorld()
        => new(Registrations());

    private static IReadOnlyList<ComponentRegistration> Registrations()
        => new[]
        {
            CounterRegistration(),
            FlagRegistration()
        };

    private static ComponentRegistration CounterRegistration()
        => ComponentRegistration.Create<CounterComponent>(
            CounterType,
            static value => value.Value.ToString(CultureInfo.InvariantCulture),
            static payload => new CounterComponent(long.Parse(payload, CultureInfo.InvariantCulture)),
            static (writer, value) => writer.AddInt64(value.Value));

    private static ComponentRegistration FlagRegistration()
        => ComponentRegistration.Create<FlagComponent>(
            FlagType,
            static value => value.Value ? "1" : "0",
            static payload => payload switch
            {
                "1" => new FlagComponent(true),
                "0" => new FlagComponent(false),
                _ => throw new FormatException("Flag component payload must be 0 or 1.")
            },
            static (writer, value) => writer.AddBoolean(value.Value));

    private static DeterministicScheduler CreateScheduler()
        => new(new ScheduledEventHandlerRegistry(Array.Empty<IScheduledEventHandler>()));

    private static MutationCommitResult CommitCreate(
        WorldState world,
        DeterministicScheduler scheduler,
        SimTick tick,
        params ComponentValue[] initialComponents)
    {
        world.Mutations.EnqueueCreate(initialComponents);
        return world.CommitEntityMutations(scheduler, tick);
    }

    private sealed record CounterComponent(long Value);
    private sealed record FlagComponent(bool Value);

    private sealed record ChurnResult(
        IReadOnlyList<ulong> Checkpoints,
        ulong FinalChecksum,
        int LiveCount,
        int RetiredCount,
        ulong NextEntityId,
        int PendingScheduledEvents,
        int LifecycleEventCount);

    private sealed class CreateEntityOnFirstCommitSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.Commit;
        public int Order => 0;
        public SystemId Id => new("step9-create-on-first-commit");

        public void Tick(SimContext context)
        {
            if (context.Tick == new SimTick(1))
            {
                context.World.Mutations.EnqueueCreate();
            }
        }
    }

    private sealed class ObserveActiveEntitiesSystem : ISimSystem
    {
        private readonly List<int> _observations;

        public ObserveActiveEntitiesSystem(List<int> observations)
        {
            _observations = observations;
        }

        public SimPhase Phase => SimPhase.PreSimulation;
        public int Order => 0;
        public SystemId Id => new("step9-observe-active-entities");

        public void Tick(SimContext context)
            => _observations.Add(context.World.Entities.ActiveCount);
    }
}
