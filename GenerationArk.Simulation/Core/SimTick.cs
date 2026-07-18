using System;

namespace GenerationArk.Simulation.Core;

/// <summary>Authoritative integer simulation time.</summary>
public readonly record struct SimTick(long Value) : IComparable<SimTick>
{
    public static SimTick Zero => new(0);

    public int CompareTo(SimTick other) => Value.CompareTo(other.Value);

    public static bool operator <(SimTick left, SimTick right)
        => left.Value < right.Value;

    public static bool operator >(SimTick left, SimTick right)
        => left.Value > right.Value;

    public static bool operator <=(SimTick left, SimTick right)
        => left.Value <= right.Value;

    public static bool operator >=(SimTick left, SimTick right)
        => left.Value >= right.Value;

    public static SimTick operator +(SimTick tick, long amount)
        => new(checked(tick.Value + amount));

    public static long operator -(SimTick left, SimTick right)
        => checked(left.Value - right.Value);

    public override string ToString()
        => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
