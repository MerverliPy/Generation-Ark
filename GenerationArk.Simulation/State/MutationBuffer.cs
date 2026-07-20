using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.Scheduling;

namespace GenerationArk.Simulation.State;

public sealed class MutationBuffer
{
    private readonly List<EntityMutation> _mutations = new();
    private readonly List<MapCellMutation> _mapMutations = new();
    private readonly List<PositionMutation> _positionMutations = new();
    private ulong _nextMutationSequence = 1;

    public int Count => _mutations.Count + _mapMutations.Count + _positionMutations.Count;
    public int EntityMutationCount => _mutations.Count;
    public int MapMutationCount => _mapMutations.Count;
    public int PositionMutationCount => _positionMutations.Count;

    public IReadOnlyList<EntityMutation> PendingMutations => _mutations
        .OrderBy(static mutation => mutation, EntityMutationComparer.Instance)
        .ToArray();

    public IReadOnlyList<MapCellMutation> PendingMapMutations => _mapMutations
        .OrderBy(static mutation => mutation, MapCellMutationComparer.Instance)
        .ToArray();

    public IReadOnlyList<PositionMutation> PendingPositionMutations => _positionMutations
        .OrderBy(static mutation => mutation, PositionMutationComparer.Instance)
        .ToArray();

    public EntityMutation EnqueueCreate(IEnumerable<ComponentValue>? initialComponents = null)
        => Add(new EntityMutation(
            AllocateSequence(),
            EntityMutationKind.CreateEntity,
            EntityId.None,
            component: null,
            componentTypeId: null,
            initialComponents));

    public EntityMutation EnqueueDestroy(EntityId entityId)
    {
        RequireEntityId(entityId);
        return Add(new EntityMutation(
            AllocateSequence(),
            EntityMutationKind.DestroyEntity,
            entityId,
            component: null,
            componentTypeId: null,
            initialComponents: null));
    }

    public EntityMutation EnqueueAdd(EntityId entityId, ComponentValue component)
    {
        RequireEntityId(entityId);
        ArgumentNullException.ThrowIfNull(component);
        return Add(new EntityMutation(
            AllocateSequence(),
            EntityMutationKind.AddComponent,
            entityId,
            component,
            component.ComponentTypeId,
            initialComponents: null));
    }

    public EntityMutation EnqueueRemove(EntityId entityId, ComponentTypeId componentTypeId)
    {
        RequireEntityId(entityId);
        return Add(new EntityMutation(
            AllocateSequence(),
            EntityMutationKind.RemoveComponent,
            entityId,
            component: null,
            componentTypeId,
            initialComponents: null));
    }

    public MapCellMutation EnqueueSetCellDefinition(
        MapCellId cell,
        MapCellDefinitionId definition)
        => AddMap(new MapCellMutation(
            AllocateSequence(),
            MapCellMutationKind.SetCellDefinition,
            cell,
            definition));

    public PositionMutation EnqueueSetPosition(EntityId entityId, EntityPosition position)
    {
        RequireEntityId(entityId);
        return AddPosition(new PositionMutation(AllocateSequence(), entityId, position));
    }

    public PositionMutation EnqueueClearPosition(EntityId entityId)
    {
        RequireEntityId(entityId);
        return AddPosition(new PositionMutation(AllocateSequence(), entityId, Position: null));
    }

    internal MutationCommitResult Commit(
        WorldState world,
        ISimScheduler scheduler,
        SimTick tick)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        if (Count == 0)
        {
            return new MutationCommitResult(0, 0, Array.Empty<EntityId>());
        }

        EntityMutation[] ordered = _mutations
            .OrderBy(static mutation => mutation, EntityMutationComparer.Instance)
            .ToArray();
        MapCellMutation[] orderedMap = _mapMutations
            .OrderBy(static mutation => mutation, MapCellMutationComparer.Instance)
            .ToArray();
        PositionMutation[] orderedPositions = _positionMutations
            .OrderBy(static mutation => mutation, PositionMutationComparer.Instance)
            .ToArray();

        ValidateUniqueSequences(ordered);
        ValidateUniqueMapSequences(orderedMap);
        ValidateUniquePositionSequences(orderedPositions);
        ValidateUniqueCombinedSequences(ordered, orderedMap, orderedPositions);
        Dictionary<ulong, EntityId> assignedCreateIds = ordered.Length == 0
            ? new Dictionary<ulong, EntityId>()
            : ValidateBatch(world, ordered, tick);
        world.Map.ValidateMutationBatch(orderedMap);
        ValidatePositionBatch(world, orderedPositions, ordered);

