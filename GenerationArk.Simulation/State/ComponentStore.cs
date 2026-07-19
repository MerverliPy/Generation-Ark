using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Diagnostics;

namespace GenerationArk.Simulation.State;

public sealed class ComponentStore : IComponentStore
{
    private readonly ComponentRegistration _registration;
    private readonly SortedDictionary<EntityId, object> _values = new();

    public ComponentStore(ComponentRegistration registration)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
    }

    public ComponentTypeId ComponentTypeId => _registration.ComponentTypeId;
    public Type RuntimeType => _registration.RuntimeType;
    public int Count => _values.Count;
    public IReadOnlyList<EntityId> EntityIds => _values.Keys.ToArray();

    public bool Contains(EntityId entityId) => _values.ContainsKey(entityId);

    public object Get(EntityId entityId)
        => _values.TryGetValue(entityId, out object? value)
            ? value
            : throw new KeyNotFoundException(
                $"Entity {entityId} does not have component {ComponentTypeId}.");

    public void Add(EntityId entityId, object value)
    {
        _registration.ValidateRuntimeType(value);
        if (!_values.TryAdd(entityId, value))
        {
            throw new InvalidOperationException(
                $"Entity {entityId} already has component {ComponentTypeId}.");
        }
    }

    public void Replace(EntityId entityId, object value)
    {
        _registration.ValidateRuntimeType(value);
        if (!_values.ContainsKey(entityId))
        {
            throw new InvalidOperationException(
                $"Entity {entityId} does not have component {ComponentTypeId}.");
        }
        _values[entityId] = value;
    }

    public void Remove(EntityId entityId)
    {
        if (!_values.Remove(entityId))
        {
            throw new InvalidOperationException(
                $"Entity {entityId} does not have component {ComponentTypeId}.");
        }
    }

    public string Serialize(EntityId entityId) => _registration.Serialize(Get(entityId));

    public object Deserialize(string payload) => _registration.Deserialize(payload);

    public void WriteChecksum(StateChecksumWriter writer, EntityId entityId)
        => _registration.WriteChecksum(writer, Get(entityId));
}
