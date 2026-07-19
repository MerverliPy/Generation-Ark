using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.Scheduling;

namespace GenerationArk.Simulation.State;

/// <summary>
/// Engine-neutral deterministic authoritative state.
/// </summary>
public sealed class WorldState
{
    private readonly SortedDictionary<string, long> _counters = new(StringComparer.Ordinal);
    private readonly List<string> _trace = new();

    public WorldState(
        IEnumerable<ComponentRegistration>? componentRegistrations = null,
        IEnumerable<IEntityCleanupHook>? cleanupHooks = null,
        int lifecycleEventRetentionCapacity = 2048,
        MapState? map = null)
    {
        Entities = new EntityRegistry();
        Components = new ComponentRegistry(componentRegistrations);
        Mutations = new MutationBuffer();
        CleanupHooks = new EntityCleanupHookRegistry(cleanupHooks);
        LifecycleEvents = new EntityLifecycleEventLog(lifecycleEventRetentionCapacity);
        Map = map ?? MapState.CreateDefault();
    }

    public IReadOnlyDictionary<string, long> Counters => _counters;
    public IReadOnlyList<string> Trace => _trace;
    public EntityRegistry Entities { get; }
    public ComponentRegistry Components { get; }
    public MutationBuffer Mutations { get; }
    public EntityCleanupHookRegistry CleanupHooks { get; }
    public EntityLifecycleEventLog LifecycleEvents { get; }
    public MapState Map { get; }

    public long GetCounter(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _counters.TryGetValue(key, out long value) ? value : 0;
    }

    public void SetCounter(string key, long value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _counters[key] = value;
    }

    public void AddToCounter(string key, long delta)
        => SetCounter(key, checked(GetCounter(key) + delta));

    public void AppendTrace(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _trace.Add(value);
    }

    public IReadOnlyList<EntityId> ActivatePendingEntities(SimTick tick)
    {
        IReadOnlyList<EntityId> activated = Entities.ActivatePending();
        foreach (EntityId entityId in activated)
        {
            RecordLifecycleEvent(
                mutationSequence: 0,
                tick,
                EntityLifecycleEventKind.EntityActivated,
                entityId,
                componentTypeId: null,
                "activated");
        }
        return activated;
    }

    public MutationCommitResult CommitMutations(
        ISimScheduler scheduler,
        SimTick tick)
        => Mutations.Commit(this, scheduler, tick);

    public MutationCommitResult CommitEntityMutations(
        ISimScheduler scheduler,
        SimTick tick)
        => CommitMutations(scheduler, tick);

    public void ValidateEntityInvariants(ISimScheduler? scheduler = null)
    {
        Entities.ValidateInvariants();
        Components.ValidateEntityOwnership(Entities);
        if (Mutations.Count != 0)
        {
            throw new InvalidOperationException(
                $"Structural mutation buffer is not empty after Commit: {Mutations.Count} pending request(s).");
        }
        Map.ValidateInvariants();

        if (scheduler is null || Entities.RetiredCount == 0)
        {
            return;
        }

        foreach (ScheduledEventSnapshot scheduledEvent in scheduler.CaptureSnapshot().Events)
        {
            if (scheduledEvent.Owner is ulong owner
                && Entities.IsRetired(new EntityId(owner)))
            {
                throw new InvalidOperationException(
                    $"Scheduled event {scheduledEvent.Id} remains owned by destroyed entity {owner}.");
            }
        }
    }

    public void WriteEntityChecksum(StateChecksumWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.AddUInt64(Entities.NextEntityId);
        writer.AddInt32(Entities.RetiredCount);
        foreach (EntityId retiredEntityId in Entities.RetiredEntityIds)
        {
            writer.AddUInt64(retiredEntityId.Value);
        }

        writer.AddInt32(Entities.LiveCount);
        foreach (EntityId entityId in Entities.AllEntityIds)
        {
            writer.AddUInt64(entityId.Value);
            writer.AddByte((byte)Entities.GetLifecycleState(entityId));
            Components.WriteEntityChecksum(writer, entityId);
        }
    }

    internal void RestoreEntityState(
        ulong nextEntityId,
        IEnumerable<KeyValuePair<EntityId, EntityLifecycleState>> liveEntities,
        IEnumerable<EntityId> retiredEntityIds,
        IEnumerable<(EntityId EntityId, ComponentTypeId ComponentTypeId, string Payload)> components)
    {
        ArgumentNullException.ThrowIfNull(liveEntities);
        ArgumentNullException.ThrowIfNull(retiredEntityIds);
        ArgumentNullException.ThrowIfNull(components);
        if (Mutations.Count != 0)
        {
            throw new InvalidOperationException(
                "Cannot restore entity state while structural mutations are pending.");
        }

        KeyValuePair<EntityId, EntityLifecycleState>[] liveArray = liveEntities.ToArray();
        EntityId[] retiredArray = retiredEntityIds.ToArray();
        (EntityId EntityId, ComponentTypeId ComponentTypeId, string Payload)[] componentArray = components.ToArray();

        var validationRegistry = new EntityRegistry();
        validationRegistry.Restore(nextEntityId, liveArray, retiredArray);
        var seenComponents = new HashSet<(EntityId EntityId, ComponentTypeId ComponentTypeId)>();
        var validatedComponents = new List<(EntityId EntityId, ComponentTypeId ComponentTypeId, object Value)>();
        foreach ((EntityId entityId, ComponentTypeId componentTypeId, string payload) in componentArray)
        {
            if (!validationRegistry.Contains(entityId))
            {
                throw new InvalidOperationException(
                    $"Component {componentTypeId} targets missing entity {entityId}.");
            }
            if (!Components.IsRegistered(componentTypeId))
            {
                throw new InvalidOperationException(
                    $"Unknown required component type ID {componentTypeId}.");
            }
            if (!seenComponents.Add((entityId, componentTypeId)))
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} contains duplicate component {componentTypeId}.");
            }
            object value = Components.GetStore(componentTypeId).Deserialize(payload);
            validatedComponents.Add((entityId, componentTypeId, value));
        }

        if (Components.Stores.Any(static store => store.Count != 0))
        {
            throw new InvalidOperationException(
                "Cannot restore entity state into non-empty component stores.");
        }

        Entities.Restore(nextEntityId, liveArray, retiredArray);
        foreach ((EntityId entityId, ComponentTypeId componentTypeId, object value) in validatedComponents
                     .OrderBy(static item => item.EntityId)
                     .ThenBy(static item => item.ComponentTypeId))
        {
            Components.Add(Entities, entityId, new ComponentValue(componentTypeId, value));
        }
        ValidateEntityInvariants();
    }

    internal void RecordLifecycleEvent(
        ulong mutationSequence,
        SimTick tick,
        EntityLifecycleEventKind kind,
        EntityId entityId,
        ComponentTypeId? componentTypeId,
        string reasonCode)
        => LifecycleEvents.Add(
            mutationSequence,
            tick.Value,
            kind,
            entityId,
            componentTypeId,
            reasonCode);
}
