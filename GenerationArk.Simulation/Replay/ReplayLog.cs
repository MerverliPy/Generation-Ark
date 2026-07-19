using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Immutable replay input and expected-checkpoint record.
/// </summary>
public sealed class ReplayLog
{
    public const int CurrentFormatVersion = 1;

    private readonly ReadOnlyCollection<ReplayCommand> _commands;
    private readonly ReadOnlyCollection<ReplayCheckpoint> _checkpoints;

    public ReplayLog(
        int formatVersion,
        ulong rootSeed,
        string buildVersion,
        long finalTick,
        IEnumerable<ReplayCommand> commands,
        IEnumerable<ReplayCheckpoint> checkpoints)
    {
        if (formatVersion != CurrentFormatVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(formatVersion),
                formatVersion,
                $"Supported replay format version is {CurrentFormatVersion}.");
        }
        if (string.IsNullOrWhiteSpace(buildVersion))
        {
            throw new ArgumentException("Build version is required.", nameof(buildVersion));
        }
        if (finalTick <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(finalTick));
        }
        if (commands is null)
        {
            throw new ArgumentNullException(nameof(commands));
        }
        if (checkpoints is null)
        {
            throw new ArgumentNullException(nameof(checkpoints));
        }

        ReplayCommand[] commandArray = commands.ToArray();
        Array.Sort(commandArray, ReplayCommandComparer.Instance);
        for (int index = 0; index < commandArray.Length; index++)
        {
            ReplayCommand command = commandArray[index];
            if (command.TargetTick > finalTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(commands),
                    $"Command {command.CommandId} targets tick {command.TargetTick}, after final tick {finalTick}.");
            }
            if (index > 0 && ReplayCommandComparer.Instance.Compare(
                    commandArray[index - 1],
                    command) == 0)
            {
                throw new ArgumentException(
                    "Replay commands contain a duplicate target/sequence/ID ordering key.",
                    nameof(commands));
            }
        }

        ReplayCheckpoint[] checkpointArray = checkpoints.ToArray();
        Array.Sort(checkpointArray, static (left, right) => left.Tick.CompareTo(right.Tick));
        for (int index = 0; index < checkpointArray.Length; index++)
        {
            ReplayCheckpoint checkpoint = checkpointArray[index];
            if (checkpoint.Tick <= 0 || checkpoint.Tick > finalTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(checkpoints),
                    $"Checkpoint tick {checkpoint.Tick} is outside 1..{finalTick}.");
            }
            if (index > 0 && checkpointArray[index - 1].Tick == checkpoint.Tick)
            {
                throw new ArgumentException(
                    $"Replay checkpoints contain duplicate tick {checkpoint.Tick}.",
                    nameof(checkpoints));
            }
        }
        if (checkpointArray.Length == 0 || checkpointArray[^1].Tick != finalTick)
        {
            throw new ArgumentException(
                "Replay checkpoints must include the final tick.",
                nameof(checkpoints));
        }

        FormatVersion = formatVersion;
        RootSeed = rootSeed;
        BuildVersion = buildVersion;
        FinalTick = finalTick;
        _commands = Array.AsReadOnly(commandArray);
        _checkpoints = Array.AsReadOnly(checkpointArray);
    }

    public int FormatVersion { get; }

    public ulong RootSeed { get; }

    public string BuildVersion { get; }

    public long FinalTick { get; }

    public IReadOnlyList<ReplayCommand> Commands => _commands;

    public IReadOnlyList<ReplayCheckpoint> Checkpoints => _checkpoints;
}
