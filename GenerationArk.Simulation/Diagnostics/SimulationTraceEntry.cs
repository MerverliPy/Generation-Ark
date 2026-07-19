using System.Collections.Generic;
using GenerationArk.Simulation.Commands;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Random;
using GenerationArk.Simulation.Scheduling;

namespace GenerationArk.Simulation.Diagnostics;

public readonly record struct SystemTraceEntry(
    SimPhase Phase,
    SystemId SystemId);

public readonly record struct ScheduledEventTraceEntry(
    ScheduledEventId EventId,
    SimTick DueTick,
    SimPhase Phase,
    int Priority,
    ulong CreationSequence,
    ScheduledEventTypeId Type,
    ScheduledEventOwnerId? Owner,
    string Payload,
    long? RepeatIntervalTicks);

public sealed record SimulationFailureTrace(
    SimPhase? Phase,
    SystemId? SystemId,
    string ExceptionType,
    string Message);

public sealed class SimulationTraceEntry
{
    internal SimulationTraceEntry(
        SimTick tick,
        int commandsPendingAtStart,
        int commandsPendingAtEnd,
        int scheduledEventsPendingAtStart,
        int scheduledEventsPendingAtEnd,
        IReadOnlyList<SystemTraceEntry> systems,
        IReadOnlyList<CommandResult> commands,
        IReadOnlyList<ScheduledEventTraceEntry> scheduledEvents,
        IReadOnlyList<RandomRequest> randomRequests,
        TickChecksum? checksum,
        SimulationFailureTrace? failure)
    {
        Tick = tick;
        CommandsPendingAtStart = commandsPendingAtStart;
        CommandsPendingAtEnd = commandsPendingAtEnd;
        ScheduledEventsPendingAtStart = scheduledEventsPendingAtStart;
        ScheduledEventsPendingAtEnd = scheduledEventsPendingAtEnd;
        Systems = systems;
        Commands = commands;
        ScheduledEvents = scheduledEvents;
        RandomRequests = randomRequests;
        Checksum = checksum;
        Failure = failure;
    }

    public SimTick Tick { get; }
    public int CommandsPendingAtStart { get; }
    public int CommandsPendingAtEnd { get; }
    public int ScheduledEventsPendingAtStart { get; }
    public int ScheduledEventsPendingAtEnd { get; }
    public IReadOnlyList<SystemTraceEntry> Systems { get; }
    public IReadOnlyList<CommandResult> Commands { get; }
    public IReadOnlyList<ScheduledEventTraceEntry> ScheduledEvents { get; }
    public IReadOnlyList<RandomRequest> RandomRequests { get; }
    public TickChecksum? Checksum { get; }
    public SimulationFailureTrace? Failure { get; }
}
