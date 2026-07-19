using System;

namespace GenerationArk.Simulation.Random;

public readonly record struct RandomDomainId : IComparable<RandomDomainId>
{
    public RandomDomainId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public int CompareTo(RandomDomainId other)
        => StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value;
}
