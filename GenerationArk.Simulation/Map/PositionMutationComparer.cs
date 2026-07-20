using System.Collections.Generic;

namespace GenerationArk.Simulation.Map;

internal sealed class PositionMutationComparer : IComparer<PositionMutation>
{
    public static PositionMutationComparer Instance { get; } = new();

    public int Compare(PositionMutation left, PositionMutation right)
    {
        int entity = left.EntityId.CompareTo(right.EntityId);
        if (entity != 0)
        {
            return entity;
        }

        int kind = left.Position.HasValue.CompareTo(right.Position.HasValue);
        if (kind != 0)
        {
            return kind;
        }

        int cell = left.Position.GetValueOrDefault().CompareTo(right.Position.GetValueOrDefault());
        if (cell != 0)
        {
            return cell;
        }

        return left.MutationSequence.CompareTo(right.MutationSequence);
    }
}
