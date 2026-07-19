using System;
using GenerationArk.Simulation.UnityAdapter;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// One presentation-frame instruction for frame-pattern determinism validation.
/// </summary>
public readonly struct FramePatternStep
{
    public FramePatternStep(
        double unscaledDeltaSeconds,
        int speedMultiplier,
        int manualSteps = 0)
    {
        if (double.IsNaN(unscaledDeltaSeconds)
            || double.IsInfinity(unscaledDeltaSeconds)
            || unscaledDeltaSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
        }
        SimulationSpeedProfile.ValidateSupported(speedMultiplier, nameof(speedMultiplier));
        if (manualSteps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(manualSteps));
        }
        if (manualSteps > 0 && speedMultiplier != SimulationSpeedProfile.Paused)
        {
            throw new ArgumentException("Manual steps require a paused frame pattern step.", nameof(manualSteps));
        }

        UnscaledDeltaSeconds = unscaledDeltaSeconds;
        SpeedMultiplier = speedMultiplier;
        ManualSteps = manualSteps;
    }

    public double UnscaledDeltaSeconds { get; }
    public int SpeedMultiplier { get; }
    public int ManualSteps { get; }
}
