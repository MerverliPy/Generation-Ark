using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Diagnostics;

namespace GenerationArk.Simulation.State;

public sealed class ComponentRegistry
{
    private readonly SortedDictionary<ComponentTypeId, ComponentStore> _stores = new();
    private readonly Dictionary<Type, ComponentTypeId> _idsByRuntimeType = new();
    private readonly SortedDictionary<ComponentTypeId, ComponentRegistration> _registrations = new();

    public ComponentRegistry(IEnumerable<ComponentRegistration>? registrations = null)
    {
        foreach (ComponentRegistration registration in registrations ?? Array.Empty<ComponentRegistration>())
        {
            Register(registration);
        }
    }

    public int RegisteredCount => _stores.Count;
    public IReadOnlyList<ComponentTypeId> RegisteredTypeIds => _stores.Keys.ToArray();
    public IReadOnlyList<ComponentRegistration> Registrations => _registrations.Values.ToArray();
    public IReadOnlyList<IComponentStore> Stores => _stores.Values.Cast<IComponentStore>().ToArray();

    public void Register(ComponentRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (_stores.ContainsKey(registration.ComponentTypeId))
        {
            throw new InvalidOperationException(
                $"Duplicate component type ID {registration.ComponentTypeId}.");
        }
        if (_idsByRuntimeType.ContainsKey(registration.RuntimeType))
        {
            throw new InvalidOperationException(
                $"Duplicate component runtime type {registration.RuntimeType.FullName}.");
        }

        _stores.Add(registration.ComponentTypeId, new ComponentStore(registration));
        _registrations.Add(registration.ComponentTypeId, registration);
        _idsByRuntimeType.Add(registration.RuntimeType, registration.ComponentTypeId);
    }

    public bool IsRegistered(ComponentTypeId componentTypeId)
        => _stores.ContainsKey(componentTypeId);

    public ComponentTypeId GetTypeId(Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        return _idsByRuntimeType.TryGetValue(runtimeType, out ComponentTypeId componentTypeId)
            ? componentTypeId
            : throw new InvalidOperationException(
                $"Runtime component type {runtimeType.FullName} is not registered.");
    }

    public IComponentStore GetStore(ComponentTypeId componentTypeId)
        => _stores.TryGetValue(componentTypeId, out ComponentStore? store)
            ? store
            : throw new InvalidOperationException(
                $"Unknown component type ID {componentTypeId}.");

    public bool Contains(EntityId entityId, ComponentTypeId componentTypeId)
        => GetStore(componentTypeId).Contains(entityId);

    public object Get(EntityId entityId, ComponentTypeId componentTypeId)
        => GetStore(componentTypeId).Get(entityId);

    internal void Add(EntityRegistry entities, EntityId entityId, ComponentValue component)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(component);
        if (!entities.Contains(entityId))
        {
            throw new InvalidOperationException(
                $"Cannot add component {component.ComponentTypeId} to missing entity {entityId}.");
        }

        GetStore(component.ComponentTypeId).Add(entityId, component.Value);
    }

    internal void Remove(EntityRegistry entities, EntityId entityId, ComponentTypeId componentTypeId)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!entities.Contains(entityId))
        {
            throw new InvalidOperationException(
                $"Cannot remove component {componentTypeId} from missing entity {entityId}.");
        }

        GetStore(componentTypeId).Remove(entityId);
    }

    internal IReadOnlyList<ComponentTypeId> RemoveAll(EntityId entityId)
    {
        var removed = new List<ComponentTypeId>();
        foreach ((ComponentTypeId componentTypeId, ComponentStore store) in _stores)
        {
            if (store.Contains(entityId))
            {
                store.Remove(entityId);
                removed.Add(componentTypeId);
            }
        }
        return removed;
    }

    public IReadOnlyList<ComponentTypeId> GetComponentTypeIds(EntityId entityId)
        => _stores
            .Where(pair => pair.Value.Contains(entityId))
            .Select(static pair => pair.Key)
            .ToArray();

    internal void ValidateEntityOwnership(EntityRegistry entities)
    {
        foreach ((ComponentTypeId componentTypeId, ComponentStore store) in _stores)
        {
            foreach (EntityId entityId in store.EntityIds)
            {
                if (!entities.Contains(entityId))
                {
                    throw new InvalidOperationException(
                        $"Component {componentTypeId} is attached to missing entity {entityId}.");
                }
            }
        }
    }

    internal void WriteEntityChecksum(StateChecksumWriter writer, EntityId entityId)
    {
        ComponentTypeId[] componentTypeIds = GetComponentTypeIds(entityId).ToArray();
        writer.AddInt32(componentTypeIds.Length);
        foreach (ComponentTypeId componentTypeId in componentTypeIds)
        {
            writer.AddString(componentTypeId.Value);
            GetStore(componentTypeId).WriteChecksum(writer, entityId);
        }
    }
}
