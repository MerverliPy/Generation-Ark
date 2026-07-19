namespace GenerationArk.Simulation.Core;

public interface ISimulationRunner
{
    SimTick CurrentTick { get; }
    void RunOneTick();
    void RunTicks(int count);
}
