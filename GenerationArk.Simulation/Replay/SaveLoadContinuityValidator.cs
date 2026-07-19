using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Persistence;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Compares an uninterrupted run with a run saved and loaded at an exact tick boundary.
/// Commands accepted before the save but targeting later ticks remain in the saved payload.
/// </summary>
public sealed class SaveLoadContinuityValidator
{
    private readonly HeadlessSimulationRunner _headless = new();

    public ContinuityValidationResult Validate(
        IReplaySimulationFactory factory,
        ReplayLog expectedLog,
        long saveTick)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        if (expectedLog is null)
        {
            throw new ArgumentNullException(nameof(expectedLog));
        }
        if (saveTick <= 0 || saveTick >= expectedLog.FinalTick)
        {
            throw new ArgumentOutOfRangeException(nameof(saveTick));
        }

        IReplaySimulationSession beforeSave = factory.CreateNew();
        ReplayCommand[] acceptedBeforeOrAtSave = expectedLog.Commands
            .Where(command => command.AcceptedTick <= saveTick)
            .ToArray();
        long[] checkpointsBeforeOrAtSave = expectedLog.Checkpoints
            .Where(checkpoint => checkpoint.Tick <= saveTick)
            .Select(checkpoint => checkpoint.Tick)
            .ToArray();

        HeadlessRunResult firstSegment = _headless.RunToTick(
            beforeSave,
            saveTick,
            acceptedBeforeOrAtSave,
            checkpointsBeforeOrAtSave);

        SimulationSaveEnvelope save = beforeSave.CaptureSave();
        ulong savedChecksum = beforeSave.CaptureChecksum();
        if (save.Metadata.CurrentTick != saveTick)
        {
            throw new InvalidOperationException(
                $"Save metadata tick {save.Metadata.CurrentTick} does not match save boundary {saveTick}.");
        }
        if (save.Checksum != savedChecksum)
        {
            throw new InvalidOperationException("Save checksum does not match the authoritative state checksum.");
        }

        IReplaySimulationSession afterLoad = factory.Load(save);
        if (afterLoad.CurrentTick != saveTick)
        {
            throw new InvalidOperationException("Loaded simulation did not resume at the exact save tick boundary.");
        }
        if (afterLoad.CaptureChecksum() != savedChecksum)
        {
            throw new InvalidOperationException("Loaded state checksum differs immediately after restore.");
        }

        ReplayCommand[] acceptedAfterSave = expectedLog.Commands
            .Where(command => command.AcceptedTick > saveTick)
            .ToArray();
        long[] checkpointsAfterSave = expectedLog.Checkpoints
            .Where(checkpoint => checkpoint.Tick > saveTick)
            .Select(checkpoint => checkpoint.Tick)
            .ToArray();

        HeadlessRunResult secondSegment = _headless.RunToTick(
            afterLoad,
            expectedLog.FinalTick,
            acceptedAfterSave,
            checkpointsAfterSave);

        var actualByTick = new Dictionary<long, ulong>();
        foreach (ReplayCheckpoint checkpoint in firstSegment.Checkpoints)
        {
            actualByTick.Add(checkpoint.Tick, checkpoint.Checksum);
        }
        foreach (ReplayCheckpoint checkpoint in secondSegment.Checkpoints)
        {
            actualByTick.Add(checkpoint.Tick, checkpoint.Checksum);
        }

        foreach (ReplayCheckpoint expected in expectedLog.Checkpoints)
        {
            if (!actualByTick.TryGetValue(expected.Tick, out ulong actual))
            {
                throw new InvalidOperationException($"Continuity run did not capture checkpoint {expected.Tick}.");
            }
            if (actual != expected.Checksum)
            {
                return new ContinuityValidationResult(
                    succeeded: false,
                    saveTick,
                    savedChecksum,
                    expected.Tick,
                    expected.Checksum,
                    actual);
            }
        }

        return new ContinuityValidationResult(
            succeeded: true,
            saveTick,
            savedChecksum,
            firstMismatchTick: null,
            expectedChecksum: null,
            actualChecksum: null);
    }
}
