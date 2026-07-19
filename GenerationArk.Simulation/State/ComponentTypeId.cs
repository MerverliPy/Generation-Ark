using System;

namespace GenerationArk.Simulation.State;

public readonly record struct ComponentTypeId : IComparable<ComponentTypeId>
{
    public ComponentTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public int CompareTo(ComponentTypeId other)
        => StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value;
}
