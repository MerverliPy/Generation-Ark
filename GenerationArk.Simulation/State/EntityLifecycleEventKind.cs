namespace GenerationArk.Simulation.State;

public enum EntityLifecycleEventKind : byte
{
    EntityCreated = 1,
    EntityActivated = 2,
    ComponentAdded = 3,
    ComponentRemoved = 4,
    EntityDestroyed = 5,
    MutationRejected = 6
}
