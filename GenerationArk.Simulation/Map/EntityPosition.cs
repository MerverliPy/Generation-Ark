using System;

namespace GenerationArk.Simulation.Map;

public readonly record struct EntityPosition(MapCellId Cell) : IComparable<EntityPosition>
{
    public int CompareTo(EntityPosition other) => Cell.CompareTo(other.Cell);
}
