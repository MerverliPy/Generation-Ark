using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.Movement;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Tests;

internal static class PathfindingMilestoneTests
{
    private static readonly MapCellDefinitionId FloorDefinition = new(1);

    public static void CardinalRouteUsesCanonicalTieBreaking()
    {
        MapState map = CreateMap(3, 3);
        IReadOnlyList<MapCellId> route = DeterministicPathfinder.FindPath(
            map,
            Cell(0, 0, map),
            Cell(2, 2, map),
            static _ => true);

        TestAssert.Equal("0,1,2,5,8", CanonicalRoute(route));
    }

    public static void BlockedCellsAreAvoidedAndBlockedDestinationFails()
    {
        MapState map = CreateMap(4, 3);
        var blocked = new HashSet<MapCellId>
        {
            Cell(1, 0, map),
            Cell(1, 1, map)
        };

        IReadOnlyList<MapCellId> route = DeterministicPathfinder.FindPath(
            map,
            Cell(0, 0, map),
            Cell(3, 0, map),
            cell => !blocked.Contains(cell));

        TestAssert.Equal("0,4,8,9,10,6,2,3", CanonicalRoute(route));

        blocked.Add(Cell(3, 0, map));
        TestAssert.Equal(
            0,
            DeterministicPathfinder.FindPath(
                map,
                Cell(0, 0, map),
                Cell(3, 0, map),
                cell => !blocked.Contains(cell)).Count);
    }

    public static void RepathAfterObstructionChangeIsDeterministic()
    {
        MapState map = CreateMap(5, 3);
        var blocked = new HashSet<MapCellId>();
        MapCellId start = Cell(0, 1, map);
        MapCellId destination = Cell(4, 1, map);

        IReadOnlyList<MapCellId> first = DeterministicPathfinder.FindPath(
            map, start, destination, cell => !blocked.Contains(cell));
        blocked.Add(Cell(2, 1, map));
        IReadOnlyList<MapCellId> second = DeterministicPathfinder.FindPath(
            map, start, destination, cell => !blocked.Contains(cell));
        IReadOnlyList<MapCellId> repeat = DeterministicPathfinder.FindPath(
            map, start, destination, cell => !blocked.Contains(cell));

        TestAssert.Equal("5,6,7,8,9", CanonicalRoute(first));
        TestAssert.Equal(CanonicalRoute(second), CanonicalRoute(repeat));
        TestAssert.True(!second.Contains(Cell(2, 1, map)));
    }

    public static void OneHundredConcurrentRoutesCompleteWithoutDivergence()
    {
        MapState map = CreateMap(20, 20);
        var routes = new ConcurrentBag<string>();

        Parallel.For(0, 100, _ =>
        {
            IReadOnlyList<MapCellId> route = DeterministicPathfinder.FindPath(
                map,
                Cell(0, 0, map),
                Cell(19, 19, map),
                static _ => true);
            routes.Add(CanonicalRoute(route));
        });

        TestAssert.Equal(100, routes.Count);
        TestAssert.Equal(1, routes.Distinct(StringComparer.Ordinal).Count());
    }

    public static void MovementAgentStateSerializationAndChecksumAreCanonical()
    {
        var state = new MovementAgentState(new MapCellId(3), new MapCellId(9), 7UL);
        string payload = MovementAgentState.Serialize(state);
        MovementAgentState restored = MovementAgentState.Deserialize(payload);
        TestAssert.Equal("3:9:7", payload);
        TestAssert.Equal(state, restored);

        ComponentRegistration registration = MovementAgentState.CreateRegistration();
        var first = new StateChecksumWriter();
        var second = new StateChecksumWriter();
        registration.WriteChecksum(first, state);
        registration.WriteChecksum(second, restored);
        TestAssert.Equal(first.Value, second.Value);
    }

    public static void AuthoritativePlannerAdvancesOneCanonicalCellPerCommitIntent()
    {
        MapState map = CreateMap(3, 2);
        var initial = new MovementAgentState(Cell(0, 0, map), Cell(2, 1, map), 0UL);
        MovementAgentState next = AuthoritativeMovementPlanner.PlanNext(map, initial, static _ => true);

        TestAssert.Equal(Cell(1, 0, map), next.CurrentCell);
        TestAssert.Equal(initial.DestinationCell, next.DestinationCell);
        TestAssert.Equal(1UL, next.RouteRevision);
    }

    private static MapState CreateMap(int width, int height)
    {
        var registry = new MapCellDefinitionRegistry(new[]
        {
            new MapCellDefinition(FloorDefinition, ParticipatesInRoomTopology: true)
        });
        return new MapState(width, height, registry, FloorDefinition);
    }

    private static MapCellId Cell(int x, int y, MapState map) =>
        MapCellId.FromPosition(new GridPosition(x, y), map.Width, map.Height);

    private static string CanonicalRoute(IEnumerable<MapCellId> route) =>
        string.Join(",", route.Select(static cell => cell.Value));
}
