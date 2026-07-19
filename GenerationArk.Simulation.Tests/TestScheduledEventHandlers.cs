using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Scheduling;

namespace GenerationArk.Simulation.Tests;

internal sealed class TraceScheduledEventHandler : IScheduledEventHandler
{
    public ScheduledEventTypeId Type { get; } = new("test.trace");

    public void Handle(SimContext context, ScheduledEvent scheduledEvent)
    {
        context.World.AppendTrace(
            $"event:{scheduledEvent.Data.Payload}@{context.Tick}:{context.Phase}");
    }
}
