namespace GenerationArk.Simulation.Scheduling;

public sealed record ScheduledEventSnapshot(
    ulong Id,
    long DueTick,
    byte Phase,
    int Priority,
    ulong CreationSequence,
    string Type,
    ulong? Owner,
    string Payload,
    long? RepeatIntervalTicks);
