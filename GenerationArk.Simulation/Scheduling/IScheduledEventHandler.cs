using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Scheduling;

public interface IScheduledEventHandler
{
    ScheduledEventTypeId Type { get; }

    void Handle(SimContext context, ScheduledEvent scheduledEvent);
}
