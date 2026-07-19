using System;

namespace GenerationArk.Simulation.UnityAdapter;

/// <summary>
/// Stable presentation-side speed multipliers supported by the simulation clock specification.
/// The adapter validates these values before converting frame time into requested ticks.
/// </summary>
public static class SimulationSpeedProfile
{
    public const int Paused = 0;
    public const int Normal = 1;
    public const int Fast = 4;
    public const int VeryFast = 16;
    public const int Chronicle = 64;
    public const int DeepFastForward = 256;

    public static bool IsSupported(int multiplier)
        => multiplier is Paused
            or Normal
            or Fast
            or VeryFast
            or Chronicle
            or DeepFastForward;

    public static bool IsRunningSpeed(int multiplier)
        => multiplier is Normal
            or Fast
            or VeryFast
            or Chronicle
            or DeepFastForward;

    public static void ValidateSupported(int multiplier, string? parameterName = null)
    {
        if (!IsSupported(multiplier))
        {
            throw new ArgumentOutOfRangeException(
                parameterName ?? nameof(multiplier),
                multiplier,
                "Supported simulation speed multipliers are 0, 1, 4, 16, 64, and 256.");
        }
    }

    public static void ValidateRunning(int multiplier, string? parameterName = null)
    {
        if (!IsRunningSpeed(multiplier))
        {
            throw new ArgumentOutOfRangeException(
                parameterName ?? nameof(multiplier),
                multiplier,
                "Running simulation speed multipliers are 1, 4, 16, 64, and 256.");
        }
    }
}
