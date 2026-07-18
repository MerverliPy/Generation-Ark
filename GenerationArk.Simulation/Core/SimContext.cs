using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Core;

public readonly struct SimContext
{
    public required SimTick Tick { get; init; }
    public required SimPhase Phase { get; init; }
    public required WorldState World { get; init; }
    public required SimCommandQueue Commands { get; init; }
}
