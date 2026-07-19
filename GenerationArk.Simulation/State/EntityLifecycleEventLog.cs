using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.State;

public sealed class EntityLifecycleEventLog
{
    private readonly Queue<EntityLifecycleEvent> _events = new();
    private ulong _nextEventSequence = 1;

    public EntityLifecycleEventLog(int retentionCapacity = 2048)
    {
        if (retentionCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retentionCapacity),
                retentionCapacity,
                "Lifecycle-event retention capacity must be positive.");
        }

        RetentionCapacity = retentionCapacity;
    }

    public int RetentionCapacity { get; }
    public int Count => _events.Count;
    public IReadOnlyList<EntityLifecycleEvent> Events => _events.ToArray();

    internal EntityLifecycleEvent Add(
        ulong mutationSequence,
        long tick,
        EntityLifecycleEventKind kind,
        EntityId entityId,
        ComponentTypeId? componentTypeId,
        string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ulong followingSequence = checked(_nextEventSequence + 1UL);
        var lifecycleEvent = new EntityLifecycleEvent(
            _nextEventSequence,
            mutationSequence,
            tick,
            kind,
            entityId,
            componentTypeId,
            reasonCode);
        _nextEventSequence = followingSequence;
        _events.Enqueue(lifecycleEvent);
        while (_events.Count > RetentionCapacity)
        {
            _events.Dequeue();
        }
        return lifecycleEvent;
    }
}
