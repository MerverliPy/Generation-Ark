namespace GenerationArk.Simulation.Core;

public interface ISimSystem
{
    SimPhase Phase { get; }
    int Order { get; }
    SystemId Id { get; }

    void Tick(SimContext context);
}