        var created = new List<EntityId>();
        int cancelledEvents = 0;
        foreach (EntityMutation mutation in ordered)
        {
            switch (mutation.Kind)
            {
                case EntityMutationKind.CreateEntity:
                {
                    EntityId entityId = assignedCreateIds[mutation.MutationSequence];
                    world.Entities.ApplyCreate(entityId);
                    created.Add(entityId);
                    world.RecordLifecycleEvent(
                        mutation.MutationSequence,
                        tick,
                        EntityLifecycleEventKind.EntityCreated,
                        entityId,
                        componentTypeId: null,
                        "created");
                    foreach (ComponentValue component in mutation.InitialComponents)
                    {
                        world.Components.Add(world.Entities, entityId, component);
                        world.RecordLifecycleEvent(
                            mutation.MutationSequence,
                            tick,
                            EntityLifecycleEventKind.ComponentAdded,
                            entityId,
                            component.ComponentTypeId,
                            "initial-component");
                    }
                    break;
                }
                case EntityMutationKind.DestroyEntity:
                {
                    EntityId entityId = mutation.EntityId;
                    world.Entities.Destroy(entityId);
                    world.Spatial.ApplyClear(entityId);
                    foreach (ComponentTypeId componentTypeId in world.Components.RemoveAll(entityId))
                    {
                        world.RecordLifecycleEvent(
                            mutation.MutationSequence,
                            tick,
                            EntityLifecycleEventKind.ComponentRemoved,
                            entityId,
                            componentTypeId,
                            "entity-destroyed");
                    }
                    cancelledEvents += scheduler.CancelOwnedBy(
                        new ScheduledEventOwnerId(entityId.Value));
                    world.CleanupHooks.Cleanup(entityId, world);
                    world.RecordLifecycleEvent(
                        mutation.MutationSequence,
                        tick,
                        EntityLifecycleEventKind.EntityDestroyed,
                        entityId,
                        componentTypeId: null,
                        "destroyed");
                    break;
                }
                case EntityMutationKind.AddComponent:
                {
                    ComponentValue component = mutation.Component
                        ?? throw new InvalidOperationException(
                            $"Mutation {mutation.MutationSequence} is missing its component value.");
                    world.Components.Add(world.Entities, mutation.EntityId, component);
                    world.RecordLifecycleEvent(
                        mutation.MutationSequence,
                        tick,
                        EntityLifecycleEventKind.ComponentAdded,
                        mutation.EntityId,
                        component.ComponentTypeId,
                        "component-added");
                    break;
                }
                case EntityMutationKind.RemoveComponent:
                {
                    ComponentTypeId componentTypeId = mutation.ComponentTypeId
                        ?? throw new InvalidOperationException(
                            $"Mutation {mutation.MutationSequence} is missing its component type ID.");
                    world.Components.Remove(world.Entities, mutation.EntityId, componentTypeId);
                    world.RecordLifecycleEvent(
                        mutation.MutationSequence,
                        tick,
                        EntityLifecycleEventKind.ComponentRemoved,
                        mutation.EntityId,
                        componentTypeId,
                        "component-removed");
                    break;
                }
                default:
                    throw new InvalidOperationException(
                        $"Unknown entity mutation kind {(byte)mutation.Kind}.");
            }
        }

