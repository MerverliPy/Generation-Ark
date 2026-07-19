namespace GenerationArk.Simulation.State;

public interface IEntityCleanupHook
{
    string Id { get; }
    int Order { get; }

    void Cleanup(EntityId entityId, WorldState world);
}
