using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Scheduling;

namespace GenerationArk.Simulation.Tests;

internal static class SchedulerMilestoneTests
{
    private static readonly ScheduledEventTypeId TraceType = new("test.trace");

    public static void EventsNeverExecuteBeforeTheirDueTick()
    {
        SimulationFixture fixture = CreateFixture();
        fixture.Scheduler.ScheduleAt(
            new SimTick(3),
            SimPhase.AgentState,
            priority: 0,
            eventData: TraceData("due-three"));

        fixture.Runner.RunTicks(2);
        TestAssert.Equal(0, EventTrace(fixture).Count);

        fixture.Runner.RunOneTick();
        TestAssert.Equal(1, EventTrace(fixture).Count);
        TestAssert.Equal(
            "event:due-three@3:AgentState",
            EventTrace(fixture)[0]);
    }

    public static void EqualTickEventsExecuteByPriorityThenCreationSequence()
    {
        SimulationFixture fixture = CreateFixture();
        fixture.Scheduler.ScheduleAt(new SimTick(1), SimPhase.PreSimulation, 20, TraceData("A"));
        fixture.Scheduler.ScheduleAt(new SimTick(1), SimPhase.PreSimulation, 10, TraceData("B"));
        fixture.Scheduler.ScheduleAt(new SimTick(1), SimPhase.PreSimulation, 10, TraceData("C"));

        fixture.Runner.RunOneTick();

        IReadOnlyList<string> trace = EventTrace(fixture);
        TestAssert.Equal(3, trace.Count);
        TestAssert.Equal("event:B@1:PreSimulation", trace[0]);
        TestAssert.Equal("event:C@1:PreSimulation", trace[1]);
        TestAssert.Equal("event:A@1:PreSimulation", trace[2]);
    }

    public static void SnapshotArrayOrderDoesNotAffectExecutionOrder()
    {
        ScheduledEventSnapshot first = SnapshotEvent(10, 1, "A");
        ScheduledEventSnapshot second = SnapshotEvent(20, 2, "B");
        ScheduledEventSnapshot third = SnapshotEvent(30, 3, "C");

        var snapshotA = new SchedulerSnapshot(
            CurrentTick: 0,
            NextEventId: 31,
            NextCreationSequence: 4,
            Events: new[] { third, first, second });
        var snapshotB = new SchedulerSnapshot(
            CurrentTick: 0,
            NextEventId: 31,
            NextCreationSequence: 4,
            Events: new[] { second, third, first });

        SimulationFixture fixtureA = CreateFixture();
        SimulationFixture fixtureB = CreateFixture();
        fixtureA.Scheduler.Restore(snapshotA);
        fixtureB.Scheduler.Restore(snapshotB);

        fixtureA.Runner.RunOneTick();
        fixtureB.Runner.RunOneTick();

        TestAssert.Equal(
            string.Join("|", EventTrace(fixtureA)),
            string.Join("|", EventTrace(fixtureB)));
        TestAssert.Equal(
            "event:A@1:PreSimulation|event:B@1:PreSimulation|event:C@1:PreSimulation",
            string.Join("|", EventTrace(fixtureA)));
    }

    public static void CancellationByIdAndOwnerPreventsExecution()
    {
        SimulationFixture fixture = CreateFixture();
        var ownerSeven = new ScheduledEventOwnerId(7);
        var ownerEight = new ScheduledEventOwnerId(8);

        ScheduledEventId first = fixture.Scheduler.ScheduleAt(
            new SimTick(1), SimPhase.Narrative, 0, TraceData("owner-seven-one", ownerSeven));
        fixture.Scheduler.ScheduleAt(
            new SimTick(1), SimPhase.Narrative, 0, TraceData("owner-seven-two", ownerSeven));
        fixture.Scheduler.ScheduleAt(
            new SimTick(1), SimPhase.Narrative, 0, TraceData("owner-eight", ownerEight));
        fixture.Scheduler.ScheduleAt(
            new SimTick(1), SimPhase.Narrative, 0, TraceData("unowned"));

        TestAssert.True(fixture.Scheduler.Cancel(first));
        TestAssert.True(!fixture.Scheduler.Cancel(first));
        TestAssert.Equal(1, fixture.Scheduler.CancelOwnedBy(ownerSeven));
        TestAssert.Equal(2, fixture.Scheduler.PendingCount);

        fixture.Runner.RunOneTick();
        TestAssert.Equal(
            "event:owner-eight@1:Narrative|event:unowned@1:Narrative",
            string.Join("|", EventTrace(fixture)));
    }

    public static void RepeatingEventsRemainAlignedToOriginalCadence()
    {
        SimulationFixture fixture = CreateFixture();
        fixture.Scheduler.ScheduleRepeating(
            firstTick: new SimTick(2),
            intervalTicks: 3,
            phase: SimPhase.Narrative,
            priority: 0,
            eventData: TraceData("repeat"));

        fixture.Runner.RunTicks(11);

        TestAssert.Equal(
            "event:repeat@2:Narrative|event:repeat@5:Narrative|event:repeat@8:Narrative|event:repeat@11:Narrative",
            string.Join("|", EventTrace(fixture)));

        SchedulerSnapshot snapshot = fixture.Scheduler.CaptureSnapshot();
        TestAssert.Equal(1, snapshot.Events.Length);
        TestAssert.Equal(14L, snapshot.Events[0].DueTick);
        TestAssert.Equal<long?>(3L, snapshot.Events[0].RepeatIntervalTicks);
    }

