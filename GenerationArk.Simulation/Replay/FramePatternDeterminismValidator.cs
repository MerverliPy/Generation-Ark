using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.UnityAdapter;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Drives the same replay through UnitySimulationAdapter under multiple frame/speed patterns.
/// </summary>
public sealed class FramePatternDeterminismValidator
{
    private const int ValidationFrameTickBudget = 1_000_000;

    public IReadOnlyList<FramePatternRunResult> Validate(
        IReplaySimulationFactory factory,
        ReplayLog log,
        IEnumerable<FramePattern> patterns)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }
        if (patterns is null)
        {
            throw new ArgumentNullException(nameof(patterns));
        }

        FramePattern[] patternArray = patterns.ToArray();
        if (patternArray.Length < 2)
        {
            throw new ArgumentException("At least two frame patterns are required.", nameof(patterns));
        }

        var results = new List<FramePatternRunResult>(patternArray.Length);
        foreach (FramePattern pattern in patternArray)
        {
            results.Add(RunPattern(factory.CreateNew(), log, pattern));
        }

        FramePatternRunResult baseline = results[0];
        ValidateAgainstReplay(log, baseline);
        for (int resultIndex = 1; resultIndex < results.Count; resultIndex++)
        {
            FramePatternRunResult candidate = results[resultIndex];
            if (candidate.FinalTick != baseline.FinalTick
                || candidate.FinalChecksum != baseline.FinalChecksum)
            {
                throw new InvalidOperationException(
                    $"Frame pattern {candidate.PatternName} changed the final authoritative result.");
            }
            if (candidate.Checkpoints.Count != baseline.Checkpoints.Count)
            {
                throw new InvalidOperationException(
                    $"Frame pattern {candidate.PatternName} produced a different checkpoint count.");
            }
            for (int checkpointIndex = 0; checkpointIndex < baseline.Checkpoints.Count; checkpointIndex++)
            {
                ReplayCheckpoint expected = baseline.Checkpoints[checkpointIndex];
                ReplayCheckpoint actual = candidate.Checkpoints[checkpointIndex];
                if (actual.Tick != expected.Tick || actual.Checksum != expected.Checksum)
                {
                    throw new InvalidOperationException(
                        $"Frame pattern {candidate.PatternName} diverged at checkpoint {expected.Tick}.");
                }
            }
        }

        return results;
    }

    private static void ValidateAgainstReplay(ReplayLog log, FramePatternRunResult result)
    {
        if (result.FinalTick != log.FinalTick)
        {
            throw new InvalidOperationException(
                $"Frame pattern {result.PatternName} stopped at tick {result.FinalTick}, expected {log.FinalTick}.");
        }
        if (result.Checkpoints.Count != log.Checkpoints.Count)
        {
            throw new InvalidOperationException(
                $"Frame pattern {result.PatternName} produced a different replay checkpoint count.");
        }
        for (int index = 0; index < log.Checkpoints.Count; index++)
        {
            ReplayCheckpoint expected = log.Checkpoints[index];
            ReplayCheckpoint actual = result.Checkpoints[index];
            if (actual.Tick != expected.Tick || actual.Checksum != expected.Checksum)
            {
                throw new InvalidOperationException(
                    $"Frame pattern {result.PatternName} diverged from replay at checkpoint {expected.Tick}.");
            }
        }
    }

    private static FramePatternRunResult RunPattern(
        IReplaySimulationSession session,
        ReplayLog log,
        FramePattern pattern)
    {
        var coordinator = new ReplayTickCoordinator(session, log);
        var adapter = new UnitySimulationAdapter(
            coordinator.RunOneTick,
            ValidationFrameTickBudget);

        int stepIndex = 0;
        int cyclesWithoutProgress = 0;
        long tickAtCycleStart = session.CurrentTick;
        double accumulatedTicksAtCycleStart = adapter.AccumulatedTicks;

        while (session.CurrentTick < log.FinalTick)
        {
            FramePatternStep step = pattern.Steps[stepIndex];
            adapter.SetSpeedMultiplier(step.SpeedMultiplier);

            if (step.SpeedMultiplier == SimulationSpeedProfile.Paused)
            {
                for (int manual = 0;
                    manual < step.ManualSteps && session.CurrentTick < log.FinalTick;
                    manual++)
                {
                    adapter.StepOneTick();
                }
                adapter.AdvanceFrame(step.UnscaledDeltaSeconds);
            }
            else
            {
                double delta = LimitDeltaToRemainingTicks(
                    step.UnscaledDeltaSeconds,
                    step.SpeedMultiplier,
                    adapter.AccumulatedTicks,
                    checked(log.FinalTick - session.CurrentTick));
                adapter.AdvanceFrame(delta);
            }

            stepIndex++;
            if (stepIndex == pattern.Steps.Count)
            {
                stepIndex = 0;
                if (session.CurrentTick == tickAtCycleStart
                    && adapter.AccumulatedTicks == accumulatedTicksAtCycleStart)
                {
                    cyclesWithoutProgress++;
                    if (cyclesWithoutProgress >= 2)
                    {
                        throw new InvalidOperationException(
                            $"Frame pattern {pattern.Name} cannot advance ticks or fractional backlog.");
                    }
                }
                else
                {
                    cyclesWithoutProgress = 0;
                    tickAtCycleStart = session.CurrentTick;
                    accumulatedTicksAtCycleStart = adapter.AccumulatedTicks;
                }
            }
        }

        coordinator.Complete();
        return new FramePatternRunResult(
            pattern.Name,
            session.CurrentTick,
            session.CaptureChecksum(),
            coordinator.Checkpoints.ToArray());
    }

    private static double LimitDeltaToRemainingTicks(
        double requestedDelta,
        int speedMultiplier,
        double accumulatedTicks,
        long remainingAuthoritativeTicks)
    {
        double availableRequestedTicks = remainingAuthoritativeTicks - accumulatedTicks;
        if (availableRequestedTicks <= 0.0)
        {
            return 0.0;
        }

        double requestedTicks = requestedDelta
            * UnityFrameAccumulator.DefaultBaseTicksPerSecond
            * speedMultiplier;
        if (requestedTicks <= availableRequestedTicks)
        {
            return requestedDelta;
        }

        return availableRequestedTicks
            / (UnityFrameAccumulator.DefaultBaseTicksPerSecond * speedMultiplier);
    }

    private sealed class ReplayTickCoordinator
    {
        private readonly IReplaySimulationSession _session;
        private readonly ReplayCommand[] _commands;
        private readonly long[] _checkpointTicks;
        private int _commandIndex;
        private int _checkpointIndex;

        public ReplayTickCoordinator(IReplaySimulationSession session, ReplayLog log)
        {
            _session = session;
            _commands = log.Commands
                .OrderBy(static command => command.AcceptedTick)
                .ThenBy(static command => command.Sequence)
                .ThenBy(static command => command.CommandId, StringComparer.Ordinal)
                .ToArray();
            _checkpointTicks = log.Checkpoints
                .Select(static checkpoint => checkpoint.Tick)
                .ToArray();
            Checkpoints = new List<ReplayCheckpoint>(_checkpointTicks.Length);
            SubmitCommandsAtCurrentTick();
            CaptureCheckpointAtCurrentTick();
        }

        public List<ReplayCheckpoint> Checkpoints { get; }

        public void RunOneTick()
        {
            long before = _session.CurrentTick;
            _session.RunOneTick();
            if (_session.CurrentTick != checked(before + 1))
            {
                throw new InvalidOperationException("Frame adapter callback did not advance exactly one tick.");
            }
            SubmitCommandsAtCurrentTick();
            CaptureCheckpointAtCurrentTick();
        }

        public void Complete()
        {
            if (_commandIndex != _commands.Length)
            {
                throw new InvalidOperationException("Frame pattern did not submit every replay command.");
            }
            if (_checkpointIndex != _checkpointTicks.Length)
            {
                throw new InvalidOperationException("Frame pattern did not capture every replay checkpoint.");
            }
        }

        private void SubmitCommandsAtCurrentTick()
        {
            while (_commandIndex < _commands.Length
                && _commands[_commandIndex].AcceptedTick == _session.CurrentTick)
            {
                _session.SubmitCommand(_commands[_commandIndex]);
                _commandIndex++;
            }
        }

        private void CaptureCheckpointAtCurrentTick()
        {
            if (_checkpointIndex < _checkpointTicks.Length
                && _checkpointTicks[_checkpointIndex] == _session.CurrentTick)
            {
                Checkpoints.Add(new ReplayCheckpoint(
                    _session.CurrentTick,
                    _session.CaptureChecksum()));
                _checkpointIndex++;
            }
        }
    }
}
