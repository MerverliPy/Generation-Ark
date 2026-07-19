using System;

namespace GenerationArk.Simulation.UnityAdapter;

/// <summary>
/// Converts presentation-frame duration into deterministic calls to RunOneTick.
/// The accumulated frame time is non-authoritative and is deliberately excluded from save state.
/// </summary>
public sealed class UnityFrameAccumulator
{
    public const double DefaultBaseTicksPerSecond = 30.0;

    // Compensates only for representational noise around exact integer tick boundaries.
    private const double TickBoundaryEpsilon = 1e-9;

    private readonly double _baseTicksPerSecond;
    private readonly int _maxTicksPerFrame;
    private double _accumulatedTicks;

    public UnityFrameAccumulator(
        int maxTicksPerFrame,
        double baseTicksPerSecond = DefaultBaseTicksPerSecond)
    {
        if (maxTicksPerFrame <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTicksPerFrame),
                maxTicksPerFrame,
                "The per-frame tick budget must be positive.");
        }

        if (!IsFinite(baseTicksPerSecond) || baseTicksPerSecond <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseTicksPerSecond),
                baseTicksPerSecond,
                "Base ticks per second must be finite and positive.");
        }

        _maxTicksPerFrame = maxTicksPerFrame;
        _baseTicksPerSecond = baseTicksPerSecond;
    }

    public int MaxTicksPerFrame => _maxTicksPerFrame;

    public double BaseTicksPerSecond => _baseTicksPerSecond;

    public double AccumulatedTicks => _accumulatedTicks;

    public long WholeTicksBacklogged => GetWholeTicks(_accumulatedTicks);

    public double FractionalTicks
    {
        get
        {
            long wholeTicks = GetWholeTicks(_accumulatedTicks);
            double fraction = _accumulatedTicks - wholeTicks;
            return ClampRepresentationalNoise(fraction);
        }
    }

    /// <summary>
    /// Advances one presentation frame. Paused frames do not add time and do not discard backlog.
    /// If RunOneTick throws, only successfully completed ticks are removed from the accumulator.
    /// </summary>
    public TickPumpResult AdvanceFrame(
        double unscaledDeltaSeconds,
        int speedMultiplier,
        bool isPaused,
        Action runOneTick)
    {
        if (runOneTick is null)
        {
            throw new ArgumentNullException(nameof(runOneTick));
        }

        if (!IsFinite(unscaledDeltaSeconds) || unscaledDeltaSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unscaledDeltaSeconds),
                unscaledDeltaSeconds,
                "Frame duration must be finite and non-negative.");
        }

        SimulationSpeedProfile.ValidateSupported(speedMultiplier, nameof(speedMultiplier));

        if (isPaused || speedMultiplier == SimulationSpeedProfile.Paused)
        {
            return CreateResult(ticksExecuted: 0, requestedTicksThisFrame: 0.0);
        }

        double requestedTicks =
            unscaledDeltaSeconds * _baseTicksPerSecond * speedMultiplier;

        if (!IsFinite(requestedTicks))
        {
            throw new OverflowException("Requested frame ticks exceeded the supported numeric range.");
        }

        double nextAccumulator = _accumulatedTicks + requestedTicks;
        if (!IsFinite(nextAccumulator) || nextAccumulator > long.MaxValue)
        {
            throw new OverflowException("The retained frame backlog exceeded Int64 tick capacity.");
        }

        _accumulatedTicks = nextAccumulator;

        long availableWholeTicks = GetWholeTicks(_accumulatedTicks);
        int ticksToRun = (int)Math.Min(availableWholeTicks, _maxTicksPerFrame);
        int ticksExecuted = 0;

        try
        {
            for (; ticksExecuted < ticksToRun; ticksExecuted++)
            {
                runOneTick();
            }
        }
        finally
        {
            _accumulatedTicks -= ticksExecuted;
            if (_accumulatedTicks < 0.0 && _accumulatedTicks > -TickBoundaryEpsilon)
            {
                _accumulatedTicks = 0.0;
            }
        }

        return CreateResult(ticksExecuted, requestedTicks);
    }

    /// <summary>
    /// Clears presentation timing state after starting a new simulation or loading at a tick boundary.
    /// </summary>
    public void Reset()
        => _accumulatedTicks = 0.0;

    private TickPumpResult CreateResult(int ticksExecuted, double requestedTicksThisFrame)
    {
        long wholeTicks = GetWholeTicks(_accumulatedTicks);
        double fractionalTicks = ClampRepresentationalNoise(_accumulatedTicks - wholeTicks);

        return new TickPumpResult(
            ticksExecuted,
            wholeTicks,
            fractionalTicks,
            _accumulatedTicks,
            requestedTicksThisFrame,
            BudgetLimited: wholeTicks > 0);
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private static double ClampRepresentationalNoise(double value)
        => value < 0.0 && value > -TickBoundaryEpsilon ? 0.0 : value;

    private static long GetWholeTicks(double accumulatedTicks)
    {
        double adjusted = accumulatedTicks + TickBoundaryEpsilon;
        return checked((long)Math.Floor(adjusted));
    }
}
