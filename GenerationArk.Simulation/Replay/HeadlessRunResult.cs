using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GenerationArk.Simulation.Replay;

public sealed class HeadlessRunResult
{
    private readonly ReadOnlyCollection<ReplayCheckpoint> _checkpoints;

    public HeadlessRunResult(
        long startTick,
        long endTick,
        ulong finalChecksum,
        ReplayCheckpoint[] checkpoints)
    {
        if (endTick < startTick)
        {
            throw new ArgumentOutOfRangeException(nameof(endTick));
        }
        StartTick = startTick;
        EndTick = endTick;
        FinalChecksum = finalChecksum;
        _checkpoints = Array.AsReadOnly(
            checkpoints ?? throw new ArgumentNullException(nameof(checkpoints)));
    }

    public long StartTick { get; }

    public long EndTick { get; }

    public long TicksExecuted => checked(EndTick - StartTick);

    public ulong FinalChecksum { get; }

    public IReadOnlyList<ReplayCheckpoint> Checkpoints => _checkpoints;
}
