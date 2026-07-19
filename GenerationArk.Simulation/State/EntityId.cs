using System;
using System.Globalization;

namespace GenerationArk.Simulation.State;

public readonly record struct EntityId(ulong Value) : IComparable<EntityId>
{
    public static EntityId None { get; } = new(0);

    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
