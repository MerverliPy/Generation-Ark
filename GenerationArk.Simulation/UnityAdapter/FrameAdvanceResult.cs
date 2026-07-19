namespace GenerationArk.Simulation.UnityAdapter;

/// <summary>
/// Complete non-authoritative result of one Unity/presentation frame.
/// </summary>
public readonly record struct FrameAdvanceResult(
    long FrameSequence,
    int RequestedSpeedMultiplier,
    int EffectiveSpeedMultiplier,
    bool IsPaused,
    TickPumpResult TickPump,
    bool ShouldPresent)
{
    public bool IsCatchingUp => TickPump.IsCatchingUp;
}
