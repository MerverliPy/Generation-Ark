using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;

namespace GenerationArk.Simulation.Tests;

internal static class ClockMilestoneTests
{
    public static void SimTickRelationalOperatorsCompareUnderlyingValues()
    {
        var earlier = new SimTick(10);
        var later = new SimTick(20);
        var equalToEarlier = new SimTick(10);

        TestAssert.True(earlier < later);
        TestAssert.True(later > earlier);
        TestAssert.True(earlier <= equalToEarlier);
        TestAssert.True(equalToEarlier >= earlier);
    }
    public static void PauseAndSingleStepAdvanceExactlyOneTick()
    {
        var fixture = new SimulationFixture(new[]
        {
            new IncrementSystem("test.increment", SimPhase.AgentState, 0, "ticks", 1)
        });

        fixture.Clock.Pause();
        TestAssert.Equal(0, fixture.Pump.AdvanceFrame(10.0));
        TestAssert.Equal(SimTick.Zero, fixture.Clock.CurrentTick);

        fixture.Clock.RequestSingleStep();
        TestAssert.Equal(1, fixture.Pump.AdvanceFrame(0.0));
        TestAssert.Equal(new SimTick(1), fixture.Clock.CurrentTick);
        TestAssert.Equal(1L, fixture.World.GetCounter("ticks"));

        TestAssert.Equal(0, fixture.Pump.AdvanceFrame(10.0));
        TestAssert.Equal(new SimTick(1), fixture.Clock.CurrentTick);
    }

    public static void FramePatternsProduceIdenticalState()
    {
        const int targetTicks = 20_000;
        ulong checksumA = RunWithFramePattern(
            targetTicks,
            new[] { 1.0 / 60.0 });
        ulong checksumB = RunWithFramePattern(
            targetTicks,
            new[] { 1.0 / 144.0, 1.0 / 24.0, 0.001, 0.075, 1.0 / 90.0 });
        ulong checksumC = RunWithFramePattern(
            targetTicks,
            new[] { 0.5, 0.0, 0.002, 1.0 / 30.0, 0.25 });

        TestAssert.Equal(checksumA, checksumB);
        TestAssert.Equal(checksumA, checksumC);
    }

    public static void RegistrationOrderDoesNotAffectExecutionOrder()
    {
        ISimSystem first = new OrderedMathSystem("math.first", 10, multiplier: 1, addition: 2);
        ISimSystem second = new OrderedMathSystem("math.second", 20, multiplier: 3, addition: 1);

        var fixtureA = new SimulationFixture(new[] { first, second });
        var fixtureB = new SimulationFixture(new[] { second, first });

        fixtureA.Runner.RunTicks(100);
        fixtureB.Runner.RunTicks(100);

        TestAssert.Equal(
            StateChecksum.Compute(fixtureA.Clock.CurrentTick, fixtureA.World),
            StateChecksum.Compute(fixtureB.Clock.CurrentTick, fixtureB.World));
        TestAssert.Equal(fixtureA.World.GetCounter("ordered"), fixtureB.World.GetCounter("ordered"));
    }

    public static void DuplicateSystemIdFailsStartupValidation()
    {
        ISimSystem one = new IncrementSystem("duplicate", SimPhase.AgentState, 0, "a", 1);
        ISimSystem two = new IncrementSystem("duplicate", SimPhase.Narrative, 0, "b", 1);

        TestAssert.Throws<InvalidOperationException>(
            () => _ = new SimSystemRegistry(new[] { one, two }));
    }

    public static void CommandsApplyByTickThenSequenceThenId()
    {
        var fixture = new SimulationFixture(Array.Empty<ISimSystem>());
        fixture.Commands.Enqueue(
            new AddCounterCommand(new CommandId(30), new SimTick(5), 30, "value", 100),
            fixture.Clock.CurrentTick);
        fixture.Commands.Enqueue(
            new AddCounterCommand(new CommandId(20), new SimTick(5), 20, "value", 10),
            fixture.Clock.CurrentTick);
        fixture.Commands.Enqueue(
            new AddCounterCommand(new CommandId(10), new SimTick(5), 10, "value", 1),
            fixture.Clock.CurrentTick);

        fixture.Runner.RunTicks(5);

        TestAssert.Equal(111L, fixture.World.GetCounter("value"));
        TestAssert.Equal(3, fixture.Commands.Results.Count);
        TestAssert.Equal(new CommandId(10), fixture.Commands.Results[0].CommandId);
        TestAssert.Equal(new CommandId(20), fixture.Commands.Results[1].CommandId);
        TestAssert.Equal(new CommandId(30), fixture.Commands.Results[2].CommandId);
    }

