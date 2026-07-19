using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Scheduling;

namespace GenerationArk.Simulation.Tests;

internal static class DiagnosticsMilestoneTests
{
    private static readonly ScheduledEventTypeId TraceType = new("test.trace");

    public static void CanonicalChecksumIsRepeatableForIdenticalState()
    {
        SimulationFixture first = CreateTracedFixture(capacity: 4);
        SimulationFixture second = CreateTracedFixture(capacity: 4);
        ScheduleAndCommand(first);
        ScheduleAndCommand(second);

        first.Runner.RunTicks(3);
        second.Runner.RunTicks(3);

        TickChecksum firstChecksum = StateChecksum.ComputeDetailed(
            first.Clock.CurrentTick,
            first.World,
            first.Scheduler,
            first.Random);
        TickChecksum secondChecksum = StateChecksum.ComputeDetailed(
            second.Clock.CurrentTick,
            second.World,
            second.Scheduler,
            second.Random);

        TestAssert.Equal(firstChecksum.GlobalChecksum, secondChecksum.GlobalChecksum);
        TestAssert.Equal(
            FormatComponents(firstChecksum.Components),
            FormatComponents(secondChecksum.Components));
        TestAssert.Equal(ChecksumFormatVersion.Current, firstChecksum.FormatVersion);
        TestAssert.Equal(
            FormatComponents(first.Checksums.DetailedAt(new SimTick(3)).Components),
            FormatComponents(second.Checksums.DetailedAt(new SimTick(3)).Components));
    }

    public static void ComponentRegistrationOrderDoesNotAffectChecksums()
    {
        IStateChecksumContributor alpha = new TestChecksumContributor("alpha", 10);
        IStateChecksumContributor beta = new TestChecksumContributor("beta", 20);

        IReadOnlyList<ComponentChecksum> first =
            StateChecksum.ComputeComponentChecksums(new[] { alpha, beta });
        IReadOnlyList<ComponentChecksum> second =
            StateChecksum.ComputeComponentChecksums(new[] { beta, alpha });

        TestAssert.Equal(FormatComponents(first), FormatComponents(second));
        TestAssert.Equal("alpha|beta", string.Join("|", first.Select(
            static item => item.ComponentId.Value)));
    }

    public static void DuplicateComponentIdsFailValidation()
    {
        IStateChecksumContributor one = new TestChecksumContributor("duplicate", 1);
        IStateChecksumContributor two = new TestChecksumContributor("duplicate", 2);

        TestAssert.Throws<InvalidOperationException>(
            () => StateChecksum.ComputeComponentChecksums(new[] { one, two }));
    }

    public static void DiagnosticTracingDoesNotAlterAuthoritativeOutcomes()
    {
        var withoutTrace = new SimulationFixture(new ISimSystem[]
        {
            new IncrementSystem("increment", SimPhase.AgentState, 0, "value", 3),
            new RandomDrawingSystem()
        });
        var trace = new SimulationTrace(capacity: 16);
        var withTrace = new SimulationFixture(
            new ISimSystem[]
            {
                new IncrementSystem("increment", SimPhase.AgentState, 0, "value", 3),
                new RandomDrawingSystem()
            },
            trace: trace);

        withoutTrace.Runner.RunTicks(10);
        withTrace.Runner.RunTicks(10);

        TestAssert.Equal(
            withoutTrace.Checksums.At(new SimTick(10)),
            withTrace.Checksums.At(new SimTick(10)));
        TestAssert.Equal(
            withoutTrace.World.GetCounter("random-low"),
            withTrace.World.GetCounter("random-low"));
        TestAssert.Equal(10, trace.Entries.Count);
        TestAssert.Equal(1, trace.Entries[^1].RandomRequests.Count);
    }

    public static void TickTracePreservesStableExecutionOrder()
    {
        SimulationTrace trace = new(capacity: 4);
        var fixture = new SimulationFixture(
            new ISimSystem[]
            {
                new IncrementSystem("second", SimPhase.AgentState, 20, "value", 2),
                new IncrementSystem("first", SimPhase.AgentState, 10, "value", 1),
                new RandomDrawingSystem()
            },
            new IScheduledEventHandler[] { new TraceScheduledEventHandler() },
            trace: trace);
        fixture.Commands.Enqueue(
            new AddCounterCommand(
                new CommandId(1),
                new SimTick(1),
                sequence: 1,
                counterKey: "value",
                delta: 5),
            fixture.Clock.CurrentTick);
        fixture.Scheduler.ScheduleAt(
            new SimTick(1),
            SimPhase.PreSimulation,
            priority: 0,
            eventData: new ScheduledEventData(TraceType, owner: null, payload: "event"));

        fixture.Runner.RunOneTick();

        SimulationTraceEntry entry = trace.Entries[0];
        TestAssert.Equal(new SimTick(1), entry.Tick);
        TestAssert.Equal("first|second|test.random-draw", string.Join("|", entry.Systems.Select(
            static item => item.SystemId.Value)));
        TestAssert.Equal(1, entry.Commands.Count);
        TestAssert.Equal(new CommandId(1), entry.Commands[0].CommandId);
        TestAssert.Equal(1, entry.ScheduledEvents.Count);
        TestAssert.Equal("event", entry.ScheduledEvents[0].Payload);
        TestAssert.Equal(1, entry.RandomRequests.Count);
        TestAssert.True(entry.Checksum is not null);
        TestAssert.True(entry.Failure is null);
    }

