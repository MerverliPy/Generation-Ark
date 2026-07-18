using System;

namespace GenerationArk.Simulation.Core;

/// <summary>
/// Engine-independent frame accumulator. A Unity MonoBehaviour can delegate its
/// unscaled frame delta to this type without allowing rendering time into gameplay logic.
/// </summary>
public sealed class SimulationFramePump
{
    public const double BaseTicksPerSecond = 30.0;

    private readonly SimulationClock _clock;
    private readonly ISimulationRunner _runner;
    private readonly int _maxTicksPerFrame;
    private double _tickAccumulator;

    public SimulationFramePump(
        SimulationClock clock,
        ISimulationRunner runner,
        int maxTicksPerFrame = 4096)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));

        if (maxTicksPerFrame <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTicksPerFrame));
        }

        _maxTicksPerFrame = maxTicksPerFrame;
    }

    public double PendingTickFraction => _tickAccumulator;

    public int AdvanceFrame(double unscaledDeltaSeconds, int? tickLimit = null)
    {
        if (double.IsNaN(unscaledDeltaSeconds) ||
            double.IsInfinity(unscaledDeltaSeconds) ||
            unscaledDeltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
        }

        int limit = tickLimit.HasValue
            ? Math.Min(_maxTicksPerFrame, Math.Max(0, tickLimit.Value))
            : _maxTicksPerFrame;

        if (_clock.IsPaused)
        {
            _tickAccumulator = 0;
            if (_clock.ConsumeSingleStepRequest() && limit > 0)
            {
                _runner.RunOneTick();
                return 1;
            }

            return 0;
        }

        _tickAccumulator += unscaledDeltaSeconds
            * BaseTicksPerSecond
            * (int)_clock.RequestedSpeed;

        int ticksToRun = Math.Min((int)Math.Floor(_tickAccumulator), limit);
        for (int i = 0; i < ticksToRun; i++)
        {
            _runner.RunOneTick();
        }

        _tickAccumulator -= ticksToRun;
        return ticksToRun;
    }
}
