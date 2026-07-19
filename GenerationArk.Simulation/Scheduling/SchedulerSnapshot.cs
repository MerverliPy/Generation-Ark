namespace GenerationArk.Simulation.Scheduling;

public sealed record SchedulerSnapshot(
    long CurrentTick,
    ulong NextEventId,
    ulong NextCreationSequence,
    ScheduledEventSnapshot[] Events);
