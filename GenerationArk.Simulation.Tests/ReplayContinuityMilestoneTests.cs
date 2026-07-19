using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GenerationArk.Simulation.Persistence;
using GenerationArk.Simulation.Replay;
using GenerationArk.Simulation.UnityAdapter;

namespace GenerationArk.Simulation.Tests;

public static class ReplayContinuityMilestoneTests
{
    private const ulong Seed = 0x0123456789ABCDEFUL;
    private const string BuildVersion = "step8-foundation-r1";

    public static void HeadlessRunnerAdvancesExactTicksAndCapturesCheckpoints()
    {
        var runner = new HeadlessSimulationRunner();
        IReplaySimulationSession session = new FoundationScenarioFactory().CreateNew();
        HeadlessRunResult result = runner.RunToTick(
            session,
            1_000,
            CreateCommands(),
            new long[] { 10, 250, 500, 1_000 });

        Equal(0L, result.StartTick, "Headless start tick mismatch.");
        Equal(1_000L, result.EndTick, "Headless end tick mismatch.");
        Equal(1_000L, result.TicksExecuted, "Headless tick count mismatch.");
        Equal(4, result.Checkpoints.Count, "Checkpoint count mismatch.");
        Equal(result.FinalChecksum, result.Checkpoints[^1].Checksum, "Final checkpoint mismatch.");
    }

    public static void HeadlessRunnerRejectsInvalidTickProgress()
    {
        var runner = new HeadlessSimulationRunner();
        Throws<InvalidOperationException>(() => runner.RunToTick(new BrokenTickSession(), 1));
        Throws<ArgumentOutOfRangeException>(() => runner.RunToTick(
            new FoundationScenarioFactory().CreateNew(),
            -1));
    }

    public static void ReplayLogJsonRoundTripIsCanonical()
    {
        ReplayLog log = RecordLog(2_000, CreateCommands(), new long[] { 10, 400, 1_000, 2_000 });
        byte[] first = ReplayLogJson.ToUtf8(log);
        ReplayLog restored = ReplayLogJson.FromUtf8(first);
        byte[] second = ReplayLogJson.ToUtf8(restored);

        SequenceEqual(first, second, "Replay JSON was not canonical after round-trip.");
        Equal(log.FinalTick, restored.FinalTick, "Replay final tick changed.");
        Equal(log.Commands.Count, restored.Commands.Count, "Replay command count changed.");
    }

    public static void ReplayRunnerAppliesCommandsInStableOrder()
    {
        var commands = new[]
        {
            Command(0, 1, 3, "cmd-c", 1),
            Command(0, 1, 1, "cmd-a", 1),
            Command(0, 1, 2, "cmd-b", 1)
        };
        var recording = new SubmissionRecordingSession();
        var runner = new HeadlessSimulationRunner();
        runner.RunToTick(recording, 1, commands, new long[] { 1 });

        string joined = string.Join(",", recording.SubmittedIds);
        Equal("cmd-a,cmd-b,cmd-c", joined, "Replay submission order was unstable.");
    }

    public static void ReplayRunnerDetectsCheckpointMismatch()
    {
        ReplayLog valid = RecordLog(1_000, CreateCommands(), new long[] { 100, 500, 1_000 });
        ReplayCheckpoint[] corrupted = valid.Checkpoints.ToArray();
        corrupted[1] = new ReplayCheckpoint(corrupted[1].Tick, corrupted[1].Checksum ^ 1UL);
        var invalid = new ReplayLog(
            valid.FormatVersion,
            valid.RootSeed,
            valid.BuildVersion,
            valid.FinalTick,
            valid.Commands,
            corrupted);

        ReplayRunResult result = new ReplayRunner().Run(
            new FoundationScenarioFactory().CreateNew(),
            invalid);
        True(!result.Succeeded, "Corrupted replay checkpoint was not detected.");
        Equal(500L, result.FirstMismatchTick!.Value, "First mismatch tick was wrong.");
    }