        world.Map.ApplyValidatedMutations(orderedMap);
        foreach (PositionMutation mutation in orderedPositions)
        {
            if (mutation.Position is EntityPosition position)
            {
                world.Spatial.ApplySet(mutation.EntityId, position);
            }
            else
            {
                world.Spatial.ApplyClear(mutation.EntityId);
            }
        }
        _mutations.Clear();
        _mapMutations.Clear();
        _positionMutations.Clear();
        return new MutationCommitResult(
            ordered.Length + orderedMap.Length + orderedPositions.Length,
            cancelledEvents,
            created.ToArray(),
            orderedPositions.Length);
    }

    private Dictionary<ulong, EntityId> ValidateBatch(
        WorldState world,
        EntityMutation[] ordered,
        SimTick tick)
    {
        IReadOnlyList<EntityMutation> conflicts = FindConflicts(ordered);
        if (conflicts.Count > 0)
        {
            Reject(world, tick, "conflicting-mutation-batch", conflicts);
        }

        var simulatedEntities = world.Entities.AllEntityIds.ToDictionary(
            static entityId => entityId,
            entityId => world.Entities.GetLifecycleState(entityId));
        var simulatedComponents = new HashSet<(EntityId EntityId, ComponentTypeId ComponentTypeId)>();
        foreach (IComponentStore store in world.Components.Stores)
        {
            foreach (EntityId entityId in store.EntityIds)
            {
                simulatedComponents.Add((entityId, store.ComponentTypeId));
            }
        }

        ulong nextEntityId = world.Entities.NextEntityId;
        var assignedCreateIds = new Dictionary<ulong, EntityId>();
        foreach (EntityMutation mutation in ordered)
        {
            try
            {
                switch (mutation.Kind)
                {
                    case EntityMutationKind.CreateEntity:
                    {
                        ulong followingId = checked(nextEntityId + 1UL);
                        var entityId = new EntityId(nextEntityId);
                        if (entityId == EntityId.None
                            || simulatedEntities.ContainsKey(entityId)
                            || world.Entities.IsRetired(entityId))
                        {
                            throw new InvalidOperationException(
                                $"Entity ID {entityId} is not available for creation.");
                        }

                        var initialIds = new HashSet<ComponentTypeId>();
                        foreach (ComponentValue component in mutation.InitialComponents)
                        {
                            ValidateComponent(world, component);
                            if (!initialIds.Add(component.ComponentTypeId))
                            {
                                throw new InvalidOperationException(
                                    $"Create mutation {mutation.MutationSequence} contains duplicate component {component.ComponentTypeId}.");
                            }
                            simulatedComponents.Add((entityId, component.ComponentTypeId));
                        }

                        simulatedEntities.Add(entityId, EntityLifecycleState.PendingActivation);
                        assignedCreateIds.Add(mutation.MutationSequence, entityId);
                        nextEntityId = followingId;
                        break;
                    }
                    case EntityMutationKind.DestroyEntity:
                        RequireSimulatedEntity(simulatedEntities, mutation);
                        simulatedEntities.Remove(mutation.EntityId);
                        simulatedComponents.RemoveWhere(pair => pair.EntityId == mutation.EntityId);
                        break;
                    case EntityMutationKind.AddComponent:
                    {
                        RequireSimulatedEntity(simulatedEntities, mutation);
                        ComponentValue component = mutation.Component
                            ?? throw new InvalidOperationException(
                                $"Mutation {mutation.MutationSequence} is missing its component value.");
                        ValidateComponent(world, component);
                        if (!simulatedComponents.Add((mutation.EntityId, component.ComponentTypeId)))
                        {
                            throw new InvalidOperationException(
                                $"Entity {mutation.EntityId} already has component {component.ComponentTypeId}.");
                        }
                        break;
                    }
                    case EntityMutationKind.RemoveComponent:
                    {
                        RequireSimulatedEntity(simulatedEntities, mutation);
                        ComponentTypeId componentTypeId = mutation.ComponentTypeId
                            ?? throw new InvalidOperationException(
                                $"Mutation {mutation.MutationSequence} is missing its component type ID.");
                        if (!world.Components.IsRegistered(componentTypeId))
                        {
                            throw new InvalidOperationException(
                                $"Unknown component type ID {componentTypeId}.");
                        }
                        if (!simulatedComponents.Remove((mutation.EntityId, componentTypeId)))
                        {
                            throw new InvalidOperationException(
                                $"Entity {mutation.EntityId} does not have component {componentTypeId}.");
                        }
                        break;
                    }
                    default:
                        throw new InvalidOperationException(
                            $"Unknown entity mutation kind {(byte)mutation.Kind}.");
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or OverflowException)
            {
                Reject(
                    world,
                    tick,
                    $"mutation-{mutation.MutationSequence}-invalid: {exception.Message}",
                    new[] { mutation });
            }
        }

        return assignedCreateIds;
    }

    private static IReadOnlyList<EntityMutation> FindConflicts(EntityMutation[] ordered)
    {
        var conflicts = new SortedDictionary<ulong, EntityMutation>();
        foreach (IGrouping<EntityId, EntityMutation> group in ordered
                     .Where(static mutation => mutation.Kind != EntityMutationKind.CreateEntity)
                     .GroupBy(static mutation => mutation.EntityId))
        {
            EntityMutation[] destroys = group
                .Where(static mutation => mutation.Kind == EntityMutationKind.DestroyEntity)
                .ToArray();
            EntityMutation[] componentChanges = group
                .Where(static mutation => mutation.Kind is EntityMutationKind.AddComponent or EntityMutationKind.RemoveComponent)
                .ToArray();
            if (destroys.Length > 1 || (destroys.Length > 0 && componentChanges.Length > 0))
            {
                foreach (EntityMutation mutation in destroys.Concat(componentChanges))
                {
                    conflicts[mutation.MutationSequence] = mutation;
                }
            }
        }

        foreach (IGrouping<(EntityId EntityId, ComponentTypeId ComponentTypeId), EntityMutation> group in ordered
                     .Where(static mutation => mutation.Kind is EntityMutationKind.AddComponent or EntityMutationKind.RemoveComponent)
                     .GroupBy(static mutation => (
                         mutation.EntityId,
                         mutation.ComponentTypeId
                             ?? mutation.Component?.ComponentTypeId
                             ?? default)))
        {
            int additions = group.Count(static mutation => mutation.Kind == EntityMutationKind.AddComponent);
            int removals = group.Count(static mutation => mutation.Kind == EntityMutationKind.RemoveComponent);
            if (additions > 1 || removals > 1 || (additions > 0 && removals > 0))
            {
                foreach (EntityMutation mutation in group)
                {
                    conflicts[mutation.MutationSequence] = mutation;
                }
            }
        }

        return conflicts.Values.ToArray();
    }

    private void Reject(
        WorldState world,
        SimTick tick,
        string reasonCode,
        IEnumerable<EntityMutation> conflictingMutations)
    {
        EntityMutation[] conflicts = conflictingMutations
            .OrderBy(static mutation => mutation, EntityMutationComparer.Instance)
            .ToArray();
        EntityMutation first = conflicts[0];
        world.RecordLifecycleEvent(
            first.MutationSequence,
            tick,
            EntityLifecycleEventKind.MutationRejected,
            first.EntityId,
            first.ComponentTypeId ?? first.Component?.ComponentTypeId,
            reasonCode);
        string sequences = string.Join(",", conflicts.Select(static mutation => mutation.MutationSequence));
        throw new MutationValidationException(
            $"Entity mutation batch rejected. Conflicting mutation sequences: {sequences}. Reason: {reasonCode}",
            conflicts);
    }

    private static void ValidateComponent(WorldState world, ComponentValue component)
    {
        if (!world.Components.IsRegistered(component.ComponentTypeId))
        {
            throw new InvalidOperationException(
                $"Unknown component type ID {component.ComponentTypeId}.");
        }
        IComponentStore store = world.Components.GetStore(component.ComponentTypeId);
        if (component.Value.GetType() != store.RuntimeType)
        {
            throw new InvalidOperationException(
                $"Component {component.ComponentTypeId} requires runtime type {store.RuntimeType.FullName}, got {component.Value.GetType().FullName}.");
        }
    }

    private static void RequireSimulatedEntity(
        IReadOnlyDictionary<EntityId, EntityLifecycleState> simulatedEntities,
        EntityMutation mutation)
    {
        if (!simulatedEntities.ContainsKey(mutation.EntityId))
        {
            throw new InvalidOperationException(
                $"Mutation {mutation.MutationSequence} targets missing entity {mutation.EntityId}.");
        }
    }

    private static void ValidateUniqueSequences(EntityMutation[] ordered)
    {
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].MutationSequence == ordered[index].MutationSequence)
            {
                throw new InvalidOperationException(
                    $"Duplicate entity mutation sequence {ordered[index].MutationSequence}.");
            }
        }
    }

    private static void ValidateUniqueMapSequences(MapCellMutation[] ordered)
    {
        ulong[] sequences = ordered
            .Select(static mutation => mutation.MutationSequence)
            .OrderBy(static sequence => sequence)
            .ToArray();
        for (int index = 1; index < sequences.Length; index++)
        {
            if (sequences[index - 1] == sequences[index])
            {
                throw new InvalidOperationException(
                    $"Duplicate map mutation sequence {sequences[index]}.");
            }
        }
    }

    private static void ValidateUniquePositionSequences(PositionMutation[] ordered)
    {
        ulong[] sequences = ordered
            .Select(static mutation => mutation.MutationSequence)
            .OrderBy(static sequence => sequence)
            .ToArray();
        for (int index = 1; index < sequences.Length; index++)
        {
            if (sequences[index - 1] == sequences[index])
            {
                throw new InvalidOperationException(
                    $"Duplicate position mutation sequence {sequences[index]}.");
            }
        }
    }

    private static void ValidateUniqueCombinedSequences(
        IEnumerable<EntityMutation> entityMutations,
        IEnumerable<MapCellMutation> mapMutations,
        IEnumerable<PositionMutation> positionMutations)
    {
        ulong[] ordered = entityMutations.Select(static mutation => mutation.MutationSequence)
            .Concat(mapMutations.Select(static mutation => mutation.MutationSequence))
            .Concat(positionMutations.Select(static mutation => mutation.MutationSequence))
            .OrderBy(static sequence => sequence)
            .ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1] == ordered[index])
            {
                throw new InvalidOperationException(
                    $"Duplicate mutation sequence {ordered[index]} across mutation kinds.");
            }
        }
    }

    private static void ValidatePositionBatch(
        WorldState world,
        PositionMutation[] orderedPositions,
        EntityMutation[] orderedEntities)
    {
        var conflicts = new List<PositionMutation>();
        for (int index = 1; index < orderedPositions.Length; index++)
        {
            if (orderedPositions[index - 1].EntityId == orderedPositions[index].EntityId)
            {
                if (conflicts.Count == 0 || conflicts[^1] != orderedPositions[index - 1])
                {
                    conflicts.Add(orderedPositions[index - 1]);
                }
                conflicts.Add(orderedPositions[index]);
            }
        }
        if (conflicts.Count > 0)
        {
            RejectPosition(
                "Position mutation batch rejected. Multiple operations target one entity.",
                conflicts);
        }

        var destroyed = new HashSet<EntityId>(orderedEntities
            .Where(static mutation => mutation.Kind == EntityMutationKind.DestroyEntity)
            .Select(static mutation => mutation.EntityId));
        foreach (PositionMutation mutation in orderedPositions)
        {
            try
            {
                if (destroyed.Contains(mutation.EntityId))
                {
                    throw new InvalidOperationException(
                        $"Position mutation {mutation.MutationSequence} targets entity {mutation.EntityId} scheduled for destruction.");
                }
                if (!world.Entities.Contains(mutation.EntityId)
                    || world.Entities.GetLifecycleState(mutation.EntityId) != EntityLifecycleState.Active)
                {
                    throw new InvalidOperationException(
                        $"Position mutation {mutation.MutationSequence} targets non-active entity {mutation.EntityId}.");
                }
                if (mutation.Position is EntityPosition position)
                {
                    world.Map.GetCellDefinition(position.Cell);
                }
                else if (!world.Spatial.TryGetPosition(mutation.EntityId, out _))
                {
                    throw new InvalidOperationException(
                        $"Position mutation {mutation.MutationSequence} clears an unpositioned entity {mutation.EntityId}.");
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
            {
                RejectPosition(
                    $"Position mutation batch rejected: {exception.Message}",
                    new[] { mutation });
            }
        }
    }

    private static void RejectPosition(
        string message,
        IEnumerable<PositionMutation> conflictingMutations)
        => throw new PositionMutationValidationException(message, conflictingMutations);

    private ulong AllocateSequence()
    {
        ulong sequence = _nextMutationSequence;
        _nextMutationSequence = checked(_nextMutationSequence + 1UL);
        return sequence;
    }

    private EntityMutation Add(EntityMutation mutation)
    {
        _mutations.Add(mutation);
        return mutation;
    }

    private MapCellMutation AddMap(MapCellMutation mutation)
    {
        _mapMutations.Add(mutation);
        return mutation;
    }

    private PositionMutation AddPosition(PositionMutation mutation)
    {
        _positionMutations.Add(mutation);
        return mutation;
    }

    private static void RequireEntityId(EntityId entityId)
    {
        if (entityId == EntityId.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entityId),
                entityId,
                "Entity ID zero is reserved.");
        }
    }
}
