using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Runs only authoritative ticks. It has no wall-clock, frame, Unity, or presentation dependency.
/// </summary>
public sealed class HeadlessSimulationRunner
{
    public HeadlessRunResult RunToTick(
        IReplaySimulationSession session,
        long targetTick,
        IEnumerable<ReplayCommand>? commands = null,
        IEnumerable<long>? checkpointTicks = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        long startTick = session.CurrentTick;
        if (targetTick < startTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetTick),
                targetTick,
                "Target tick cannot precede the current session tick.");
        }

        ReplayCommand[] acceptedCommands = (commands ?? Array.Empty<ReplayCommand>()).ToArray();
        Array.Sort(acceptedCommands, ReplayCommandAcceptanceComparer.Instance);
        ValidateCommands(acceptedCommands, startTick, targetTick);

        long[] requestedCheckpoints = (checkpointTicks ?? Array.Empty<long>()).ToArray();
        Array.Sort(requestedCheckpoints);
        ValidateCheckpoints(requestedCheckpoints, startTick, targetTick);

        var captured = new List<ReplayCheckpoint>(requestedCheckpoints.Length);
        int commandIndex = 0;
        int checkpointIndex = 0;

        SubmitAcceptedAtCurrentTick(session, acceptedCommands, ref commandIndex);
        CaptureAtCurrentTick(session, requestedCheckpoints, ref checkpointIndex, captured);

        while (session.CurrentTick < targetTick)
        {
            long beforeTick = session.CurrentTick;
            long expectedAfterTick = checked(beforeTick + 1);
            session.RunOneTick();
            if (session.CurrentTick != expectedAfterTick)
            {
                throw new InvalidOperationException(
                    $"RunOneTick must advance exactly one tick. Before: {beforeTick}. After: {session.CurrentTick}.");
            }

            SubmitAcceptedAtCurrentTick(session, acceptedCommands, ref commandIndex);
            CaptureAtCurrentTick(session, requestedCheckpoints, ref checkpointIndex, captured);
        }

        if (commandIndex != acceptedCommands.Length)
        {
            throw new InvalidOperationException("Not every replay command reached its acceptance tick.");
        }
        if (checkpointIndex != requestedCheckpoints.Length)
        {
            throw new InvalidOperationException("Not every requested checkpoint was captured.");
        }

        return new HeadlessRunResult(
            startTick,
            session.CurrentTick,
            session.CaptureChecksum(),
            captured.ToArray());
    }

    private static void SubmitAcceptedAtCurrentTick(
        IReplaySimulationSession session,
        ReplayCommand[] commands,
        ref int commandIndex)
    {
        while (commandIndex < commands.Length
            && commands[commandIndex].AcceptedTick == session.CurrentTick)
        {
            session.SubmitCommand(commands[commandIndex]);
            commandIndex++;
        }
    }

    private static void CaptureAtCurrentTick(
        IReplaySimulationSession session,
        long[] checkpointTicks,
        ref int checkpointIndex,
        List<ReplayCheckpoint> captured)
    {
        if (checkpointIndex < checkpointTicks.Length
            && checkpointTicks[checkpointIndex] == session.CurrentTick)
        {
            captured.Add(new ReplayCheckpoint(session.CurrentTick, session.CaptureChecksum()));
            checkpointIndex++;
        }
    }

    private static void ValidateCommands(
        ReplayCommand[] commands,
        long startTick,
        long targetTick)
    {
        for (int index = 0; index < commands.Length; index++)
        {
            ReplayCommand command = commands[index];
            if (command.AcceptedTick < startTick || command.AcceptedTick > targetTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(commands),
                    $"Command {command.CommandId} acceptance tick {command.AcceptedTick} is outside {startTick}..{targetTick}.");
            }
            if (index > 0 && ReplayCommandAcceptanceComparer.Instance.Compare(
                    commands[index - 1],
                    command) == 0)
            {
                throw new ArgumentException(
                    "Replay commands contain a duplicate acceptance/sequence/ID key.",
                    nameof(commands));
            }
        }
    }

    private static void ValidateCheckpoints(
        long[] checkpoints,
        long startTick,
        long targetTick)
    {
        for (int index = 0; index < checkpoints.Length; index++)
        {
            long checkpoint = checkpoints[index];
            if (checkpoint < startTick || checkpoint > targetTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(checkpoints),
                    $"Checkpoint tick {checkpoint} is outside {startTick}..{targetTick}.");
            }
            if (index > 0 && checkpoints[index - 1] == checkpoint)
            {
                throw new ArgumentException(
                    $"Duplicate checkpoint tick {checkpoint}.",
                    nameof(checkpoints));
            }
        }
    }

    private sealed class ReplayCommandAcceptanceComparer : IComparer<ReplayCommand>
    {
        public static ReplayCommandAcceptanceComparer Instance { get; } = new();

        public int Compare(ReplayCommand? left, ReplayCommand? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left is null)
            {
                return -1;
            }
            if (right is null)
            {
                return 1;
            }

            int comparison = left.AcceptedTick.CompareTo(right.AcceptedTick);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = left.Sequence.CompareTo(right.Sequence);
            if (comparison != 0)
            {
                return comparison;
            }
            return StringComparer.Ordinal.Compare(left.CommandId, right.CommandId);
        }
    }
}
