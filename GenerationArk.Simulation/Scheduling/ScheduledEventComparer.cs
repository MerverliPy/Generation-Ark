using System.Collections.Generic;
using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Scheduling;

internal sealed class ScheduledEventComparer : IComparer<ScheduledEvent>
{
    public static ScheduledEventComparer Instance { get; } = new();

    public int Compare(ScheduledEvent? x, ScheduledEvent? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int byTick = x.DueTick.CompareTo(y.DueTick);
        if (byTick != 0)
        {
            return byTick;
        }

        int byPhase = SimPhaseOrder.IndexOf(x.Phase).CompareTo(SimPhaseOrder.IndexOf(y.Phase));
        if (byPhase != 0)
        {
            return byPhase;
        }

        int byPriority = x.Priority.CompareTo(y.Priority);
        if (byPriority != 0)
        {
            return byPriority;
        }

        int bySequence = x.CreationSequence.CompareTo(y.CreationSequence);
        if (bySequence != 0)
        {
            return bySequence;
        }

        return x.Id.CompareTo(y.Id);
    }
}
