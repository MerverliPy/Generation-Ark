namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Canonical checksum captured at an exact authoritative tick boundary.
/// </summary>
public readonly struct ReplayCheckpoint
{
    public ReplayCheckpoint(long tick, ulong checksum)
    {
        Tick = tick;
        Checksum = checksum;
    }

    public long Tick { get; }

    public ulong Checksum { get; }
}
