using System;
using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Random;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Core;

public sealed class SimulationRunner : ISimulationRunner
{
    private readonly SimulationClock _clock;
    private readonly WorldState _world;
    private readonly SimSystemRegistry _systems;
    private readonly SimCommandQueue _commands;
    private readonly DeterministicScheduler _scheduler;
    private readonly ISimRandom _random;
    private readonly ChecksumHistory _checksums;
    private readonly SimulationTrace? _trace;

    public SimulationRunner(
        SimulationClock clock,
        WorldState world,
        SimSystemRegistry systems,
        SimCommandQueue commands,
        DeterministicScheduler scheduler,
        ISimRandom random,
        ChecksumHistory checksums,
        SimulationTrace? trace = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _systems = systems ?? throw new ArgumentNullException(nameof(systems));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _checksums = checksums ?? throw new ArgumentNullException(nameof(checksums));
        _trace = trace;
    }

    public SimTick CurrentTick => _clock.CurrentTick;
    public WorldState World => _world;
    public DeterministicScheduler Scheduler => _scheduler;
    public ChecksumHistory Checksums => _checksums;
    public SimulationTrace? Trace => _trace;

    public void RunOneTick()
    {
        SimTick tick = _clock.AdvanceOneTick();
        _scheduler.BeginTick(tick);
        _trace?.BeginTick(tick, _commands.PendingCount, _scheduler.PendingCount);

        SimPhase? activePhase = null;
        SystemId? activeSystem = null;
        TickChecksum? tickChecksum = null;

        try
        {
            foreach (SimPhase phase in SimPhaseOrder.All)
            {
                activePhase = phase;
                activeSystem = null;
                _scheduler.EnterPhase(phase);
                var context = new SimContext
                {
                    Tick = tick,
                    Phase = phase,
                    World = _world,
                    Commands = _commands,
                    Scheduler = _scheduler,
                    Random = _random
                };

                if (phase == SimPhase.PreSimulation)
                {
                    _world.ActivatePendingEntities(tick);
                }

                if (phase == SimPhase.CommandApply)
                {
                    int firstResult = _commands.Results.Count;
                    try
                    {
                        _commands.ApplyForTick(context);
                    }
                    finally
                    {
                        for (int index = firstResult; index < _commands.Results.Count; index++)
                        {
                            _trace?.RecordCommand(_commands.Results[index]);
                        }
                    }
                }

                _scheduler.ExecuteDueEvents(context, _trace);

                foreach (ISimSystem system in _systems.ForPhase(phase))
                {
                    activeSystem = system.Id;
                    _trace?.RecordSystem(phase, system.Id);
                    system.Tick(context);
                    activeSystem = null;
                }

                if (phase == SimPhase.Commit)
                {
                    _world.CommitMutations(_scheduler, tick);
                }

                if (phase == SimPhase.Diagnostics)
                {
                    _world.ValidateEntityInvariants(_scheduler);
                    tickChecksum = StateChecksum.ComputeDetailed(
                        tick,
                        _world,
                        _scheduler,
                        _random);
                    _checksums.Record(tickChecksum);
                }
            }

            if (tickChecksum is null)
            {
                throw new InvalidOperationException(
                    $"Tick {tick} completed without a diagnostics checksum.");
            }

            _scheduler.EndTick();
        }
        catch (Exception exception)
        {
            _trace?.FailTick(
                tick,
                activePhase,
                activeSystem,
                exception,
                _commands.PendingCount,
                _scheduler.PendingCount);

            // The tick is authoritative and must not be continued after failure.
            // EndTick is intentionally not called because pending due events may remain.
            throw;
        }

        _trace?.CompleteTick(
            tickChecksum!,
            _commands.PendingCount,
            _scheduler.PendingCount);
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
