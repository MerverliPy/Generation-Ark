using System;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.Movement;
using GenerationArk.Simulation.Persistence;
using GenerationArk.Simulation.Replay;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

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

    private sealed class MovementScenarioFactory : IReplaySimulationFactory
    {
        public IReplaySimulationSession CreateNew() => new MovementScenarioSession();

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
