using System;
using System.Globalization;

namespace GenerationArk.Simulation.Scheduling;

public readonly record struct ScheduledEventOwnerId(ulong Value) : IComparable<ScheduledEventOwnerId>
{
    public int CompareTo(ScheduledEventOwnerId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
