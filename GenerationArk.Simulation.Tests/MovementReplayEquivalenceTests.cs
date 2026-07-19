using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.Movement;
using GenerationArk.Simulation.Persistence;
using GenerationArk.Simulation.Replay;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;
using GenerationArk.Simulation.UnityAdapter;

namespace GenerationArk.Simulation.Tests;

internal static class MovementReplayEquivalenceTests
{
    private const ulong Seed = 0x4D4F56454D454E54UL;
    private const string BuildVersion = "step11-movement-replay-r1";

    public static void MovementReplayMatchesRecordedCheckpoints()
    {
        var factory = new MovementScenarioFactory();
        IReplaySimulationSession baselineSession = factory.CreateNew();
        HeadlessRunResult baseline = new HeadlessSimulationRunner().RunToTick(
            baselineSession,
            6,
            Array.Empty<ReplayCommand>(),
            new long[] { 1, 2, 3, 4, 5, 6 });

        var log = new ReplayLog(
            ReplayLog.CurrentFormatVersion,
            Seed,
            BuildVersion,
            finalTick: 6,
            commands: Array.Empty<ReplayCommand>(),
            checkpoints: baseline.Checkpoints);

        ReplayRunResult replay = new ReplayRunner().Run(factory.CreateNew(), log);

        TestAssert.True(replay.Succeeded, "Movement replay diverged from recorded checkpoints.");
        TestAssert.Equal(baseline.FinalChecksum, replay.Run.FinalChecksum);

        var baselineMovement = ((MovementScenarioSession)baselineSession).Movement;
        TestAssert.Equal(new MapCellId(5), baselineMovement.CurrentCell);
        TestAssert.Equal(new MapCellId(5), baselineMovement.DestinationCell);
        TestAssert.Equal<ulong>(5, baselineMovement.RouteRevision);
    }

    public static void MovementFramePatternsProduceIdenticalCheckpointsAndFinalState()
    {
        var baselineFactory = new MovementScenarioFactory();
        HeadlessRunResult baseline = new HeadlessSimulationRunner().RunToTick(
            baselineFactory.CreateNew(),
            6,
            Array.Empty<ReplayCommand>(),
            new long[] { 1, 2, 3, 4, 5, 6 });
        var log = new ReplayLog(
            ReplayLog.CurrentFormatVersion,
            Seed,
            BuildVersion,
            finalTick: 6,
            commands: Array.Empty<ReplayCommand>(),
            checkpoints: baseline.Checkpoints);
        var patterns = new[]
        {
            new FramePattern("stable-30", new[]
            {
                new FramePatternStep(1.0 / 30.0, SimulationSpeedProfile.Normal)
            }),
            new FramePattern("stable-144", new[]
            {
                new FramePatternStep(1.0 / 144.0, SimulationSpeedProfile.Normal)
            }),
            new FramePattern("stalls-speed-pause-step", new[]
            {
                new FramePatternStep(1.0 / 60.0, SimulationSpeedProfile.Normal),
                new FramePatternStep(1.0 / 240.0, SimulationSpeedProfile.Fast),
                new FramePatternStep(0.0, SimulationSpeedProfile.Paused, manualSteps: 1),
                new FramePatternStep(0.25, SimulationSpeedProfile.Paused),
                new FramePatternStep(0.005, SimulationSpeedProfile.VeryFast),
                new FramePatternStep(0.10, SimulationSpeedProfile.Normal)
            })
        };
        var factory = new MovementScenarioFactory();

        IReadOnlyList<FramePatternRunResult> results =
            new FramePatternDeterminismValidator().Validate(factory, log, patterns);

        TestAssert.Equal(patterns.Length, results.Count);
        TestAssert.Equal(patterns.Length, factory.CreatedSessions.Count);
        MovementAgentState expected = factory.CreatedSessions[0].Movement;
        for (int index = 0; index < results.Count; index++)
        {
            FramePatternRunResult result = results[index];
            MovementAgentState actual = factory.CreatedSessions[index].Movement;
            TestAssert.Equal(log.FinalTick, result.FinalTick);
            TestAssert.Equal(baseline.FinalChecksum, result.FinalChecksum);
            TestAssert.Equal(log.Checkpoints.Count, result.Checkpoints.Count);
            for (int checkpointIndex = 0; checkpointIndex < log.Checkpoints.Count; checkpointIndex++)
            {
                TestAssert.Equal(log.Checkpoints[checkpointIndex].Tick, result.Checkpoints[checkpointIndex].Tick);
                TestAssert.Equal(log.Checkpoints[checkpointIndex].Checksum, result.Checkpoints[checkpointIndex].Checksum);
            }
            TestAssert.Equal(expected.CurrentCell, actual.CurrentCell);
            TestAssert.Equal(expected.DestinationCell, actual.DestinationCell);
            TestAssert.Equal(expected.RouteRevision, actual.RouteRevision);
        }

        TestAssert.Equal(new MapCellId(5), expected.CurrentCell);
        TestAssert.Equal(new MapCellId(5), expected.DestinationCell);
        TestAssert.Equal<ulong>(5, expected.RouteRevision);
    }

