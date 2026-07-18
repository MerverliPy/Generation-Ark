using System.Collections.Generic;
using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Diagnostics;

public sealed class ChecksumHistory
{
    private readonly SortedDictionary<long, ulong> _values = new();

    public IReadOnlyDictionary<long, ulong> Values => _values;

    public void Record(SimTick tick, ulong checksum) => _values[tick.Value] = checksum;

    public ulong At(SimTick tick) => _values[tick.Value];
}
