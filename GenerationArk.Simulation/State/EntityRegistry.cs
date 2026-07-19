using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.State;

public sealed class EntityRegistry
{
    private readonly SortedDictionary<EntityId, EntityLifecycleState> _live = new();
    private readonly SortedSet<EntityId> _retired = new();

    public ulong NextEntityId { get; private set; } = 1;
    public int LiveCount => _live.Count;
    public int RetiredCount => _retired.Count;
    public int ActiveCount => _live.Count(static pair => pair.Value == EntityLifecycleState.Active);
    public int PendingActivationCount => _live.Count(static pair => pair.Value == EntityLifecycleState.PendingActivation);

    public IReadOnlyList<EntityId> AllEntityIds => _live.Keys.ToArray();
    public IReadOnlyList<EntityId> ActiveEntityIds => _live
        .Where(static pair => pair.Value == EntityLifecycleState.Active)
        .Select(static pair => pair.Key)
        .ToArray();
    public IReadOnlyList<EntityId> PendingActivationEntityIds => _live
        .Where(static pair => pair.Value == EntityLifecycleState.PendingActivation)
        .Select(static pair => pair.Key)
        .ToArray();
    public IReadOnlyList<EntityId> RetiredEntityIds => _retired.ToArray();

    public bool Contains(EntityId entityId) => _live.ContainsKey(entityId);
    public bool IsRetired(EntityId entityId) => _retired.Contains(entityId);

    public EntityLifecycleState GetLifecycleState(EntityId entityId)
    {
        if (!_live.TryGetValue(entityId, out EntityLifecycleState state))
        {
            throw new KeyNotFoundException($"Entity {entityId} does not exist.");
        }

        return state;
    }

    internal EntityId ApplyCreate(EntityId expectedId)
    {
        if (expectedId == EntityId.None || expectedId.Value != NextEntityId)
        {
            throw new InvalidOperationException(
                $"Entity allocation expected ID {NextEntityId}, got {expectedId.Value}.");
        }

        ulong followingId = checked(NextEntityId + 1UL);
        if (_live.ContainsKey(expectedId) || _retired.Contains(expectedId))
        {
            throw new InvalidOperationException($"Entity ID {expectedId} has already been assigned.");
        }

        _live.Add(expectedId, EntityLifecycleState.PendingActivation);
        NextEntityId = followingId;
        return expectedId;
    }

    internal IReadOnlyList<EntityId> ActivatePending()
    {
        EntityId[] pending = PendingActivationEntityIds.ToArray();
        foreach (EntityId entityId in pending)
        {
            _live[entityId] = EntityLifecycleState.Active;
        }

        return pending;
    }

    internal void Destroy(EntityId entityId)
    {
        if (!_live.Remove(entityId))
        {
            throw new InvalidOperationException($"Cannot destroy missing entity {entityId}.");
        }

        if (!_retired.Add(entityId))
        {
            throw new InvalidOperationException($"Entity ID {entityId} was already retired.");
        }
    }

    internal void Restore(
        ulong nextEntityId,
        IEnumerable<KeyValuePair<EntityId, EntityLifecycleState>> liveEntities,
        IEnumerable<EntityId> retiredEntityIds)
    {
        ArgumentNullException.ThrowIfNull(liveEntities);
        ArgumentNullException.ThrowIfNull(retiredEntityIds);
        if (nextEntityId == 0)
        {
            throw new InvalidOperationException("Next entity ID cannot be zero.");
        }

        var live = new SortedDictionary<EntityId, EntityLifecycleState>();
        var retired = new SortedSet<EntityId>();
        ulong maximumAssigned = 0;

        foreach ((EntityId entityId, EntityLifecycleState lifecycleState) in liveEntities)
        {
            ValidateAssignedId(entityId, nextEntityId);
            if (lifecycleState is not EntityLifecycleState.PendingActivation and not EntityLifecycleState.Active)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} has unknown lifecycle state {(byte)lifecycleState}.");
            }
            if (!live.TryAdd(entityId, lifecycleState))
            {
                throw new InvalidOperationException($"Duplicate live entity ID {entityId}.");
            }
            maximumAssigned = Math.Max(maximumAssigned, entityId.Value);
        }

        foreach (EntityId entityId in retiredEntityIds)
        {
            ValidateAssignedId(entityId, nextEntityId);
            if (live.ContainsKey(entityId))
            {
                throw new InvalidOperationException(
                    $"Entity ID {entityId} cannot be both live and retired.");
            }
            if (!retired.Add(entityId))
            {
                throw new InvalidOperationException($"Duplicate retired entity ID {entityId}.");
            }
            maximumAssigned = Math.Max(maximumAssigned, entityId.Value);
        }

        if (nextEntityId <= maximumAssigned)
        {
            throw new InvalidOperationException(
                "Next entity ID is not greater than every assigned entity ID.");
        }

        _live.Clear();
        _retired.Clear();
        foreach ((EntityId entityId, EntityLifecycleState lifecycleState) in live)
        {
            _live.Add(entityId, lifecycleState);
        }
        foreach (EntityId entityId in retired)
        {
            _retired.Add(entityId);
        }
        NextEntityId = nextEntityId;
    }

    internal void ValidateInvariants()
    {
        ulong maximumAssigned = 0;
        foreach ((EntityId entityId, EntityLifecycleState lifecycleState) in _live)
        {
            if (entityId == EntityId.None)
            {
                throw new InvalidOperationException("A live entity has the reserved None ID.");
            }
            if (lifecycleState is not EntityLifecycleState.PendingActivation and not EntityLifecycleState.Active)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} has unknown lifecycle state {(byte)lifecycleState}.");
            }
            if (_retired.Contains(entityId))
            {
                throw new InvalidOperationException(
                    $"Entity ID {entityId} is both live and retired.");
            }
            maximumAssigned = Math.Max(maximumAssigned, entityId.Value);
        }

        foreach (EntityId entityId in _retired)
        {
            if (entityId == EntityId.None)
            {
                throw new InvalidOperationException("The reserved None entity ID was retired.");
            }
            maximumAssigned = Math.Max(maximumAssigned, entityId.Value);
        }

        if (NextEntityId == 0 || NextEntityId <= maximumAssigned)
        {
            throw new InvalidOperationException(
                "Next entity ID is not greater than every assigned entity ID.");
        }
    }

    private static void ValidateAssignedId(EntityId entityId, ulong nextEntityId)
    {
        if (entityId == EntityId.None)
        {
            throw new InvalidOperationException("Entity ID zero is reserved.");
        }
        if (entityId.Value >= nextEntityId)
        {
            throw new InvalidOperationException(
                $"Assigned entity ID {entityId} must be less than next entity ID {nextEntityId}.");
        }
    }
}
