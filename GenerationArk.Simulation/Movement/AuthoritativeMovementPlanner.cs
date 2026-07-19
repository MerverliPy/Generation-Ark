using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Map;

namespace GenerationArk.Simulation.Movement;

public static class AuthoritativeMovementPlanner
{
    public static MovementAgentState PlanNext(
        MapState map,
        MovementAgentState current,
        Func<MapCellId, bool> isWalkable)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(isWalkable);

        IReadOnlyList<MapCellId> route = DeterministicPathfinder.FindPath(
            map,
            current.CurrentCell,
            current.DestinationCell,
            isWalkable);

        if (route.Count <= 1)
        {
            return current;
        }

        return current with
        {
            CurrentCell = route[1],
            RouteRevision = checked(current.RouteRevision + 1UL)
        };
    }
}
