using System;

namespace GenerationArk.Simulation.Scheduling;

public sealed record ScheduledEventData
{
    public ScheduledEventData(
        ScheduledEventTypeId type,
        ScheduledEventOwnerId? owner,
        string payload)
    {
        Type = type;
        Owner = owner;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public ScheduledEventTypeId Type { get; }
    public ScheduledEventOwnerId? Owner { get; }
    public string Payload { get; }
}
