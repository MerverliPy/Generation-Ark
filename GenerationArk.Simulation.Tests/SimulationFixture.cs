using System.Collections.Generic;
using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Tests;

internal sealed class SimulationFixture
{
    public SimulationFixture(IEnumerable<ISimSystem> systems)
    {
        Clock = new SimulationClock();
        World = new WorldState();
        Commands = new SimCommandQueue();
        Checksums = new ChecksumHistory();
        Runner = new SimulationRunner(
            Clock,
            World,
            new SimSystemRegistry(systems),
            Commands,
            Checksums);
        Pump = new SimulationFramePump(Clock, Runner, maxTicksPerFrame: 8192);
    }

    public SimulationClock Clock { get; }
    public WorldState World { get; }
    public SimCommandQueue Commands { get; }
    public ChecksumHistory Checksums { get; }
    public SimulationRunner Runner { get; }
    public SimulationFramePump Pump { get; }
}
