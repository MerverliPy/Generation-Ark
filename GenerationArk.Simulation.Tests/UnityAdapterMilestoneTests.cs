using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.UnityAdapter;

namespace GenerationArk.Simulation.Tests;

public static class UnityAdapterMilestoneTests
{
    public static void FrameAccumulatorRetainsFractionalTicks()
    {
        int ticks = 0;
        var adapter = new UnitySimulationAdapter(() => ticks++, maxTicksPerFrame: 100);

        FrameAdvanceResult first = adapter.AdvanceFrame(1.0 / 60.0);
        Equal(0, first.TickPump.TicksExecuted, "Half a tick must not execute early.");
        Near(0.5, first.TickPump.FractionalTicks, "Fractional tick was not retained.");

        FrameAdvanceResult second = adapter.AdvanceFrame(1.0 / 60.0);
        Equal(1, second.TickPump.TicksExecuted, "Two half frames must execute one tick.");
        Equal(1, ticks, "Runner call count mismatch.");
        Near(0.0, second.TickPump.AccumulatedTicks, "Accumulator should return to zero.");
    }

    public static void PerFrameBudgetRetainsBacklog()
    {
        int ticks = 0;
        var adapter = new UnitySimulationAdapter(() => ticks++, maxTicksPerFrame: 7);
        adapter.SetSpeedMultiplier(SimulationSpeedProfile.Fast);

        FrameAdvanceResult first = adapter.AdvanceFrame(1.0);
        Equal(7, first.TickPump.TicksExecuted, "The per-frame budget was not enforced.");
        Equal(113L, first.TickPump.WholeTicksBacklogged, "Requested ticks were dropped.");
        True(first.TickPump.BudgetLimited, "Budget limiting was not reported.");

        while (adapter.WholeTicksBacklogged > 0)
        {
            adapter.AdvanceFrame(0.0);
        }

        Equal(120, ticks, "Backlogged ticks did not eventually execute.");
    }

    public static void PausedFramesRetainExistingBacklog()
    {
        int ticks = 0;
        var adapter = new UnitySimulationAdapter(() => ticks++, maxTicksPerFrame: 2);

        adapter.AdvanceFrame(1.0);
        Equal(28L, adapter.WholeTicksBacklogged, "Test setup did not create the expected backlog.");

        adapter.Pause();
        FrameAdvanceResult paused = adapter.AdvanceFrame(5.0);
        Equal(0, paused.TickPump.TicksExecuted, "Paused frame advanced simulation.");
        Equal(28L, adapter.WholeTicksBacklogged, "Pause discarded retained authoritative work.");

        adapter.Resume();
        adapter.AdvanceFrame(0.0);
        Equal(4, ticks, "Backlog did not resume under the existing frame budget.");
        Equal(26L, adapter.WholeTicksBacklogged, "Backlog accounting changed while paused.");
    }

    public static void ManualStepAdvancesExactlyOneTickWhilePaused()
    {
        int ticks = 0;
        var adapter = new UnitySimulationAdapter(() => ticks++, maxTicksPerFrame: 10);

        Throws<InvalidOperationException>(adapter.StepOneTick);

        adapter.Pause();
        adapter.StepOneTick();
        Equal(1, ticks, "Manual step did not execute exactly one tick.");
        Near(0.0, adapter.AccumulatedTicks, "Manual step modified frame accumulation state.");

        adapter.AdvanceFrame(10.0);
        Equal(1, ticks, "Paused frame advanced after manual step.");
    }

    public static void SpeedControlsUseDocumentedMultipliers()
    {
        int[] supported = { 0, 1, 4, 16, 64, 256 };
        foreach (int multiplier in supported)
        {
            True(
                SimulationSpeedProfile.IsSupported(multiplier),
                $"Documented multiplier {multiplier} was rejected.");
        }

        int[] rejected = { -1, 2, 8, 32, 128, 257 };
        foreach (int multiplier in rejected)
        {
            True(
                !SimulationSpeedProfile.IsSupported(multiplier),
                $"Undocumented multiplier {multiplier} was accepted.");
        }

        int ticks = 0;
        var adapter = new UnitySimulationAdapter(() => ticks++, maxTicksPerFrame: 10_000);
        adapter.SetSpeedMultiplier(SimulationSpeedProfile.Chronicle);
        Equal(64, adapter.EffectiveSpeedMultiplier, "Chronicle speed was not selected.");
        adapter.SetSpeedMultiplier(SimulationSpeedProfile.Paused);
        True(adapter.IsPaused, "Paused speed did not pause the adapter.");
        Equal(0, adapter.EffectiveSpeedMultiplier, "Paused effective multiplier must be zero.");
    }

    public static void FastForwardThrottlingDoesNotChangeTickExecution()
    {
        int ticks = 0;
        int presentations = 0;
        var adapter = new UnitySimulationAdapter(() => ticks++, maxTicksPerFrame: 20_000);
        adapter.SetSpeedMultiplier(SimulationSpeedProfile.DeepFastForward);

        for (int frame = 0; frame < 60; frame++)
        {
            FrameAdvanceResult result = adapter.AdvanceFrame(1.0 / 60.0);
            if (result.ShouldPresent)
            {
                presentations++;
            }
        }

        Equal(7_680, ticks, "Presentation throttling changed authoritative tick execution.");
        True(presentations > 0, "Fast-forward suppressed all presentation refreshes.");
        True(presentations < 60, "Deep fast-forward did not throttle presentation.");
    }

    public static void InvalidFrameInputsAreRejected()
    {
        var adapter = new UnitySimulationAdapter(() => { }, maxTicksPerFrame: 10);

        Throws<ArgumentOutOfRangeException>(() => adapter.AdvanceFrame(-0.001));
        Throws<ArgumentOutOfRangeException>(() => adapter.AdvanceFrame(double.NaN));
        Throws<ArgumentOutOfRangeException>(() => adapter.SetSpeedMultiplier(3));
        Throws<ArgumentOutOfRangeException>(
            () => _ = new UnitySimulationAdapter(() => { }, maxTicksPerFrame: 0));
    }

    public static void FramePatternsProduceIdenticalAdapterTickCounts()
    {
        int stableTicks = RunPattern(Enumerable.Repeat(1.0 / 60.0, 120));
        int irregularTicks = RunPattern(new[]
        {
            0.10,
            0.03,
            0.07,
            0.20,
            0.40,
            0.50,
            0.70
        });

        Equal(60, stableTicks, "Stable frame pattern produced the wrong tick count.");
        Equal(stableTicks, irregularTicks, "Frame pattern changed authoritative tick count.");
    }

    private static int RunPattern(IEnumerable<double> frameDurations)
    {
        int ticks = 0;
        var adapter = new UnitySimulationAdapter(() => ticks++, maxTicksPerFrame: 10_000);

        foreach (double frameDuration in frameDurations)
        {
            adapter.AdvanceFrame(frameDuration);
        }

        while (adapter.WholeTicksBacklogged > 0)
        {
            adapter.AdvanceFrame(0.0);
        }

        return ticks;
    }

    private static void Equal<T>(T expected, T actual, string message)
        where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
        {
            throw new InvalidOperationException(
                $"{message} Expected: {expected}. Actual: {actual}.");
        }
    }

    private static void Near(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 1e-8)
        {
            throw new InvalidOperationException(
                $"{message} Expected: {expected:R}. Actual: {actual:R}.");
        }
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected exception {typeof(TException).Name} was not thrown.");
    }
}
