using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Random;
using GenerationArk.Simulation.Scheduling;

namespace GenerationArk.Simulation.Diagnostics;

/// <summary>
/// Diagnostic-only bounded tick history. Trace records are deliberately excluded
/// from authoritative state and canonical checksums.
/// </summary>
public sealed class SimulationTrace : IRandomRequestTracer
{
    private readonly Queue<SimulationTraceEntry> _entries = new();
    private ActiveTick? _active;

    public SimulationTrace(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Trace capacity must be positive.");
        }

        Capacity = capacity;
    }

    public int Capacity { get; }
    public IReadOnlyList<SimulationTraceEntry> Entries => _entries.ToArray();

    internal void BeginTick(
        SimTick tick,
        int commandsPending,
        int scheduledEventsPending)
    {
        if (_active is not null)
        {
            throw new InvalidOperationException(
                $"Trace tick {_active.Tick} has not been completed.");
        }

        _active = new ActiveTick(tick, commandsPending, scheduledEventsPending);
    }

    internal void RecordSystem(SimPhase phase, SystemId systemId)
        => _active?.Systems.Add(new SystemTraceEntry(phase, systemId));

    internal void RecordCommand(CommandResult result)
        => _active?.Commands.Add(result);

    internal void RecordScheduledEvent(ScheduledEvent scheduledEvent)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);
        _active?.ScheduledEvents.Add(
            new ScheduledEventTraceEntry(
                scheduledEvent.Id,
                scheduledEvent.DueTick,
                scheduledEvent.Phase,
                scheduledEvent.Priority,
                scheduledEvent.CreationSequence,
                scheduledEvent.Data.Type,
                scheduledEvent.Data.Owner,
                scheduledEvent.Data.Payload,
                scheduledEvent.RepeatIntervalTicks));
    }

    public void Record(RandomRequest request)
        => _active?.RandomRequests.Add(request);

    internal void CompleteTick(
        TickChecksum checksum,
        int commandsPending,
        int scheduledEventsPending)
    {
        ArgumentNullException.ThrowIfNull(checksum);
        ActiveTick active = RequireActive(checksum.Tick);
        AddEntry(active.ToEntry(
            commandsPending,
            scheduledEventsPending,
            checksum,
            failure: null));
        _active = null;
    }

    internal void FailTick(
        SimTick tick,
        SimPhase? phase,
        SystemId? systemId,
        Exception exception,
        int commandsPending,
        int scheduledEventsPending)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (_active is null || _active.Tick != tick)
        {
            return;
        }

        string exceptionType = exception.GetType().FullName
            ?? exception.GetType().Name;
        var failure = new SimulationFailureTrace(
            phase,
            systemId,
            exceptionType,
            exception.Message);
        AddEntry(_active.ToEntry(
            commandsPending,
            scheduledEventsPending,
            checksum: null,
            failure: failure));
        _active = null;
    }

    private ActiveTick RequireActive(SimTick tick)
    {
        if (_active is null || _active.Tick != tick)
        {
            throw new InvalidOperationException(
                $"Trace is not recording tick {tick}.");
        }

        return _active;
    }

    private void AddEntry(SimulationTraceEntry entry)
    {
        while (_entries.Count >= Capacity)
        {
            _entries.Dequeue();
        }

        _entries.Enqueue(entry);
    }

    private sealed class ActiveTick
    {
        public ActiveTick(
            SimTick tick,
            int commandsPendingAtStart,
            int scheduledEventsPendingAtStart)
        {
            Tick = tick;
            CommandsPendingAtStart = commandsPendingAtStart;
            ScheduledEventsPendingAtStart = scheduledEventsPendingAtStart;
        }

        public SimTick Tick { get; }
        public int CommandsPendingAtStart { get; }
        public int ScheduledEventsPendingAtStart { get; }
        public List<SystemTraceEntry> Systems { get; } = new();
        public List<CommandResult> Commands { get; } = new();
        public List<ScheduledEventTraceEntry> ScheduledEvents { get; } = new();
        public List<RandomRequest> RandomRequests { get; } = new();

        public SimulationTraceEntry ToEntry(
            int commandsPendingAtEnd,
            int scheduledEventsPendingAtEnd,
            TickChecksum? checksum,
            SimulationFailureTrace? failure)
            => new(
                Tick,
                CommandsPendingAtStart,
                commandsPendingAtEnd,
                ScheduledEventsPendingAtStart,
                scheduledEventsPendingAtEnd,
                Systems.ToArray(),
                Commands.ToArray(),
                ScheduledEvents.ToArray(),
                RandomRequests.ToArray(),
                checksum,
                failure);
    }
}
