using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Diagnostics;

public sealed class ChecksumHistory
{
    private readonly SortedDictionary<long, ulong> _values = new();
    private readonly SortedDictionary<long, TickChecksum> _detailedValues = new();

    public IReadOnlyDictionary<long, ulong> Values => _values;
    public IReadOnlyDictionary<long, TickChecksum> DetailedValues => _detailedValues;

    public void Record(SimTick tick, ulong checksum)
    {
        _values[tick.Value] = checksum;
        _detailedValues.Remove(tick.Value);
    }

    public void Record(TickChecksum checksum)
    {
        ArgumentNullException.ThrowIfNull(checksum);
        _values[checksum.Tick.Value] = checksum.GlobalChecksum;
        _detailedValues[checksum.Tick.Value] = checksum;
    }

    public ulong At(SimTick tick) => _values[tick.Value];

    public TickChecksum DetailedAt(SimTick tick) => _detailedValues[tick.Value];
}
