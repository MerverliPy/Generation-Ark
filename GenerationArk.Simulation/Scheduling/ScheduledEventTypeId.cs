using System;

namespace GenerationArk.Simulation.Scheduling;

public readonly record struct ScheduledEventTypeId : IComparable<ScheduledEventTypeId>
{
    public ScheduledEventTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public int CompareTo(ScheduledEventTypeId other)
        => StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value;
}
