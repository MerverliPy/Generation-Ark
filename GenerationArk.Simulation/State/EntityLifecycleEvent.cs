namespace GenerationArk.Simulation.State;

public sealed record EntityLifecycleEvent(
    ulong EventSequence,
    ulong MutationSequence,
    long Tick,
    EntityLifecycleEventKind Kind,
    EntityId EntityId,
    ComponentTypeId? ComponentTypeId,
    string ReasonCode);
