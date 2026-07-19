namespace GenerationArk.Simulation.State;

public enum EntityMutationKind : byte
{
    CreateEntity = 1,
    DestroyEntity = 2,
    AddComponent = 3,
    RemoveComponent = 4
}
