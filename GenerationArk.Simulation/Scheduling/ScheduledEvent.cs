using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Scheduling;

public sealed record ScheduledEvent(
    ScheduledEventId Id,
    SimTick DueTick,
    SimPhase Phase,
    int Priority,
    ulong CreationSequence,
    ScheduledEventData Data,
    long? RepeatIntervalTicks)
{
    public ScheduledEventSnapshot ToSnapshot()
        => new(
            Id.Value,
            DueTick.Value,
            (byte)Phase,
            Priority,
            CreationSequence,
            Data.Type.Value,
            Data.Owner?.Value,
            Data.Payload,
            RepeatIntervalTicks);
}