    private sealed class MovementScenarioFactory : IReplaySimulationFactory
    {
        public List<MovementScenarioSession> CreatedSessions { get; } = new();

        public IReplaySimulationSession CreateNew()
        {
            var session = new MovementScenarioSession();
            CreatedSessions.Add(session);
            return session;
        }

        public IReplaySimulationSession Load(SimulationSaveEnvelope save) =>
            throw new NotSupportedException("Movement replay equivalence does not load saves.");
    }

    private sealed class MovementScenarioSession : IReplaySimulationSession
    {
        private readonly WorldState _world;
        private readonly DeterministicScheduler _scheduler;
        private readonly EntityId _entityId;

        public MovementScenarioSession()
        {
            var definitions = new MapCellDefinitionRegistry(new[]
            {
                new MapCellDefinition(new MapCellDefinitionId(1), ParticipatesInRoomTopology: true)
            });
            var map = new MapState(6, 1, definitions, new MapCellDefinitionId(1));
            _world = new WorldState(
                componentRegistrations: new[] { MovementAgentState.CreateRegistration() },
                map: map);
            _scheduler = new DeterministicScheduler(
                new ScheduledEventHandlerRegistry(Array.Empty<IScheduledEventHandler>()));
            _world.Mutations.EnqueueCreate(new[]
            {
                new ComponentValue(
                    MovementAgentState.ComponentTypeId,
                    new MovementAgentState(new MapCellId(0), new MapCellId(5), 0))
            });
            _entityId = _world.CommitMutations(_scheduler, new SimTick(0)).CreatedEntityIds[0];
        }

        public long CurrentTick { get; private set; }

        public MovementAgentState Movement =>
            (MovementAgentState)_world.Components.Get(_entityId, MovementAgentState.ComponentTypeId);

        public void SubmitCommand(ReplayCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            throw new InvalidOperationException("This deterministic movement scenario accepts no replay commands.");
        }

        public void RunOneTick()
        {
            CurrentTick++;
            MovementAgentState next = AuthoritativeMovementPlanner.PlanNext(
                _world.Map,
                Movement,
                static _ => true);
            if (next != Movement)
            {
                _world.Mutations.EnqueueReplace(
                    _entityId,
                    new ComponentValue(MovementAgentState.ComponentTypeId, next));
                _world.CommitMutations(_scheduler, new SimTick(CurrentTick));
            }
        }

        public ulong CaptureChecksum() =>
            StateChecksum.Compute(new SimTick(CurrentTick), _world, _scheduler);

        public SimulationSaveEnvelope CaptureSave() =>
            throw new NotSupportedException("Movement replay equivalence does not capture saves.");
    }
}
