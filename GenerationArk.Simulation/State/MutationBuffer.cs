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
    private ulong _nextMutationSequence = 1;

    public int Count => _mutations.Count + _mapMutations.Count;
    public int EntityMutationCount => _mutations.Count;
    public int MapMutationCount => _mapMutations.Count;

    public IReadOnlyList<EntityMutation> PendingMutations => _mutations
        .OrderBy(static mutation => mutation, EntityMutationComparer.Instance)
        .ToArray();

    public IReadOnlyList<MapCellMutation> PendingMapMutations => _mapMutations
        .OrderBy(static mutation => mutation, MapCellMutationComparer.Instance)
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

    public EntityMutation EnqueueReplace(EntityId entityId, ComponentValue component)
    {
        RequireEntityId(entityId);
        ArgumentNullException.ThrowIfNull(component);
        return Add(new EntityMutation(
            AllocateSequence(),
            EntityMutationKind.ReplaceComponent,
            entityId,
            component,
            component.ComponentTypeId,
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

        ValidateUniqueSequences(ordered);
        ValidateUniqueMapSequences(orderedMap);
        Dictionary<ulong, EntityId> assignedCreateIds = ordered.Length == 0
            ? new Dictionary<ulong, EntityId>()
            : ValidateBatch(world, ordered, tick);
        world.Map.ValidateMutationBatch(orderedMap);

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
                    ComponentValue component = RequireComponent(mutation);
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
                case EntityMutationKind.ReplaceComponent:
                {
                    ComponentValue component = RequireComponent(mutation);
                    world.Components.Replace(world.Entities, mutation.EntityId, component);
                    world.RecordLifecycleEvent(
                        mutation.MutationSequence,
                        tick,
                        EntityLifecycleEventKind.ComponentAdded,
                        mutation.EntityId,
                        component.ComponentTypeId,
                        "component-replaced");
                    break;
                }
                default:
                    throw new InvalidOperationException(
                        $"Unknown entity mutation kind {(byte)mutation.Kind}.");
            }
        }

        world.Map.ApplyValidatedMutations(orderedMap);
        _mutations.Clear();
        _mapMutations.Clear();
        return new MutationCommitResult(
            ordered.Length + orderedMap.Length,
            cancelledEvents,
            created.ToArray());
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
                        ComponentValue component = RequireComponent(mutation);
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
                    case EntityMutationKind.ReplaceComponent:
                    {
                        RequireSimulatedEntity(simulatedEntities, mutation);
                        ComponentValue component = RequireComponent(mutation);
                        ValidateComponent(world, component);
                        if (!simulatedComponents.Contains((mutation.EntityId, component.ComponentTypeId)))
                        {
                            throw new InvalidOperationException(
                                $"Entity {mutation.EntityId} does not have component {component.ComponentTypeId} to replace.");
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
                .Where(static mutation => mutation.Kind is EntityMutationKind.AddComponent
                    or EntityMutationKind.RemoveComponent
                    or EntityMutationKind.ReplaceComponent)
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
                     .Where(static mutation => mutation.Kind is EntityMutationKind.AddComponent
                         or EntityMutationKind.RemoveComponent
                         or EntityMutationKind.ReplaceComponent)
                     .GroupBy(static mutation => (
                         mutation.EntityId,
                         mutation.ComponentTypeId
                             ?? mutation.Component?.ComponentTypeId
                             ?? default)))
        {
            if (group.Count() > 1)
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

    private static ComponentValue RequireComponent(EntityMutation mutation)
        => mutation.Component
            ?? throw new InvalidOperationException(
                $"Mutation {mutation.MutationSequence} is missing its component value.");

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
