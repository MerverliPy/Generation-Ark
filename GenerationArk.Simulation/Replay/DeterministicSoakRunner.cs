using System;
using System.Collections.Generic;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Repeats long headless runs and compares periodic and final checksums.
/// Checkpoint retention is bounded by totalTicks/checkpointInterval.
/// </summary>
public sealed class DeterministicSoakRunner
{
    public const long TenYearFoundationTicks = 5_184_000;

    private readonly HeadlessSimulationRunner _headless = new();

    public SoakRunResult RunTwice(
        IReplaySimulationFactory factory,
        long totalTicks,
        long checkpointInterval)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        if (totalTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTicks));
        }
        if (checkpointInterval <= 0 || checkpointInterval > totalTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpointInterval));
        }

        long[] checkpointTicks = BuildCheckpointTicks(totalTicks, checkpointInterval);
        HeadlessRunResult first = _headless.RunToTick(
            factory.CreateNew(),
            totalTicks,
            checkpointTicks: checkpointTicks);
        HeadlessRunResult second = _headless.RunToTick(
            factory.CreateNew(),
            totalTicks,
            checkpointTicks: checkpointTicks);

        bool succeeded = first.FinalChecksum == second.FinalChecksum
            && first.Checkpoints.Count == second.Checkpoints.Count;
        if (succeeded)
        {
            for (int index = 0; index < first.Checkpoints.Count; index++)
            {
                ReplayCheckpoint left = first.Checkpoints[index];
                ReplayCheckpoint right = second.Checkpoints[index];
                if (left.Tick != right.Tick || left.Checksum != right.Checksum)
                {
                    succeeded = false;
                    break;
                }
            }
        }

        return new SoakRunResult(
            totalTicks,
            checkpointTicks.Length,
            first.FinalChecksum,
            second.FinalChecksum,
            succeeded);
    }

    private static long[] BuildCheckpointTicks(long totalTicks, long interval)
    {
        var ticks = new List<long>(checked((int)Math.Min(totalTicks / interval + 1, int.MaxValue)));
        long next = interval;
        while (next < totalTicks)
        {
            ticks.Add(next);
            next = checked(next + interval);
        }
        ticks.Add(totalTicks);
        return ticks.ToArray();
    }
}
