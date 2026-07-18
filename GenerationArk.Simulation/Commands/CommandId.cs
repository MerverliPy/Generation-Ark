using System;

namespace GenerationArk.Simulation.Commands;

public readonly record struct CommandId(ulong Value) : IComparable<CommandId>
{
    public int CompareTo(CommandId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
