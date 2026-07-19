using System;
using System.Collections.Generic;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Canonical authoritative command ordering: target tick, sequence, then ordinal ID.
/// </summary>
public sealed class ReplayCommandComparer : IComparer<ReplayCommand>
{
    public static ReplayCommandComparer Instance { get; } = new();

    private ReplayCommandComparer()
    {
    }

    public int Compare(ReplayCommand? left, ReplayCommand? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }
        if (left is null)
        {
            return -1;
        }
        if (right is null)
        {
            return 1;
        }

        int comparison = left.TargetTick.CompareTo(right.TargetTick);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Sequence.CompareTo(right.Sequence);
        if (comparison != 0)
        {
            return comparison;
        }

        return StringComparer.Ordinal.Compare(left.CommandId, right.CommandId);
    }
}
