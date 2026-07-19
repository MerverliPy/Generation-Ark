using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Scheduling;

public interface ISimScheduler
{
    SimTick CurrentTick { get; }
    SimPhase? CurrentPhase { get; }
    int PendingCount { get; }

    ScheduledEventId ScheduleAt(
        SimTick dueTick,
        SimPhase phase,
        int priority,
        ScheduledEventData eventData);

    ScheduledEventId ScheduleAfter(
        long delayTicks,
        SimPhase phase,
        int priority,
        ScheduledEventData eventData);

    ScheduledEventId ScheduleRepeating(
        SimTick firstTick,
        long intervalTicks,
        SimPhase phase,
        int priority,
        ScheduledEventData eventData);

    bool Cancel(ScheduledEventId eventId);
    int CancelOwnedBy(ScheduledEventOwnerId owner);
    SchedulerSnapshot CaptureSnapshot();
}
