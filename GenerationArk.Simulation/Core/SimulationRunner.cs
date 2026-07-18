using System;
using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Core;

public sealed class SimulationRunner : ISimulationRunner
{
    private readonly SimulationClock _clock;
    private readonly WorldState _world;
    private readonly SimSystemRegistry _systems;
    private readonly SimCommandQueue _commands;
    private readonly ChecksumHistory _checksums;

    public SimulationRunner(
        SimulationClock clock,
        WorldState world,
        SimSystemRegistry systems,
        SimCommandQueue commands,
        ChecksumHistory checksums)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _systems = systems ?? throw new ArgumentNullException(nameof(systems));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _checksums = checksums ?? throw new ArgumentNullException(nameof(checksums));
    }

    public SimTick CurrentTick => _clock.CurrentTick;
    public WorldState World => _world;
    public ChecksumHistory Checksums => _checksums;

    public void RunOneTick()
    {
        SimTick tick = _clock.AdvanceOneTick();

        foreach (SimPhase phase in SimPhaseOrder.All)
        {
            var context = new SimContext
            {
                Tick = tick,
                Phase = phase,
                World = _world,
                Commands = _commands
            };

            if (phase == SimPhase.CommandApply)
            {
                _commands.ApplyForTick(context);
            }

            foreach (ISimSystem system in _systems.ForPhase(phase))
            {
                system.Tick(context);
            }

            if (phase == SimPhase.Diagnostics)
            {
                _checksums.Record(tick, StateChecksum.Compute(tick, _world));
            }
        }
    }

    public void RunTicks(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Tick count cannot be negative.");
        }

        for (int i = 0; i < count; i++)
        {
            RunOneTick();
        }
    }
}
