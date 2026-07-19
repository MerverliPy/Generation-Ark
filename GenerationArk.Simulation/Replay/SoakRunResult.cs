namespace GenerationArk.Simulation.Replay;

public sealed class SoakRunResult
{
    public SoakRunResult(
        long totalTicks,
        int checkpointCount,
        ulong firstFinalChecksum,
        ulong secondFinalChecksum,
        bool succeeded)
    {
        TotalTicks = totalTicks;
        CheckpointCount = checkpointCount;
        FirstFinalChecksum = firstFinalChecksum;
        SecondFinalChecksum = secondFinalChecksum;
        Succeeded = succeeded;
    }

    public long TotalTicks { get; }
    public int CheckpointCount { get; }
    public ulong FirstFinalChecksum { get; }
    public ulong SecondFinalChecksum { get; }
    public bool Succeeded { get; }
}
