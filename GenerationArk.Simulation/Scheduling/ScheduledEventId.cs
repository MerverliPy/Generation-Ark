using System;
using System.Globalization;

namespace GenerationArk.Simulation.Scheduling;

public readonly record struct ScheduledEventId(ulong Value) : IComparable<ScheduledEventId>
{
    public int CompareTo(ScheduledEventId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
