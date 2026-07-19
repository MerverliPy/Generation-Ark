using GenerationArk.Simulation.Persistence;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Creates a known initial scenario or restores one at an exact saved tick boundary.
/// </summary>
public interface IReplaySimulationFactory
{
    IReplaySimulationSession CreateNew();

    IReplaySimulationSession Load(SimulationSaveEnvelope save);
}