    public static void TraceRetentionIsBoundedWithoutChangingChecksums()
    {
        SimulationTrace trace = new(capacity: 2);
        var traced = new SimulationFixture(
            new ISimSystem[] { new RandomDrawingSystem() },
            trace: trace);
        var baseline = new SimulationFixture(
            new ISimSystem[] { new RandomDrawingSystem() });

        traced.Runner.RunTicks(5);
        baseline.Runner.RunTicks(5);

        TestAssert.Equal(2, trace.Entries.Count);
        TestAssert.Equal(new SimTick(4), trace.Entries[0].Tick);
        TestAssert.Equal(new SimTick(5), trace.Entries[1].Tick);
        TestAssert.Equal(
            baseline.Checksums.At(new SimTick(5)),
            traced.Checksums.At(new SimTick(5)));
    }

    public static void DesyncDetectionFindsTheFirstDivergentTick()
    {
        TickChecksum[] expected =
        {
            Checkpoint(10, 100, ("world", 1UL)),
            Checkpoint(20, 200, ("world", 2UL)),
            Checkpoint(30, 300, ("world", 3UL))
        };
        TickChecksum[] actual =
        {
            Checkpoint(10, 100, ("world", 1UL)),
            Checkpoint(20, 999, ("world", 9UL)),
            Checkpoint(30, 300, ("world", 3UL))
        };

        DesyncReport? report = DesyncDetector.FindFirstDivergence(expected, actual);

        TestAssert.True(report is not null);
        TestAssert.Equal(new SimTick(20), report!.FirstDivergentTick);
        TestAssert.Equal<ulong?>(200UL, report.Expected?.GlobalChecksum);
        TestAssert.Equal<ulong?>(999UL, report.Actual?.GlobalChecksum);
    }

    public static void DesyncReportIdentifiesChangedAndMissingComponents()
    {
        TickChecksum expected = Checkpoint(
            50,
            1000,
            ("clock", 10UL),
            ("random", 20UL),
            ("world", 30UL));
        TickChecksum actual = Checkpoint(
            50,
            2000,
            ("clock", 10UL),
            ("scheduler", 40UL),
            ("world", 31UL));

        DesyncReport? report = DesyncDetector.FindFirstDivergence(
            new[] { expected },
            new[] { actual });

        TestAssert.True(report is not null);
        TestAssert.Equal(
            "random|scheduler|world",
            string.Join("|", report!.ComponentDifferences.Select(
                static item => item.ComponentId.Value)));
        TestAssert.Equal<ulong?>(20UL, report.ComponentDifferences[0].ExpectedChecksum);
        TestAssert.Equal<ulong?>(null, report.ComponentDifferences[0].ActualChecksum);
        TestAssert.Equal<ulong?>(null, report.ComponentDifferences[1].ExpectedChecksum);
        TestAssert.Equal<ulong?>(40UL, report.ComponentDifferences[1].ActualChecksum);
        TestAssert.Equal<ulong?>(30UL, report.ComponentDifferences[2].ExpectedChecksum);
        TestAssert.Equal<ulong?>(31UL, report.ComponentDifferences[2].ActualChecksum);
    }

    private static SimulationFixture CreateTracedFixture(int capacity)
        => new(
            new ISimSystem[]
            {
                new IncrementSystem("increment", SimPhase.AgentState, 0, "value", 2),
                new RandomDrawingSystem()
            },
            new IScheduledEventHandler[] { new TraceScheduledEventHandler() },
            trace: new SimulationTrace(capacity));

    private static void ScheduleAndCommand(SimulationFixture fixture)
    {
        fixture.Commands.Enqueue(
            new AddCounterCommand(
                new CommandId(1),
                new SimTick(2),
                sequence: 1,
                counterKey: "value",
                delta: 10),
            fixture.Clock.CurrentTick);
        fixture.Scheduler.ScheduleAt(
            new SimTick(3),
            SimPhase.Narrative,
            priority: 0,
            eventData: new ScheduledEventData(TraceType, owner: null, payload: "event"));
    }

    private static string FormatComponents(IEnumerable<ComponentChecksum> components)
        => string.Join(
            "|",
            components.Select(static item =>
                $"{item.ComponentId.Value}:{item.Checksum}"));

    private static TickChecksum Checkpoint(
        long tick,
        ulong globalChecksum,
        params (string Id, ulong Checksum)[] components)
        => new(
            new SimTick(tick),
            ChecksumFormatVersion.Current,
            globalChecksum,
            components.Select(static item => new ComponentChecksum(
                new ChecksumComponentId(item.Id),
                item.Checksum)));

    private sealed class TestChecksumContributor : IStateChecksumContributor
    {
        private readonly long _value;

        public TestChecksumContributor(string id, long value)
        {
            ComponentId = new ChecksumComponentId(id);
            _value = value;
        }

        public ChecksumComponentId ComponentId { get; }

        public void Write(StateChecksumWriter writer) => writer.AddInt64(_value);
    }
}
