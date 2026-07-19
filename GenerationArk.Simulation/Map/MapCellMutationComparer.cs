using System.Collections.Generic;

namespace GenerationArk.Simulation.Map;

internal sealed class MapCellMutationComparer : IComparer<MapCellMutation>
{
    public static MapCellMutationComparer Instance { get; } = new();

    public int Compare(MapCellMutation left, MapCellMutation right)
    {
        int cell = left.Cell.CompareTo(right.Cell);
        if (cell != 0)
        {
            return cell;
        }

        int kind = left.Kind.CompareTo(right.Kind);
        if (kind != 0)
        {
            return kind;
        }

        return left.MutationSequence.CompareTo(right.MutationSequence);
    }
}
