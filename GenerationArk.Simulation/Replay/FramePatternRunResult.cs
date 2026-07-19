using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GenerationArk.Simulation.Replay;

public sealed class FramePatternRunResult
{
    private readonly ReadOnlyCollection<ReplayCheckpoint> _checkpoints;

    public FramePatternRunResult(
        string patternName,
        long finalTick,
        ulong finalChecksum,
        ReplayCheckpoint[] checkpoints)
    {
        PatternName = patternName ?? throw new ArgumentNullException(nameof(patternName));
        FinalTick = finalTick;
        FinalChecksum = finalChecksum;
        _checkpoints = Array.AsReadOnly(
            checkpoints ?? throw new ArgumentNullException(nameof(checkpoints)));
    }

    public string PatternName { get; }
    public long FinalTick { get; }
    public ulong FinalChecksum { get; }
    public IReadOnlyList<ReplayCheckpoint> Checkpoints => _checkpoints;
}
