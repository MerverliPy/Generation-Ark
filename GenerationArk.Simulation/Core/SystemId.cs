using System;

namespace GenerationArk.Simulation.Core;

public readonly record struct SystemId : IComparable<SystemId>
{
    public string Value { get; }

    public SystemId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("System ID must be non-empty.", nameof(value));
        }

        Value = value;
    }

    public int CompareTo(SystemId other) => StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value;
}
