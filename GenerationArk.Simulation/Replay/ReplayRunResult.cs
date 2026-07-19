namespace GenerationArk.Simulation.Replay;

public sealed class ReplayRunResult
{
    public ReplayRunResult(
        HeadlessRunResult run,
        bool succeeded,
        long? firstMismatchTick,
        ulong? expectedChecksum,
        ulong? actualChecksum)
    {
        Run = run;
        Succeeded = succeeded;
        FirstMismatchTick = firstMismatchTick;
        ExpectedChecksum = expectedChecksum;
        ActualChecksum = actualChecksum;
    }

    public HeadlessRunResult Run { get; }

    public bool Succeeded { get; }

    public long? FirstMismatchTick { get; }

    public ulong? ExpectedChecksum { get; }

    public ulong? ActualChecksum { get; }
}