    public static void JsonSnapshotRestorePreservesPendingOrderAndFutureState()
    {
        SimulationFixture uninterrupted = CreateFixture();
        uninterrupted.Scheduler.ScheduleAt(
            new SimTick(2), SimPhase.AgentState, 10, TraceData("second-B"));
        uninterrupted.Scheduler.ScheduleAt(
            new SimTick(2), SimPhase.AgentState, 5, TraceData("second-A"));
        uninterrupted.Scheduler.ScheduleRepeating(
            new SimTick(3), 2, SimPhase.Narrative, 0, TraceData("repeat"));
        uninterrupted.Runner.RunOneTick();

        string json = SchedulerSnapshotJson.Serialize(
            uninterrupted.Scheduler.CaptureSnapshot());

        SimulationFixture restored = CreateFixture();
        restored.Runner.RunOneTick();
        restored.Scheduler.Restore(SchedulerSnapshotJson.Deserialize(json));

        uninterrupted.Runner.RunTicks(7);
        restored.Runner.RunTicks(7);

        TestAssert.Equal(
            string.Join("|", EventTrace(uninterrupted)),
            string.Join("|", EventTrace(restored)));
        TestAssert.Equal(
            StateChecksum.Compute(
                uninterrupted.Clock.CurrentTick,
                uninterrupted.World,
                uninterrupted.Scheduler),
            StateChecksum.Compute(
                restored.Clock.CurrentTick,
                restored.World,
                restored.Scheduler));
    }

    public static void SamePhaseSchedulingDefersToTheNextTick()
    {
        var schedulingSystem = new ScheduleDuringPhaseSystem(
            "schedule.same-phase",
            SimPhase.AgentState,
            order: 0,
            SimPhase.AgentState,
            "same-phase");
        SimulationFixture fixture = CreateFixture(new[] { schedulingSystem });

        fixture.Runner.RunOneTick();
        TestAssert.Equal(0, EventTrace(fixture).Count);

        fixture.Runner.RunOneTick();
        TestAssert.Equal(
            "event:same-phase@2:AgentState",
            string.Join("|", EventTrace(fixture)));
    }

    public static void LaterPhaseSchedulingCanExecuteInTheCurrentTick()
    {
        var schedulingSystem = new ScheduleDuringPhaseSystem(
            "schedule.later-phase",
            SimPhase.AgentState,
            order: 0,
            SimPhase.Narrative,
            "later-phase");
        SimulationFixture fixture = CreateFixture(new[] { schedulingSystem });

        fixture.Runner.RunOneTick();

        TestAssert.Equal(
            "event:later-phase@1:Narrative",
            string.Join("|", EventTrace(fixture)));
    }

    public static void SchedulerStateParticipatesInCanonicalChecksums()
    {
        SimulationFixture withoutEvent = CreateFixture();
        SimulationFixture withEvent = CreateFixture();
        withEvent.Scheduler.ScheduleAt(
            new SimTick(10), SimPhase.Narrative, 0, TraceData("pending"));

        ulong worldOnlyA = StateChecksum.Compute(
            withoutEvent.Clock.CurrentTick,
            withoutEvent.World);
        ulong worldOnlyB = StateChecksum.Compute(
            withEvent.Clock.CurrentTick,
            withEvent.World);
        TestAssert.Equal(worldOnlyA, worldOnlyB);

        ulong completeA = StateChecksum.Compute(
            withoutEvent.Clock.CurrentTick,
            withoutEvent.World,
            withoutEvent.Scheduler);
        ulong completeB = StateChecksum.Compute(
            withEvent.Clock.CurrentTick,
            withEvent.World,
            withEvent.Scheduler);
        TestAssert.True(completeA != completeB);
    }

    public static void PastDueEventsAndNonPositiveRepeatsAreRejected()
    {
        SimulationFixture fixture = CreateFixture();
        fixture.Runner.RunTicks(2);

        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => fixture.Scheduler.ScheduleAt(
                new SimTick(1), SimPhase.Narrative, 0, TraceData("past")));
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => fixture.Scheduler.ScheduleRepeating(
                new SimTick(3), 0, SimPhase.Narrative, 0, TraceData("invalid-repeat")));
    }

    private static SimulationFixture CreateFixture(
        IEnumerable<ISimSystem>? systems = null)
        => new(
            systems ?? Array.Empty<ISimSystem>(),
            new IScheduledEventHandler[] { new TraceScheduledEventHandler() });

    private static ScheduledEventData TraceData(
        string payload,
        ScheduledEventOwnerId? owner = null)
        => new(TraceType, owner, payload);

    private static IReadOnlyList<string> EventTrace(SimulationFixture fixture)
        => fixture.World.Trace
            .Where(static item => item.StartsWith("event:", StringComparison.Ordinal))
            .ToArray();

    private static ScheduledEventSnapshot SnapshotEvent(
        ulong id,
        ulong creationSequence,
        string payload)
        => new(
            Id: id,
            DueTick: 1,
            Phase: (byte)SimPhase.PreSimulation,
            Priority: 5,
            CreationSequence: creationSequence,
            Type: TraceType.Value,
            Owner: null,
            Payload: payload,
            RepeatIntervalTicks: null);
}
