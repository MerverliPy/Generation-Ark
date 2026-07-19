using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.Persistence;
using GenerationArk.Simulation.Scheduling;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Tests;

internal static class MapTopologyMilestoneTests
{
    private static readonly MapCellDefinitionId VoidDefinition = new(0);
    private static readonly MapCellDefinitionId FloorDefinition = new(1);
    private static readonly MapCellDefinitionId AlternateFloorDefinition = new(2);

    public static void CellIdsUseCanonicalRowMajorCoordinates()
    {
        TestAssert.Equal(new MapCellId(0), MapCellId.FromPosition(new GridPosition(0, 0), 4, 3));
        TestAssert.Equal(new MapCellId(6), MapCellId.FromPosition(new GridPosition(2, 1), 4, 3));
        TestAssert.Equal(new MapCellId(11), MapCellId.FromPosition(new GridPosition(3, 2), 4, 3));
        TestAssert.Equal(new GridPosition(2, 1), new MapCellId(6).ToPosition(4, 3));
        TestAssert.Equal(new GridPosition(3, 2), new MapCellId(11).ToPosition(4, 3));

        MapState map = CreateMap(4, 3);
        TestAssert.Equal(new MapCellId(0), map.EnumerateCellsCanonical().First());
        TestAssert.Equal(new MapCellId(11), map.EnumerateCellsCanonical().Last());
    }

