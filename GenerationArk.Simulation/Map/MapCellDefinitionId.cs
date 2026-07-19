using System;
using System.Globalization;

namespace GenerationArk.Simulation.Map;

public readonly record struct MapCellDefinitionId(int Value) : IComparable<MapCellDefinitionId>
{
    public int CompareTo(MapCellDefinitionId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
