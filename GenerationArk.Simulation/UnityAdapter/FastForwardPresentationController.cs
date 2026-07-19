using System;

namespace GenerationArk.Simulation.UnityAdapter;

/// <summary>
/// Presentation-only throttling policy. It never changes tick count, state, command order, or checksums.
/// </summary>
public sealed class FastForwardPresentationController
{
    public bool ShouldPresent(
        long frameSequence,
        int speedMultiplier,
        bool isPaused,
        bool isCatchingUp)
    {
        if (frameSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameSequence),
                frameSequence,
                "Frame sequence must be positive.");
        }

        SimulationSpeedProfile.ValidateSupported(speedMultiplier, nameof(speedMultiplier));

        if (isPaused || speedMultiplier == SimulationSpeedProfile.Paused)
        {
            return true;
        }

        int interval = speedMultiplier switch
        {
            SimulationSpeedProfile.Normal => 1,
            SimulationSpeedProfile.Fast => 1,
            SimulationSpeedProfile.VeryFast => 2,
            SimulationSpeedProfile.Chronicle => 4,
            SimulationSpeedProfile.DeepFastForward => 8,
            _ => throw new InvalidOperationException("Validated speed was not mapped.")
        };

        if (isCatchingUp)
        {
            interval = Math.Min(interval * 2, 32);
        }

        return frameSequence % interval == 0;
    }
}