    public static void InvalidGridDimensionsAndCoordinatesFailFast()
    {
        MapCellDefinitionRegistry definitions = Definitions();
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => new MapState(0, 1, definitions, VoidDefinition));
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => new MapState(1, -1, definitions, VoidDefinition));
        TestAssert.Throws<OverflowException>(
            () => new MapState(int.MaxValue, 2, definitions, VoidDefinition));
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => MapCellId.FromPosition(new GridPosition(-1, 0), 3, 2));
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => MapCellId.FromPosition(new GridPosition(3, 0), 3, 2));
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => new MapCellId(-1).ToPosition(3, 2));
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => new MapCellId(6).ToPosition(3, 2));

        MapState map = CreateMap(3, 2);
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => map.GetCellDefinition(new MapCellId(6)));
    }

    public static void CellIterationIsCanonicalRegardlessOfWriteOrder()
    {
        WorldState first = CreateWorld(4, 2);
        WorldState second = CreateWorld(4, 2);
        DeterministicScheduler firstScheduler = CreateScheduler();
        DeterministicScheduler secondScheduler = CreateScheduler();

        MapCellId[] cells =
        {
            Cell(3, 1, 4, 2),
            Cell(0, 0, 4, 2),
            Cell(2, 0, 4, 2),
            Cell(1, 1, 4, 2)
        };
        foreach (MapCellId cell in cells)
        {
            first.Mutations.EnqueueSetCellDefinition(cell, FloorDefinition);
        }
        foreach (MapCellId cell in cells.Reverse())
        {
            second.Mutations.EnqueueSetCellDefinition(cell, FloorDefinition);
        }

        first.CommitMutations(firstScheduler, new SimTick(1));
        second.CommitMutations(secondScheduler, new SimTick(1));

        TestAssert.Equal(
            CanonicalDefinitions(first.Map),
            CanonicalDefinitions(second.Map));
        TestAssert.Equal(
            StateChecksum.Compute(new SimTick(1), first),
            StateChecksum.Compute(new SimTick(1), second));

        WorldState divergent = CreateWorld(4, 2);
        divergent.Mutations.EnqueueSetCellDefinition(new MapCellId(0), FloorDefinition);
        divergent.CommitMutations(CreateScheduler(), new SimTick(1));
        DesyncReport report = DesyncDetector.FindFirstDivergence(
            new[] { StateChecksum.ComputeDetailed(new SimTick(1), first) },
            new[] { StateChecksum.ComputeDetailed(new SimTick(1), divergent) })
            ?? throw new InvalidOperationException("Expected map divergence.");
        TestAssert.True(report.ComponentDifferences.Any(static difference =>
            difference.ComponentId == new ChecksumComponentId("map-topology")));
    }

    public static void DuplicateMapCellDefinitionIdsFailFast()
    {
        TestAssert.Throws<InvalidOperationException>(() =>
            new MapCellDefinitionRegistry(new[]
            {
                new MapCellDefinition(FloorDefinition, true),
                new MapCellDefinition(VoidDefinition, false),
                new MapCellDefinition(FloorDefinition, false)
            }));

        MapCellDefinitionRegistry first = new(new[]
        {
            new MapCellDefinition(AlternateFloorDefinition, true),
            new MapCellDefinition(VoidDefinition, false),
            new MapCellDefinition(FloorDefinition, true)
        });
        MapCellDefinitionRegistry second = new(new[]
        {
            new MapCellDefinition(FloorDefinition, true),
            new MapCellDefinition(AlternateFloorDefinition, true),
            new MapCellDefinition(VoidDefinition, false)
        });
        TestAssert.Equal(
            "0|1|2",
            string.Join("|", first.Definitions.Select(static definition => definition.Id.Value)));
        TestAssert.Equal(
            string.Join("|", first.Definitions.Select(static definition => definition.Id.Value)),
            string.Join("|", second.Definitions.Select(static definition => definition.Id.Value)));
    }

    public static void MapMutationsRemainInvisibleUntilCommit()
    {
        WorldState world = CreateWorld(3, 1);
        DeterministicScheduler scheduler = CreateScheduler();
        MapCellId middle = Cell(1, 0, 3, 1);

        world.Mutations.EnqueueSetCellDefinition(middle, FloorDefinition);

        TestAssert.Equal(VoidDefinition, world.Map.GetCellDefinition(middle));
        TestAssert.Equal(0, world.Map.Topology.RoomCount);
        TestAssert.Equal(1, world.Mutations.MapMutationCount);

        MutationCommitResult result = world.CommitMutations(scheduler, new SimTick(1));

        TestAssert.Equal(1, result.AppliedMutationCount);
        TestAssert.Equal(FloorDefinition, world.Map.GetCellDefinition(middle));
        TestAssert.Equal(1, world.Map.Topology.RoomCount);
        TestAssert.Equal(new RoomId(middle.Value), world.Map.Topology.RoomIds.Single());
        TestAssert.Equal(0, world.Mutations.Count);
    }

    public static void ConflictingMapMutationBatchFailsBeforePartialApplication()
    {
        WorldState world = CreateWorld(3, 1);
        DeterministicScheduler scheduler = CreateScheduler();
        MapCellId middle = Cell(1, 0, 3, 1);
        ulong before = StateChecksum.Compute(SimTick.Zero, world);

        MapCellId independentValid = Cell(0, 0, 3, 1);
        world.Mutations.EnqueueSetCellDefinition(independentValid, FloorDefinition);
        world.Mutations.EnqueueSetCellDefinition(middle, FloorDefinition);
        world.Mutations.EnqueueSetCellDefinition(middle, AlternateFloorDefinition);

        MapMutationValidationException exception = TestAssert.Throws<MapMutationValidationException>(() =>
            world.CommitMutations(scheduler, new SimTick(1)));

        TestAssert.Equal(2, exception.ConflictingMutations.Count);
        TestAssert.Equal(VoidDefinition, world.Map.GetCellDefinition(independentValid));
        TestAssert.Equal(VoidDefinition, world.Map.GetCellDefinition(middle));
        TestAssert.Equal(0, world.Map.Topology.RoomCount);
        TestAssert.Equal(3, world.Mutations.Count);
        TestAssert.Equal(before, StateChecksum.Compute(SimTick.Zero, world));

        WorldState invalid = CreateWorld(3, 1);
        invalid.Mutations.EnqueueSetCellDefinition(independentValid, FloorDefinition);
        invalid.Mutations.EnqueueSetCellDefinition(middle, new MapCellDefinitionId(999));
        TestAssert.Throws<MapMutationValidationException>(() =>
            invalid.CommitMutations(CreateScheduler(), new SimTick(1)));
        TestAssert.Equal(VoidDefinition, invalid.Map.GetCellDefinition(independentValid));
        TestAssert.Equal(VoidDefinition, invalid.Map.GetCellDefinition(middle));
        TestAssert.Equal(0, invalid.Map.Topology.RoomCount);
        TestAssert.Equal(2, invalid.Mutations.Count);
    }

    public static void RoomTopologyUsesCardinalConnectivityAndStableRoomIds()
    {
        WorldState world = CreateWorld(3, 3);
        DeterministicScheduler scheduler = CreateScheduler();
        foreach (GridPosition position in new[]
        {
            new GridPosition(0, 0),
            new GridPosition(1, 1),
            new GridPosition(2, 1),
            new GridPosition(2, 2)
        })
        {
            world.Mutations.EnqueueSetCellDefinition(
                MapCellId.FromPosition(position, 3, 3),
                FloorDefinition);
        }
        world.CommitMutations(scheduler, new SimTick(1));

        RoomTopology topology = world.Map.Topology;
        TestAssert.Equal(2, topology.RoomCount);
        TestAssert.Equal("0|4", string.Join("|", topology.RoomIds.Select(static id => id.Value)));
        TestAssert.Equal("0", FormatCells(topology.GetMembers(new RoomId(0))));
        TestAssert.Equal("4|5|8", FormatCells(topology.GetMembers(new RoomId(4))));
        TestAssert.Equal(new RoomId(0), topology.GetRoomId(new MapCellId(0))!.Value);
        TestAssert.Equal(new RoomId(4), topology.GetRoomId(new MapCellId(8))!.Value);
        TestAssert.Equal<RoomId?>(null, topology.GetRoomId(new MapCellId(1)));
    }

    public static void RoomTopologySplitAndMergeRebuildsDeterministically()
    {
        WorldState first = CreateFilledWorld(5, 1);
        WorldState second = CreateFilledWorld(5, 1);
        DeterministicScheduler firstScheduler = CreateScheduler();
        DeterministicScheduler secondScheduler = CreateScheduler();
        MapCellId connector = Cell(2, 0, 5, 1);

        first.Mutations.EnqueueSetCellDefinition(connector, VoidDefinition);
        second.Mutations.EnqueueSetCellDefinition(connector, VoidDefinition);
        first.CommitMutations(firstScheduler, new SimTick(1));
        second.CommitMutations(secondScheduler, new SimTick(1));

        TestAssert.Equal("0|3", string.Join("|", first.Map.Topology.RoomIds.Select(static id => id.Value)));
        TestAssert.Equal("0|1", FormatCells(first.Map.Topology.GetMembers(new RoomId(0))));
        TestAssert.Equal("3|4", FormatCells(first.Map.Topology.GetMembers(new RoomId(3))));

        first.Mutations.EnqueueSetCellDefinition(new MapCellId(4), AlternateFloorDefinition);
        first.Mutations.EnqueueSetCellDefinition(connector, FloorDefinition);
        second.Mutations.EnqueueSetCellDefinition(connector, FloorDefinition);
        second.Mutations.EnqueueSetCellDefinition(new MapCellId(4), AlternateFloorDefinition);
        first.CommitMutations(firstScheduler, new SimTick(2));
        second.CommitMutations(secondScheduler, new SimTick(2));

        TestAssert.Equal(1, first.Map.Topology.RoomCount);
        TestAssert.Equal(new RoomId(0), first.Map.Topology.RoomIds.Single());
        TestAssert.Equal("0|1|2|3|4", FormatCells(first.Map.Topology.GetMembers(new RoomId(0))));
        TestAssert.Equal(
            StateChecksum.Compute(new SimTick(2), first),
            StateChecksum.Compute(new SimTick(2), second));
    }

    public static void MapStateSaveLoadRoundTripIsCanonical()
    {
        WorldState world = CreateWorld(4, 2);
        DeterministicScheduler scheduler = CreateScheduler();
        foreach (MapCellId cell in new[]
        {
            new MapCellId(0),
            new MapCellId(1),
            new MapCellId(4),
            new MapCellId(7)
        })
        {
            world.Mutations.EnqueueSetCellDefinition(cell, FloorDefinition);
        }
        world.Mutations.EnqueueSetCellDefinition(new MapCellId(6), AlternateFloorDefinition);
        world.CommitMutations(scheduler, new SimTick(1));

        MapStateSnapshot captured = MapStateSerializer.Capture(world.Map);
        var permuted = new MapStateSnapshot(
            captured.SchemaVersion,
            captured.Width,
            captured.Height,
            captured.Cells.Reverse());
        byte[] canonical = MapStateSerializer.ToUtf8(captured);
        byte[] canonicalFromPermutation = MapStateSerializer.ToUtf8(permuted);
        MapState restoredMap = MapStateSerializer.Restore(permuted, Definitions());
        byte[] restored = MapStateSerializer.ToUtf8(restoredMap);

        TestAssert.Equal(Convert.ToHexString(canonical), Convert.ToHexString(canonicalFromPermutation));
        TestAssert.Equal(Convert.ToHexString(canonical), Convert.ToHexString(restored));
        TestAssert.Equal(
            FormatTopology(world.Map.Topology),
            FormatTopology(restoredMap.Topology));
        TestAssert.Equal(
            StateChecksum.Compute(new SimTick(1), world),
            StateChecksum.Compute(new SimTick(1), new WorldState(map: restoredMap)));
    }

    public static void MapReplayFramePatternsAndTopologyChurnMatchChecksums()
    {
        MapChurnResult stable = RunMapChurn(new[] { 1 }, reverseRequests: false);
        MapChurnResult repeated = RunMapChurn(new[] { 1 }, reverseRequests: true);
        MapChurnResult varied = RunMapChurn(new[] { 4, 1, 7, 2 }, reverseRequests: true);
        MapChurnResult wide = RunMapChurn(new[] { 13, 3, 5 }, reverseRequests: false);

        AssertChurnEquivalent(stable, repeated);
        AssertChurnEquivalent(stable, varied);
        AssertChurnEquivalent(stable, wide);
        TestAssert.True(stable.RetainedCheckpoints.Count <= 8);
        TestAssert.Equal(240L, stable.FinalTick);
    }

    private static MapChurnResult RunMapChurn(
        IReadOnlyList<int> framePattern,
        bool reverseRequests)
    {
        if (framePattern.Count == 0 || framePattern.Any(static value => value <= 0))
        {
            throw new ArgumentException("Frame pattern must contain positive tick budgets.", nameof(framePattern));
        }

        MapCellDefinitionRegistry definitions = Definitions();
        WorldState world = new(map: new MapState(8, 3, definitions, VoidDefinition));
        DeterministicScheduler scheduler = CreateScheduler();
        IReadOnlyList<MapCommand> commands = CreateCommandLog();
        var retainedCheckpoints = new Queue<ulong>();
        long tick = 0;
        int frame = 0;

        while (tick < 240)
        {
            int budget = framePattern[frame % framePattern.Count];
            frame++;
            for (int step = 0; step < budget && tick < 240; step++)
            {
                tick++;
                MapCommand[] due = commands
                    .Where(command => command.Tick == tick)
                    .OrderBy(static command => command.Sequence)
                    .ToArray();
                IEnumerable<MapCommand> requested = reverseRequests ? due.Reverse() : due;
                foreach (MapCommand command in requested)
                {
                    world.Mutations.EnqueueSetCellDefinition(command.Cell, command.Definition);
                }
                if (world.Mutations.Count > 0)
                {
                    world.CommitMutations(scheduler, new SimTick(tick));
                }

                if (tick == 120)
                {
                    byte[] save = MapStateSerializer.ToUtf8(world.Map);
                    MapState restored = MapStateSerializer.Restore(save, definitions);
                    world = new WorldState(map: restored);
                }

                if (tick % 20 == 0)
                {
                    retainedCheckpoints.Enqueue(StateChecksum.Compute(new SimTick(tick), world));
                    while (retainedCheckpoints.Count > 8)
                    {
                        retainedCheckpoints.Dequeue();
                    }
                }
            }
        }

        return new MapChurnResult(
            tick,
            StateChecksum.Compute(new SimTick(tick), world),
            retainedCheckpoints.ToArray(),
            FormatTopology(world.Map.Topology),
            Convert.ToHexString(MapStateSerializer.ToUtf8(world.Map)));
    }

    private static IReadOnlyList<MapCommand> CreateCommandLog()
    {
        var commands = new List<MapCommand>();
        long sequence = 1;
        for (int x = 0; x < 8; x++)
        {
            commands.Add(new MapCommand(
                1,
                sequence++,
                Cell(x, 1, 8, 3),
                FloorDefinition));
        }
        commands.Add(new MapCommand(1, sequence++, Cell(0, 0, 8, 3), FloorDefinition));
        commands.Add(new MapCommand(1, sequence++, Cell(7, 2, 8, 3), AlternateFloorDefinition));

        for (int tick = 10; tick <= 240; tick += 10)
        {
            int x = 1 + ((tick / 10) % 6);
            MapCellDefinitionId definition = (tick / 10) % 2 == 0
                ? FloorDefinition
                : VoidDefinition;
            commands.Add(new MapCommand(
                tick,
                sequence++,
                Cell(x, 1, 8, 3),
                definition));

            if (tick % 30 == 0)
            {
                int edgeX = (tick / 30) % 2 == 0 ? 0 : 7;
                int edgeY = edgeX == 0 ? 0 : 2;
                commands.Add(new MapCommand(
                    tick,
                    sequence++,
                    Cell(edgeX, edgeY, 8, 3),
                    definition == VoidDefinition
                        ? VoidDefinition
                        : AlternateFloorDefinition));
            }
        }
        return commands;
    }

    private static void AssertChurnEquivalent(MapChurnResult expected, MapChurnResult actual)
    {
        TestAssert.Equal(expected.FinalTick, actual.FinalTick);
        TestAssert.Equal(expected.FinalChecksum, actual.FinalChecksum);
        TestAssert.Equal(
            string.Join("|", expected.RetainedCheckpoints),
            string.Join("|", actual.RetainedCheckpoints));
        TestAssert.Equal(expected.Topology, actual.Topology);
        TestAssert.Equal(expected.CanonicalMapHex, actual.CanonicalMapHex);
    }

    private static WorldState CreateWorld(int width, int height)
        => new(map: CreateMap(width, height));

    private static WorldState CreateFilledWorld(int width, int height)
        => new(map: new MapState(width, height, Definitions(), FloorDefinition));

    private static MapState CreateMap(int width, int height)
        => new(width, height, Definitions(), VoidDefinition);

    private static MapCellDefinitionRegistry Definitions()
        => new(new[]
        {
            new MapCellDefinition(VoidDefinition, ParticipatesInRoomTopology: false),
            new MapCellDefinition(FloorDefinition, ParticipatesInRoomTopology: true),
            new MapCellDefinition(AlternateFloorDefinition, ParticipatesInRoomTopology: true)
        });

    private static DeterministicScheduler CreateScheduler()
        => new(new ScheduledEventHandlerRegistry(Array.Empty<IScheduledEventHandler>()));

    private static MapCellId Cell(int x, int y, int width, int height)
        => MapCellId.FromPosition(new GridPosition(x, y), width, height);

    private static string CanonicalDefinitions(MapState map)
        => string.Join(
            "|",
            map.EnumerateCellsCanonical().Select(cell =>
                $"{cell.Value}:{map.GetCellDefinition(cell).Value}"));

    private static string FormatCells(IEnumerable<MapCellId> cells)
        => string.Join("|", cells.Select(static cell => cell.Value));

    private static string FormatTopology(RoomTopology topology)
        => string.Join(
            ";",
            topology.RoomIds.Select(roomId =>
                $"{roomId.Value}:{FormatCells(topology.GetMembers(roomId))}"));

    private sealed record MapCommand(
        long Tick,
        long Sequence,
        MapCellId Cell,
        MapCellDefinitionId Definition);

    private sealed record MapChurnResult(
        long FinalTick,
        ulong FinalChecksum,
        IReadOnlyList<ulong> RetainedCheckpoints,
        string Topology,
        string CanonicalMapHex);
}
