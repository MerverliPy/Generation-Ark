using System;
using System.Collections.Generic;

namespace GenerationArk.Simulation.State;

/// <summary>
/// Minimal deterministic state container for the first clock milestone.
/// Gameplay-specific state will replace these counters in later milestones.
/// </summary>
public sealed class WorldState
{
    private readonly SortedDictionary<string, long> _counters = new(StringComparer.Ordinal);
    private readonly List<string> _trace = new();

    public IReadOnlyDictionary<string, long> Counters => _counters;
    public IReadOnlyList<string> Trace => _trace;

    public long GetCounter(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _counters.TryGetValue(key, out long value) ? value : 0;
    }

    public void SetCounter(string key, long value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _counters[key] = value;
    }

    public void AddToCounter(string key, long delta)
        => SetCounter(key, checked(GetCounter(key) + delta));

    public void AppendTrace(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _trace.Add(value);
    }
}