    public static void SaveEnvelopeRoundTripPreservesMetadataAndPayload()
    {
        var runner = new HeadlessSimulationRunner();
        IReplaySimulationSession session = new FoundationScenarioFactory().CreateNew();
        runner.RunToTick(
            session,
            400,
            CreateCommands().Where(command => command.AcceptedTick <= 400),
            new long[] { 400 });
        SimulationSaveEnvelope save = session.CaptureSave();

        byte[] first = SimulationSaveEnvelopeJson.ToUtf8(save);
        SimulationSaveEnvelope restored = SimulationSaveEnvelopeJson.FromUtf8(first);
        byte[] second = SimulationSaveEnvelopeJson.ToUtf8(restored);

        SequenceEqual(first, second, "Save envelope JSON was not canonical after round-trip.");
        SequenceEqual(save.CopyPayload(), restored.CopyPayload(), "Save payload changed.");
        Equal(save.Metadata.CurrentTick, restored.Metadata.CurrentTick, "Save tick changed.");
        Equal(save.Checksum, restored.Checksum, "Save checksum changed.");
    }

    public static void SaveLoadContinuityMatchesUninterruptedRun()
    {
        ReplayCommand[] commands = CreateCommands();
        ReplayLog log = RecordLog(
            2_000,
            commands,
            new long[] { 100, 400, 700, 1_000, 1_500, 2_000 });

        ContinuityValidationResult result = new SaveLoadContinuityValidator().Validate(
            new FoundationScenarioFactory(),
            log,
            saveTick: 400);

        True(result.Succeeded, "Save/load continuity diverged.");
        Equal(400L, result.SaveTick, "Save boundary changed.");
    }

    public static void FramePatternsProduceIdenticalCheckpointChecksums()
    {
        ReplayLog log = RecordLog(
            5_000,
            CreateCommands(),
            new long[] { 100, 1_000, 2_500, 5_000 });
        FramePattern[] patterns =
        {
            new("stable-30", new[]
            {
                new FramePatternStep(1.0 / 30.0, SimulationSpeedProfile.Normal)
            }),
            new("stable-144", new[]
            {
                new FramePatternStep(1.0 / 144.0, SimulationSpeedProfile.Normal)
            }),
            new("stalls-speed-pause-step", new[]
            {
                new FramePatternStep(1.0 / 60.0, SimulationSpeedProfile.Normal),
                new FramePatternStep(1.0 / 240.0, SimulationSpeedProfile.Fast),
                new FramePatternStep(0.0, SimulationSpeedProfile.Paused, manualSteps: 1),
                new FramePatternStep(0.25, SimulationSpeedProfile.Paused),
                new FramePatternStep(0.005, SimulationSpeedProfile.VeryFast),
                new FramePatternStep(0.10, SimulationSpeedProfile.Normal)
            })
        };

        IReadOnlyList<FramePatternRunResult> results =
            new FramePatternDeterminismValidator().Validate(
                new FoundationScenarioFactory(),
                log,
                patterns);

        Equal(3, results.Count, "Frame pattern result count mismatch.");
        Equal(log.FinalTick, results[2].FinalTick, "Frame pattern final tick mismatch.");
    }

    public static void TenYearFoundationSoakRepeatsFinalChecksum()
    {
        SoakRunResult result = new DeterministicSoakRunner().RunTwice(
            new FoundationScenarioFactory(),
            DeterministicSoakRunner.TenYearFoundationTicks,
            checkpointInterval: 43_200);

        True(result.Succeeded, "Ten-year foundation soak diverged.");
        Equal(120, result.CheckpointCount, "Ten-year soak checkpoint count changed.");
        Equal(result.FirstFinalChecksum, result.SecondFinalChecksum, "Soak final checksum mismatch.");
    }

    public static void SoakCheckpointRetentionRemainsBounded()
    {
        SoakRunResult result = new DeterministicSoakRunner().RunTwice(
            new FoundationScenarioFactory(),
            totalTicks: 432_000,
            checkpointInterval: 43_200);

        Equal(10, result.CheckpointCount, "Bounded soak retained an unexpected checkpoint count.");
        True(result.CheckpointCount < 1_000, "Soak checkpoint retention was not bounded.");
    }

