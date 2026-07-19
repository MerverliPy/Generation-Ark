using System;

namespace GenerationArk.Simulation.Diagnostics;

public readonly record struct ChecksumComponentId : IComparable<ChecksumComponentId>
{
    public ChecksumComponentId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public int CompareTo(ChecksumComponentId other)
        => StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value;
}
