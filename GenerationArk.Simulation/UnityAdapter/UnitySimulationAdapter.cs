using System;

namespace GenerationArk.Simulation.UnityAdapter;

/// <summary>
/// Engine-neutral orchestration used by the Unity MonoBehaviour boundary and by headless tests.
/// </summary>
public sealed class UnitySimulationAdapter
{
    private readonly Action _runOneTick;
    private readonly UnityFrameAccumulator _accumulator;
    private readonly FastForwardPresentationController _presentation;

    private long _frameSequence;
    private int _requestedSpeedMultiplier = SimulationSpeedProfile.Normal;
    private bool _isPaused;

    public UnitySimulationAdapter(
        Action runOneTick,
        int maxTicksPerFrame,
        double baseTicksPerSecond = UnityFrameAccumulator.DefaultBaseTicksPerSecond,
        FastForwardPresentationController? presentation = null)
    {
        if (runOneTick is null)
        {
            throw new ArgumentNullException(nameof(runOneTick));
        }

        _runOneTick = runOneTick;
        _accumulator = new UnityFrameAccumulator(maxTicksPerFrame, baseTicksPerSecond);
        _presentation = presentation ?? new FastForwardPresentationController();
    }

    public bool IsPaused => _isPaused;

    public int RequestedSpeedMultiplier => _requestedSpeedMultiplier;

    public int EffectiveSpeedMultiplier
        => _isPaused ? SimulationSpeedProfile.Paused : _requestedSpeedMultiplier;

    public long FrameSequence => _frameSequence;

    public double AccumulatedTicks => _accumulator.AccumulatedTicks;

    public long WholeTicksBacklogged => _accumulator.WholeTicksBacklogged;

    public void SetSpeedMultiplier(int multiplier)
    {
        SimulationSpeedProfile.ValidateSupported(multiplier, nameof(multiplier));

        if (multiplier == SimulationSpeedProfile.Paused)
        {
            Pause();
            return;
        }

        _requestedSpeedMultiplier = multiplier;
        _isPaused = false;
    }

    public void Pause()
        => _isPaused = true;

    public void Resume()
        => _isPaused = false;

    /// <summary>
    /// Advances exactly one authoritative tick while paused. Presentation backlog is unchanged.
    /// </summary>
    public void StepOneTick()
    {
        if (!_isPaused)
        {
            throw new InvalidOperationException(
                "Manual stepping is only valid while the simulation is paused.");
        }

        _runOneTick();
    }

    public FrameAdvanceResult AdvanceFrame(double unscaledDeltaSeconds)
    {
        _frameSequence = checked(_frameSequence + 1);

        TickPumpResult tickPump = _accumulator.AdvanceFrame(
            unscaledDeltaSeconds,
            EffectiveSpeedMultiplier,
            _isPaused,
            _runOneTick);

        bool shouldPresent = _presentation.ShouldPresent(
            _frameSequence,
            EffectiveSpeedMultiplier,
            _isPaused,
            tickPump.IsCatchingUp);

        return new FrameAdvanceResult(
            _frameSequence,
            _requestedSpeedMultiplier,
            EffectiveSpeedMultiplier,
            _isPaused,
            tickPump,
            shouldPresent);
    }

    /// <summary>
    /// Resets only Unity/presentation timing state. It does not touch authoritative simulation state.
    /// </summary>
    public void ResetPresentationState()
    {
        _accumulator.Reset();
        _frameSequence = 0;
    }
}
