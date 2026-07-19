using GenerationArk.Simulation.Persistence;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Minimal authoritative seam required by headless, replay, persistence, and soak validation.
/// </summary>
public interface IReplaySimulationSession
{
    long CurrentTick { get; }

    void SubmitCommand(ReplayCommand command);

    void RunOneTick();

    ulong CaptureChecksum();

    SimulationSaveEnvelope CaptureSave();
}
