using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Diagnostics;

namespace GenerationArk.Simulation.State;

public interface IComponentStore
{
    ComponentTypeId ComponentTypeId { get; }
    Type RuntimeType { get; }
    int Count { get; }
    IReadOnlyList<EntityId> EntityIds { get; }

    bool Contains(EntityId entityId);
    object Get(EntityId entityId);
    void Add(EntityId entityId, object value);
    void Remove(EntityId entityId);
    string Serialize(EntityId entityId);
    object Deserialize(string payload);
    void WriteChecksum(StateChecksumWriter writer, EntityId entityId);
}
