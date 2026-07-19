using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.Random;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Core;

public readonly struct SimContext
{
    public required SimTick Tick { get; init; }
    public required SimPhase Phase { get; init; }
    public required WorldState World { get; init; }
    public required SimCommandQueue Commands { get; init; }
    public required ISimScheduler Scheduler { get; init; }
    public required ISimRandom Random { get; init; }
}
