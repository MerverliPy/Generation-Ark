using System;
using System.Globalization;

namespace GenerationArk.Simulation.Map;

public readonly record struct RoomId(int Value) : IComparable<RoomId>
{
    public int CompareTo(RoomId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
