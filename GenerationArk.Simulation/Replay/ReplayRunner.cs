using System;
using System.Linq;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Replays accepted commands and compares canonical checksums at every recorded checkpoint.
/// </summary>
public sealed class ReplayRunner
{
    private readonly HeadlessSimulationRunner _headless = new();

    public ReplayRunResult Run(IReplaySimulationSession session, ReplayLog log)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }
        if (session.CurrentTick != 0)
        {
            throw new InvalidOperationException("Replay must start from the known tick-zero scenario.");
        }

        HeadlessRunResult run = _headless.RunToTick(
            session,
            log.FinalTick,
            log.Commands,
            log.Checkpoints.Select(static checkpoint => checkpoint.Tick));

        for (int index = 0; index < log.Checkpoints.Count; index++)
        {
            ReplayCheckpoint expected = log.Checkpoints[index];
            ReplayCheckpoint actual = run.Checkpoints[index];
            if (expected.Checksum != actual.Checksum)
            {
                return new ReplayRunResult(
                    run,
                    succeeded: false,
                    expected.Tick,
                    expected.Checksum,
                    actual.Checksum);
            }
        }

        return new ReplayRunResult(
            run,
            succeeded: true,
            firstMismatchTick: null,
            expectedChecksum: null,
            actualChecksum: null);
    }
}
