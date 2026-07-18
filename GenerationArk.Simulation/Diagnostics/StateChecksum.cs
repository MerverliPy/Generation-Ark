using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Diagnostics;

public static class StateChecksum
{
    public static ulong Compute(SimTick tick, WorldState world)
    {
        var hash = new StableHash64();
        hash.AddInt64(tick.Value);

        foreach ((string key, long value) in world.Counters)
        {
            hash.AddString(key);
            hash.AddInt64(value);
        }

        // Trace data is diagnostic, not authoritative state, so it is excluded.
        return hash.Value;
    }
}
