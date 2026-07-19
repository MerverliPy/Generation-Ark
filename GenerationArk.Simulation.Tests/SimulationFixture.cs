using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Random;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Tests;

internal sealed class SimulationFixture
{
    public SimulationFixture(
        IEnumerable<ISimSystem> systems,
        IEnumerable<IScheduledEventHandler>? scheduledEventHandlers = null,
        SimulationSeed? seed = null,
        IRandomRequestTracer? randomTracer = null,
        SimulationTrace? trace = null,
        WorldState? world = null)
    {
        Clock = new SimulationClock();
        World = world ?? new WorldState();
        Commands = new SimCommandQueue();
        Scheduler = new DeterministicScheduler(
            new ScheduledEventHandlerRegistry(
                scheduledEventHandlers ?? Array.Empty<IScheduledEventHandler>()));
        Trace = trace;
        IRandomRequestTracer? effectiveRandomTracer = CombineTracers(randomTracer, trace);
        Random = new CounterBasedRandomV1(
            seed ?? new SimulationSeed(0x47454E4552415445UL),
            effectiveRandomTracer);
        Checksums = new ChecksumHistory();
        Runner = new SimulationRunner(
            Clock,
            World,
            new SimSystemRegistry(systems),
            Commands,
            Scheduler,
            Random,
            Checksums,
            Trace);
        Pump = new SimulationFramePump(Clock, Runner, maxTicksPerFrame: 8192);
    }

    public SimulationClock Clock { get; }
    public WorldState World { get; }
    public SimCommandQueue Commands { get; }
    public DeterministicScheduler Scheduler { get; }
    public CounterBasedRandomV1 Random { get; }
    public ChecksumHistory Checksums { get; }
    public SimulationTrace? Trace { get; }
    public SimulationRunner Runner { get; }
    public SimulationFramePump Pump { get; }

    private static IRandomRequestTracer? CombineTracers(
        IRandomRequestTracer? first,
        IRandomRequestTracer? second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null || ReferenceEquals(first, second))
        {
            return first;
        }

        return new CompositeRandomRequestTracer(first, second);
    }

    private sealed class CompositeRandomRequestTracer : IRandomRequestTracer
    {
        private readonly IRandomRequestTracer _first;
        private readonly IRandomRequestTracer _second;

        public CompositeRandomRequestTracer(
            IRandomRequestTracer first,
            IRandomRequestTracer second)
        {
            _first = first;
            _second = second;
        }

        public void Record(RandomRequest request)
        {
            _first.Record(request);
            _second.Record(request);
        }
    }
}
