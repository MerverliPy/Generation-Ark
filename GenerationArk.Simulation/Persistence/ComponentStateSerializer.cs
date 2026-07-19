using System;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Persistence;

public static class ComponentStateSerializer
{
    public static ComponentStateSnapshot Capture(
        ComponentRegistry components,
        EntityId entityId,
        ComponentTypeId componentTypeId)
    {
        ArgumentNullException.ThrowIfNull(components);
        IComponentStore store = components.GetStore(componentTypeId);
        return new ComponentStateSnapshot(componentTypeId, store.Serialize(entityId));
    }

    public static object Restore(
        ComponentRegistry components,
        ComponentStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(snapshot);
        return components.GetStore(snapshot.ComponentTypeId).Deserialize(snapshot.Payload);
    }
}
