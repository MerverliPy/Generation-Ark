using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Random;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Diagnostics;

public static class StateChecksum
{
    private static readonly ChecksumComponentId ClockComponent = new("clock");
    private static readonly ChecksumComponentId EntityComponent = new("entities");
    private static readonly ChecksumComponentId MapComponent = new("map-topology");
    private static readonly ChecksumComponentId SpatialComponent = new("spatial");
    private static readonly ChecksumComponentId RandomComponent = new("random");
    private static readonly ChecksumComponentId SchedulerComponent = new("scheduler");
    private static readonly ChecksumComponentId WorldComponent = new("world");

    public static ulong Compute(SimTick tick, WorldState world)
        => Compute(tick, world, scheduler: null, random: null);

    public static ulong Compute(
        SimTick tick,
        WorldState world,
        ISimScheduler? scheduler)
        => Compute(tick, world, scheduler, random: null);

    public static ulong Compute(
        SimTick tick,
        WorldState world,
        ISimScheduler? scheduler,
        ISimRandom? random)
    {
        ArgumentNullException.ThrowIfNull(world);
        var writer = new StateChecksumWriter();
        writer.AddInt64(tick.Value);
        AddWorld(writer, world);
        world.WriteEntityChecksum(writer);
        world.Map.WriteChecksum(writer);
        world.WriteSpatialChecksum(writer);

        if (scheduler is not null)
        {
            AddScheduler(writer, scheduler.CaptureSnapshot());
        }

        if (random is not null)
        {
            AddRandom(writer, random.CaptureSnapshot());
        }

        // Trace data is diagnostic, not authoritative state, so it is excluded.
        return writer.Value;
    }

    public static TickChecksum ComputeDetailed(
        SimTick tick,
        WorldState world,
        ISimScheduler? scheduler = null,
        ISimRandom? random = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var contributors = new List<IStateChecksumContributor>
        {
            new DelegateChecksumContributor(
                ClockComponent,
                writer => writer.AddInt64(tick.Value)),
            new DelegateChecksumContributor(
                WorldComponent,
                writer => AddWorld(writer, world)),
            new DelegateChecksumContributor(
                EntityComponent,
                world.WriteEntityChecksum),
            new DelegateChecksumContributor(
                MapComponent,
                world.Map.WriteChecksum),
            new DelegateChecksumContributor(
                SpatialComponent,
                world.WriteSpatialChecksum)
        };

        if (scheduler is not null)
        {
            SchedulerSnapshot snapshot = scheduler.CaptureSnapshot();
            contributors.Add(new DelegateChecksumContributor(
                SchedulerComponent,
                writer => AddScheduler(writer, snapshot)));
        }

        if (random is not null)
        {
            RandomStateSnapshot snapshot = random.CaptureSnapshot();
            contributors.Add(new DelegateChecksumContributor(
                RandomComponent,
                writer => AddRandom(writer, snapshot)));
        }

        return new TickChecksum(
            tick,
            ChecksumFormatVersion.Current,
            Compute(tick, world, scheduler, random),
            ComputeComponentChecksums(contributors));
    }

    public static IReadOnlyList<ComponentChecksum> ComputeComponentChecksums(
        IEnumerable<IStateChecksumContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        IStateChecksumContributor[] ordered = contributors
            .Select(contributor => contributor
                ?? throw new ArgumentException(
                    "Checksum contributors cannot contain null entries.",
                    nameof(contributors)))
            .OrderBy(static contributor => contributor.ComponentId)
            .ToArray();

        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].ComponentId == ordered[index].ComponentId)
            {
                throw new InvalidOperationException(
                    $"Duplicate checksum component ID: {ordered[index].ComponentId}.");
            }
        }

        var result = new ComponentChecksum[ordered.Length];
        for (int index = 0; index < ordered.Length; index++)
        {
            var writer = new StateChecksumWriter();
            ordered[index].Write(writer);
            result[index] = new ComponentChecksum(
                ordered[index].ComponentId,
                writer.Value);
        }

        return result;
    }

    private static void AddWorld(StateChecksumWriter writer, WorldState world)
    {
        foreach ((string key, long value) in world.Counters)
        {
            writer.AddString(key);
            writer.AddInt64(value);
        }
    }

    private static void AddRandom(
        StateChecksumWriter writer,
        RandomStateSnapshot snapshot)
    {
        writer.AddUInt64(snapshot.RootSeed);
        writer.AddUInt32(snapshot.AlgorithmVersion);
    }

    private static void AddScheduler(
        StateChecksumWriter writer,
        SchedulerSnapshot snapshot)
    {
        writer.AddInt64(snapshot.CurrentTick);
        writer.AddUInt64(snapshot.NextEventId);
        writer.AddUInt64(snapshot.NextCreationSequence);
        writer.AddInt32(snapshot.Events.Length);

        foreach (ScheduledEventSnapshot scheduledEvent in snapshot.Events)
        {
            writer.AddUInt64(scheduledEvent.Id);
            writer.AddInt64(scheduledEvent.DueTick);
            writer.AddByte(scheduledEvent.Phase);
            writer.AddInt32(scheduledEvent.Priority);
            writer.AddUInt64(scheduledEvent.CreationSequence);
            writer.AddString(scheduledEvent.Type);

            writer.AddBoolean(scheduledEvent.Owner.HasValue);
            if (scheduledEvent.Owner is ulong owner)
            {
                writer.AddUInt64(owner);
            }

            writer.AddString(scheduledEvent.Payload);
            writer.AddBoolean(scheduledEvent.RepeatIntervalTicks.HasValue);
            if (scheduledEvent.RepeatIntervalTicks is long interval)
            {
                writer.AddInt64(interval);
            }
        }
    }

    private sealed class DelegateChecksumContributor : IStateChecksumContributor
    {
        private readonly Action<StateChecksumWriter> _write;

        public DelegateChecksumContributor(
            ChecksumComponentId componentId,
            Action<StateChecksumWriter> write)
        {
            ComponentId = componentId;
            _write = write ?? throw new ArgumentNullException(nameof(write));
        }

        public ChecksumComponentId ComponentId { get; }

        public void Write(StateChecksumWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);
            _write(writer);
        }
    }
}
