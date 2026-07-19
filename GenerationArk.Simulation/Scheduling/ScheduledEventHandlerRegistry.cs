using System;
using System.Collections.Generic;

namespace GenerationArk.Simulation.Scheduling;

public sealed class ScheduledEventHandlerRegistry
{
    private readonly SortedDictionary<ScheduledEventTypeId, IScheduledEventHandler> _handlers = new();

    public ScheduledEventHandlerRegistry(IEnumerable<IScheduledEventHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        foreach (IScheduledEventHandler handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (!_handlers.TryAdd(handler.Type, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate scheduled-event handler type '{handler.Type}'.");
            }
        }
    }

    public IScheduledEventHandler Get(ScheduledEventTypeId type)
        => _handlers.TryGetValue(type, out IScheduledEventHandler? handler)
            ? handler
            : throw new InvalidOperationException(
                $"No handler is registered for scheduled-event type '{type}'.");
}
