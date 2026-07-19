using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Persistence;

public sealed record EntityStateEntrySnapshot(
    EntityId EntityId,
    EntityLifecycleState LifecycleState,
    ComponentStateSnapshot[] Components);
