namespace GenerationArk.Simulation.Core;

public interface ISimulationClock
{
    SimTick CurrentTick { get; }
    SimulationSpeed RequestedSpeed { get; }
    bool IsPaused { get; }

    void SetSpeed(SimulationSpeed speed);
    void Pause();
    void RequestSingleStep();
}
