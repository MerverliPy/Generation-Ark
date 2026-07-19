using System;

namespace GenerationArk.Simulation.Persistence;

/// <summary>
/// Stable simulation-save metadata. Pending commands, scheduled-event payloads,
/// random cursors, and world state remain inside the authoritative payload.
/// </summary>
public sealed class SimulationSaveMetadata
{
    public SimulationSaveMetadata(
        int simulationSchemaVersion,
        int replayFormatVersion,
        int checksumFormatVersion,
        int randomAlgorithmVersion,
        ulong rootSeed,
        long currentTick,
        int requestedSpeedMultiplier,
        bool isPaused,
        long nextEntityId,
        long nextCommandSequence,
        long nextSchedulerEventId,
        long nextSchedulerCreationSequence,
        string calendarDefinitionId,
        string buildVersion)
    {
        if (simulationSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationSchemaVersion));
        }
        if (replayFormatVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replayFormatVersion));
        }
        if (checksumFormatVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(checksumFormatVersion));
        }
        if (randomAlgorithmVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(randomAlgorithmVersion));
        }
        if (currentTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick));
        }
        if (!IsSupportedRunningSpeed(requestedSpeedMultiplier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedSpeedMultiplier),
                requestedSpeedMultiplier,
                "Requested speed must be 1, 4, 16, 64, or 256. Pause is stored separately.");
        }
        if (nextEntityId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextEntityId));
        }
        if (nextCommandSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextCommandSequence));
        }
        if (nextSchedulerEventId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextSchedulerEventId));
        }
        if (nextSchedulerCreationSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextSchedulerCreationSequence));
        }
        if (string.IsNullOrWhiteSpace(calendarDefinitionId))
        {
            throw new ArgumentException("Calendar definition ID is required.", nameof(calendarDefinitionId));
        }
        if (string.IsNullOrWhiteSpace(buildVersion))
        {
            throw new ArgumentException("Build version is required.", nameof(buildVersion));
        }

        SimulationSchemaVersion = simulationSchemaVersion;
        ReplayFormatVersion = replayFormatVersion;
        ChecksumFormatVersion = checksumFormatVersion;
        RandomAlgorithmVersion = randomAlgorithmVersion;
        RootSeed = rootSeed;
        CurrentTick = currentTick;
        RequestedSpeedMultiplier = requestedSpeedMultiplier;
        IsPaused = isPaused;
        NextEntityId = nextEntityId;
        NextCommandSequence = nextCommandSequence;
        NextSchedulerEventId = nextSchedulerEventId;
        NextSchedulerCreationSequence = nextSchedulerCreationSequence;
        CalendarDefinitionId = calendarDefinitionId;
        BuildVersion = buildVersion;
    }

    public int SimulationSchemaVersion { get; }
    public int ReplayFormatVersion { get; }
    public int ChecksumFormatVersion { get; }
    public int RandomAlgorithmVersion { get; }
    public ulong RootSeed { get; }
    public long CurrentTick { get; }
    public int RequestedSpeedMultiplier { get; }
    public bool IsPaused { get; }
    public long NextEntityId { get; }
    public long NextCommandSequence { get; }
    public long NextSchedulerEventId { get; }
    public long NextSchedulerCreationSequence { get; }
    public string CalendarDefinitionId { get; }
    public string BuildVersion { get; }

    private static bool IsSupportedRunningSpeed(int multiplier)
        => multiplier is 1 or 4 or 16 or 64 or 256;
}