    public static void SpeedChangesDoNotChangeTickOutcomes()
    {
        const int targetTicks = 5_000;

        var baseline = CreateStandardFixture();
        baseline.Clock.SetSpeed(SimulationSpeed.Normal);
        RunToTick(baseline, targetTicks, new[] { 1.0 / 60.0 });

        var changed = CreateStandardFixture();
        SimulationSpeed[] speeds =
        {
            SimulationSpeed.Normal,
            SimulationSpeed.Fast,
            SimulationSpeed.VeryFast,
            SimulationSpeed.Chronicle,
            SimulationSpeed.DeepFastForward
        };
        double[] frames = { 1.0 / 144.0, 1.0 / 30.0, 0.1, 0.005 };

        int frame = 0;
        while (changed.Clock.CurrentTick.Value < targetTicks)
        {
            changed.Clock.SetSpeed(speeds[frame % speeds.Length]);
            int remaining = checked((int)(targetTicks - changed.Clock.CurrentTick.Value));
            changed.Pump.AdvanceFrame(frames[frame % frames.Length], remaining);
            frame++;
        }

        TestAssert.Equal(
            StateChecksum.Compute(baseline.Clock.CurrentTick, baseline.World),
            StateChecksum.Compute(changed.Clock.CurrentTick, changed.World));
    }

    public static void TickBudgetRetainsBacklogInsteadOfDroppingTicks()
    {
        var fixture = new SimulationFixture(Array.Empty<ISimSystem>());
        fixture.Clock.SetSpeed(SimulationSpeed.DeepFastForward);

        int firstFrame = fixture.Pump.AdvanceFrame(1.0, tickLimit: 10);
        TestAssert.Equal(10, firstFrame);
        TestAssert.True(fixture.Pump.PendingTickFraction > 7_000.0);

        int secondFrame = fixture.Pump.AdvanceFrame(0.0, tickLimit: 10);
        TestAssert.Equal(10, secondFrame);
        TestAssert.Equal(new SimTick(20), fixture.Clock.CurrentTick);
    }

    private static ulong RunWithFramePattern(int targetTicks, IReadOnlyList<double> pattern)
    {
        SimulationFixture fixture = CreateStandardFixture();
        fixture.Clock.SetSpeed(SimulationSpeed.VeryFast);
        EnqueueStandardCommands(fixture);
        RunToTick(fixture, targetTicks, pattern);
        return StateChecksum.Compute(fixture.Clock.CurrentTick, fixture.World);
    }

    private static SimulationFixture CreateStandardFixture()
        => new(new ISimSystem[]
        {
            new IncrementSystem("alpha", SimPhase.PreSimulation, 20, "alpha", 3),
            new IncrementSystem("beta", SimPhase.AgentState, 10, "beta", 7),
            new OrderedMathSystem("math.a", 30, multiplier: 1, addition: 2),
            new OrderedMathSystem("math.b", 40, multiplier: 1, addition: 5)
        });

    private static void EnqueueStandardCommands(SimulationFixture fixture)
    {
        fixture.Commands.Enqueue(
            new AddCounterCommand(new CommandId(1), new SimTick(10), 1, "alpha", 50),
            fixture.Clock.CurrentTick);
        fixture.Commands.Enqueue(
            new AddCounterCommand(new CommandId(2), new SimTick(999), 2, "beta", -100),
            fixture.Clock.CurrentTick);
        fixture.Commands.Enqueue(
            new AddCounterCommand(new CommandId(3), new SimTick(10_000), 3, "ordered", 12),
            fixture.Clock.CurrentTick);
    }

    private static void RunToTick(
        SimulationFixture fixture,
        int targetTicks,
        IReadOnlyList<double> pattern)
    {
        int frame = 0;
        while (fixture.Clock.CurrentTick.Value < targetTicks)
        {
            int remaining = checked((int)(targetTicks - fixture.Clock.CurrentTick.Value));
            fixture.Pump.AdvanceFrame(pattern[frame % pattern.Count], remaining);
            frame++;

            if (frame > 10_000_000)
            {
                throw new InvalidOperationException("Frame pattern did not advance the simulation.");
            }
        }
    }
}
