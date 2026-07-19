namespace GenerationArk.Simulation.Replay;

public sealed class ContinuityValidationResult
{
    public ContinuityValidationResult(
        bool succeeded,
        long saveTick,
        ulong savedChecksum,
        long? firstMismatchTick,
        ulong? expectedChecksum,
        ulong? actualChecksum)
    {
        Succeeded = succeeded;
        SaveTick = saveTick;
        SavedChecksum = savedChecksum;
        FirstMismatchTick = firstMismatchTick;
        ExpectedChecksum = expectedChecksum;
        ActualChecksum = actualChecksum;
    }

    public bool Succeeded { get; }
    public long SaveTick { get; }
    public ulong SavedChecksum { get; }
    public long? FirstMismatchTick { get; }
    public ulong? ExpectedChecksum { get; }
    public ulong? ActualChecksum { get; }
}