    private static ReplayLog RecordLog(
        long finalTick,
        IEnumerable<ReplayCommand> commands,
        IEnumerable<long> checkpoints)
    {
        ReplayCommand[] commandArray = commands.ToArray();
        long[] checkpointArray = checkpoints.ToArray();
        HeadlessRunResult baseline = new HeadlessSimulationRunner().RunToTick(
            new FoundationScenarioFactory().CreateNew(),
            finalTick,
            commandArray,
            checkpointArray);
        return new ReplayLog(
            ReplayLog.CurrentFormatVersion,
            Seed,
            BuildVersion,
            finalTick,
            commandArray,
            baseline.Checkpoints);
    }

    private static ReplayCommand[] CreateCommands()
        => new[]
        {
            Command(0, 10, 2, "command-b", 5),
            Command(0, 10, 1, "command-a", 3),
            Command(5, 12, 3, "command-c", -2),
            Command(100, 700, 4, "pending-through-save", 17),
            Command(800, 900, 5, "post-save", 11)
        };

    private static ReplayCommand Command(
        long acceptedTick,
        long targetTick,
        long sequence,
        string id,
        long delta)
        => new(
            acceptedTick,
            targetTick,
            sequence,
            id,
            "state-delta-v1",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                delta.ToString(CultureInfo.InvariantCulture))));

    private sealed class FoundationScenarioFactory : IReplaySimulationFactory
    {
        public IReplaySimulationSession CreateNew()
            => FoundationScenarioSession.CreateNew(Seed, BuildVersion);

        public IReplaySimulationSession Load(SimulationSaveEnvelope save)
            => FoundationScenarioSession.Load(save);
    }

    private sealed class FoundationScenarioSession : IReplaySimulationSession
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const ulong StateMultiplier = 6364136223846793005UL;
        private const ulong StateIncrement = 1442695040888963407UL;

        private readonly ulong _rootSeed;
        private readonly string _buildVersion;
        private readonly List<ReplayCommand> _pendingCommands = new();
        private readonly List<ScheduledValue> _scheduled = new();

        private long _currentTick;
        private ulong _state;
        private ulong _randomCounter;
        private long _nextEntityId;
        private long _nextCommandSequence;
        private long _nextSchedulerEventId;
        private long _nextSchedulerCreationSequence;

        private FoundationScenarioSession(ulong rootSeed, string buildVersion)
        {
            _rootSeed = rootSeed;
            _buildVersion = buildVersion;
            _state = 0xA5A5A5A55A5A5A5AUL;
            _nextEntityId = 1;
            _nextSchedulerEventId = 2;
            _nextSchedulerCreationSequence = 1;
            _scheduled.Add(new ScheduledValue(
                eventId: 1,
                dueTick: 1_440,
                priority: 0,
                creationSequence: 0,
                delta: 29,
                repeatInterval: 1_440));
        }

        public long CurrentTick => _currentTick;

        public static FoundationScenarioSession CreateNew(ulong rootSeed, string buildVersion)
            => new(rootSeed, buildVersion);

        public void SubmitCommand(ReplayCommand command)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            if (command.AcceptedTick != _currentTick)
            {
                throw new InvalidOperationException(
                    $"Command {command.CommandId} was accepted at tick {_currentTick}, expected {command.AcceptedTick}.");
            }
            if (_pendingCommands.Any(existing =>
                StringComparer.Ordinal.Equals(existing.CommandId, command.CommandId)))
            {
                throw new InvalidOperationException($"Duplicate command ID {command.CommandId}.");
            }

            _pendingCommands.Add(command);
            _nextCommandSequence = Math.Max(_nextCommandSequence, checked(command.Sequence + 1));
        }

        public void RunOneTick()
        {
            _currentTick = checked(_currentTick + 1);
            ApplyCommands();
            ApplyScheduledValues();

            ulong random = Mix64(unchecked(
                _rootSeed
                ^ (ulong)_currentTick
                ^ (_randomCounter * 0x9E3779B97F4A7C15UL)));
            _randomCounter = checked(_randomCounter + 1);
            _state = unchecked(_state * StateMultiplier + StateIncrement + random);

            if (_currentTick % 100_000 == 0)
            {
                _nextEntityId = checked(_nextEntityId + 1);
                _state = unchecked(_state ^ (ulong)_nextEntityId);
            }
        }

        public ulong CaptureChecksum()
        {
            ulong hash = FnvOffset;
            HashUInt64(ref hash, (ulong)_currentTick);
            HashUInt64(ref hash, _rootSeed);
            HashUInt64(ref hash, _state);
            HashUInt64(ref hash, _randomCounter);
            HashUInt64(ref hash, (ulong)_nextEntityId);
            HashUInt64(ref hash, (ulong)_nextCommandSequence);
            HashUInt64(ref hash, (ulong)_nextSchedulerEventId);
            HashUInt64(ref hash, (ulong)_nextSchedulerCreationSequence);

            ReplayCommand[] commands = _pendingCommands.ToArray();
            Array.Sort(commands, ReplayCommandComparer.Instance);
            HashUInt64(ref hash, (ulong)commands.Length);
            foreach (ReplayCommand command in commands)
            {
                HashUInt64(ref hash, (ulong)command.AcceptedTick);
                HashUInt64(ref hash, (ulong)command.TargetTick);
                HashUInt64(ref hash, (ulong)command.Sequence);
                HashString(ref hash, command.CommandId);
                HashString(ref hash, command.CommandType);
                HashString(ref hash, command.PayloadBase64);
            }

            ScheduledValue[] events = _scheduled.ToArray();
            Array.Sort(events, ScheduledValueComparer.Instance);
            HashUInt64(ref hash, (ulong)events.Length);
            foreach (ScheduledValue scheduled in events)
            {
                HashUInt64(ref hash, (ulong)scheduled.EventId);
                HashUInt64(ref hash, (ulong)scheduled.DueTick);
                HashUInt64(ref hash, unchecked((ulong)scheduled.Priority));
                HashUInt64(ref hash, (ulong)scheduled.CreationSequence);
                HashUInt64(ref hash, unchecked((ulong)scheduled.Delta));
                HashUInt64(ref hash, (ulong)scheduled.RepeatInterval);
            }

            return hash;
        }

        public SimulationSaveEnvelope CaptureSave()
        {
            var metadata = new SimulationSaveMetadata(
                simulationSchemaVersion: 1,
                replayFormatVersion: ReplayLog.CurrentFormatVersion,
                checksumFormatVersion: 1,
                randomAlgorithmVersion: 1,
                rootSeed: _rootSeed,
                currentTick: _currentTick,
                requestedSpeedMultiplier: SimulationSpeedProfile.Chronicle,
                isPaused: false,
                nextEntityId: _nextEntityId,
                nextCommandSequence: _nextCommandSequence,
                nextSchedulerEventId: _nextSchedulerEventId,
                nextSchedulerCreationSequence: _nextSchedulerCreationSequence,
                calendarDefinitionId: "ship-calendar-360-v1",
                buildVersion: _buildVersion);
            return new SimulationSaveEnvelope(metadata, CaptureChecksum(), CapturePayload());
        }

        public static FoundationScenarioSession Load(SimulationSaveEnvelope save)
        {
            if (save is null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            SimulationSaveMetadata metadata = save.Metadata;
            var session = new FoundationScenarioSession(metadata.RootSeed, metadata.BuildVersion);
            session._pendingCommands.Clear();
            session._scheduled.Clear();

            using JsonDocument document = JsonDocument.Parse(save.CopyPayload());
            JsonElement root = document.RootElement;
            session._currentTick = root.GetProperty("currentTick").GetInt64();
            session._state = ParseHex(root.GetProperty("state").GetString());
            session._randomCounter = ParseHex(root.GetProperty("randomCounter").GetString());
            session._nextEntityId = root.GetProperty("nextEntityId").GetInt64();
            session._nextCommandSequence = root.GetProperty("nextCommandSequence").GetInt64();
            session._nextSchedulerEventId = root.GetProperty("nextSchedulerEventId").GetInt64();
            session._nextSchedulerCreationSequence = root.GetProperty("nextSchedulerCreationSequence").GetInt64();

            foreach (JsonElement command in root.GetProperty("pendingCommands").EnumerateArray())
            {
                session._pendingCommands.Add(new ReplayCommand(
                    command.GetProperty("acceptedTick").GetInt64(),
                    command.GetProperty("targetTick").GetInt64(),
                    command.GetProperty("sequence").GetInt64(),
                    command.GetProperty("commandId").GetString()!,
                    command.GetProperty("commandType").GetString()!,
                    command.GetProperty("payloadBase64").GetString()!));
            }

            foreach (JsonElement scheduled in root.GetProperty("scheduledEvents").EnumerateArray())
            {
                session._scheduled.Add(new ScheduledValue(
                    scheduled.GetProperty("eventId").GetInt64(),
                    scheduled.GetProperty("dueTick").GetInt64(),
                    scheduled.GetProperty("priority").GetInt32(),
                    scheduled.GetProperty("creationSequence").GetInt64(),
                    scheduled.GetProperty("delta").GetInt64(),
                    scheduled.GetProperty("repeatInterval").GetInt64()));
            }

            if (session._currentTick != metadata.CurrentTick)
            {
                throw new InvalidDataException("Save payload tick differs from save metadata tick.");
            }
            if (session.CaptureChecksum() != save.Checksum)
            {
                throw new InvalidDataException("Loaded save payload checksum mismatch.");
            }
            return session;
        }

        private void ApplyCommands()
        {
            if (_pendingCommands.Count == 0)
            {
                return;
            }

            bool hasDueCommand = false;
            foreach (ReplayCommand command in _pendingCommands)
            {
                if (command.TargetTick == _currentTick)
                {
                    hasDueCommand = true;
                    break;
                }
            }
            if (!hasDueCommand)
            {
                return;
            }

            ReplayCommand[] due = _pendingCommands
                .Where(command => command.TargetTick == _currentTick)
                .ToArray();
            Array.Sort(due, ReplayCommandComparer.Instance);
            foreach (ReplayCommand command in due)
            {
                long delta = long.Parse(
                    Encoding.UTF8.GetString(command.DecodePayload()),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture);
                _state = unchecked(_state + (ulong)delta);
                _pendingCommands.Remove(command);
            }
        }

        private void ApplyScheduledValues()
        {
            bool hasDueEvent = false;
            foreach (ScheduledValue scheduled in _scheduled)
            {
                if (scheduled.DueTick == _currentTick)
                {
                    hasDueEvent = true;
                    break;
                }
            }
            if (!hasDueEvent)
            {
                return;
            }

            ScheduledValue[] due = _scheduled
                .Where(scheduled => scheduled.DueTick == _currentTick)
                .ToArray();
            Array.Sort(due, ScheduledValueComparer.Instance);
            foreach (ScheduledValue scheduled in due)
            {
                _state = unchecked(_state + (ulong)scheduled.Delta);
                _scheduled.Remove(scheduled);
                if (scheduled.RepeatInterval > 0)
                {
                    _scheduled.Add(new ScheduledValue(
                        eventId: _nextSchedulerEventId++,
                        dueTick: checked(scheduled.DueTick + scheduled.RepeatInterval),
                        priority: scheduled.Priority,
                        creationSequence: _nextSchedulerCreationSequence++,
                        delta: scheduled.Delta,
                        repeatInterval: scheduled.RepeatInterval));
                }
            }
        }

        private byte[] CapturePayload()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteNumber("currentTick", _currentTick);
                writer.WriteString("state", FormatHex(_state));
                writer.WriteString("randomCounter", FormatHex(_randomCounter));
                writer.WriteNumber("nextEntityId", _nextEntityId);
                writer.WriteNumber("nextCommandSequence", _nextCommandSequence);
                writer.WriteNumber("nextSchedulerEventId", _nextSchedulerEventId);
                writer.WriteNumber("nextSchedulerCreationSequence", _nextSchedulerCreationSequence);

                writer.WritePropertyName("pendingCommands");
                writer.WriteStartArray();
                ReplayCommand[] commands = _pendingCommands.ToArray();
                Array.Sort(commands, ReplayCommandComparer.Instance);
                foreach (ReplayCommand command in commands)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("acceptedTick", command.AcceptedTick);
                    writer.WriteNumber("targetTick", command.TargetTick);
                    writer.WriteNumber("sequence", command.Sequence);
                    writer.WriteString("commandId", command.CommandId);
                    writer.WriteString("commandType", command.CommandType);
                    writer.WriteString("payloadBase64", command.PayloadBase64);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WritePropertyName("scheduledEvents");
                writer.WriteStartArray();
                ScheduledValue[] events = _scheduled.ToArray();
                Array.Sort(events, ScheduledValueComparer.Instance);
                foreach (ScheduledValue scheduled in events)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("eventId", scheduled.EventId);
                    writer.WriteNumber("dueTick", scheduled.DueTick);
                    writer.WriteNumber("priority", scheduled.Priority);
                    writer.WriteNumber("creationSequence", scheduled.CreationSequence);
                    writer.WriteNumber("delta", scheduled.Delta);
                    writer.WriteNumber("repeatInterval", scheduled.RepeatInterval);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return stream.ToArray();
        }

        private static ulong Mix64(ulong value)
        {
            value = unchecked(value + 0x9E3779B97F4A7C15UL);
            value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
            value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
            return value ^ (value >> 31);
        }

        private static void HashUInt64(ref ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash = unchecked(hash * FnvPrime);
            }
        }

        private static void HashString(ref ulong hash, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            HashUInt64(ref hash, (ulong)bytes.Length);
            foreach (byte item in bytes)
            {
                hash ^= item;
                hash = unchecked(hash * FnvPrime);
            }
        }

        private static string FormatHex(ulong value)
            => $"0x{value:X16}";

        private static ulong ParseHex(string? value)
        {
            if (value is null || !value.StartsWith("0x", StringComparison.Ordinal))
            {
                throw new FormatException("Expected 0x-prefixed hexadecimal value.");
            }
            return ulong.Parse(
                value.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture);
        }

        private sealed class ScheduledValue
        {
            public ScheduledValue(
                long eventId,
                long dueTick,
                int priority,
                long creationSequence,
                long delta,
                long repeatInterval)
            {
                EventId = eventId;
                DueTick = dueTick;
                Priority = priority;
                CreationSequence = creationSequence;
                Delta = delta;
                RepeatInterval = repeatInterval;
            }

            public long EventId { get; }
            public long DueTick { get; }
            public int Priority { get; }
            public long CreationSequence { get; }
            public long Delta { get; }
            public long RepeatInterval { get; }
        }

        private sealed class ScheduledValueComparer : IComparer<ScheduledValue>
        {
            public static ScheduledValueComparer Instance { get; } = new();

            public int Compare(ScheduledValue? left, ScheduledValue? right)
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
                int comparison = left.DueTick.CompareTo(right.DueTick);
                if (comparison != 0)
                {
                    return comparison;
                }
                comparison = left.Priority.CompareTo(right.Priority);
                if (comparison != 0)
                {
                    return comparison;
                }
                comparison = left.CreationSequence.CompareTo(right.CreationSequence);
                if (comparison != 0)
                {
                    return comparison;
                }
                return left.EventId.CompareTo(right.EventId);
            }
        }
    }

    private sealed class BrokenTickSession : IReplaySimulationSession
    {
        public long CurrentTick => 0;
        public void SubmitCommand(ReplayCommand command) { }
        public void RunOneTick() { }
        public ulong CaptureChecksum() => 0;
        public SimulationSaveEnvelope CaptureSave()
            => throw new NotSupportedException();
    }

    private sealed class SubmissionRecordingSession : IReplaySimulationSession
    {
        private long _tick;
        public List<string> SubmittedIds { get; } = new();
        public long CurrentTick => _tick;
        public void SubmitCommand(ReplayCommand command) => SubmittedIds.Add(command.CommandId);
        public void RunOneTick() => _tick++;
        public ulong CaptureChecksum() => (ulong)_tick;
        public SimulationSaveEnvelope CaptureSave()
            => throw new NotSupportedException();
    }

    private static void Equal<T>(T expected, T actual, string message)
        where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
        {
            throw new InvalidOperationException(
                $"{message} Expected: {expected}. Actual: {actual}.");
        }
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void SequenceEqual(byte[] expected, byte[] actual, string message)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected exception {typeof(TException).Name} was not thrown.");
    }
}
