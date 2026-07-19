using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Map;

namespace GenerationArk.Simulation.Movement;

public static class DeterministicPathfinder
{
    public static IReadOnlyList<MapCellId> FindPath(
        MapState map,
        MapCellId start,
        MapCellId destination,
        Func<MapCellId, bool> isWalkable)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(isWalkable);

        ValidateCell(map, start, nameof(start));
        ValidateCell(map, destination, nameof(destination));

        if (start == destination)
        {
            return new[] { start };
        }
        if (!isWalkable(destination))
        {
            return Array.Empty<MapCellId>();
        }

        int[] previous = new int[map.CellCount];
        Array.Fill(previous, -1);
        bool[] visited = new bool[map.CellCount];
        var queue = new Queue<MapCellId>();
        visited[start.Value] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            MapCellId current = queue.Dequeue();
            foreach (MapCellId neighbor in EnumerateNeighborsCanonical(map, current))
            {
                if (visited[neighbor.Value] || !isWalkable(neighbor))
                {
                    continue;
                }

                visited[neighbor.Value] = true;
                previous[neighbor.Value] = current.Value;
                if (neighbor == destination)
                {
                    return Reconstruct(start, destination, previous);
                }
                queue.Enqueue(neighbor);
            }
        }

        return Array.Empty<MapCellId>();
    }

    private static IEnumerable<MapCellId> EnumerateNeighborsCanonical(MapState map, MapCellId cell)
    {
        GridPosition position = cell.ToPosition(map.Width, map.Height);
        Span<MapCellId> candidates = stackalloc MapCellId[4];
        int count = 0;

        Add(position.X, position.Y - 1);
        Add(position.X - 1, position.Y);
        Add(position.X + 1, position.Y);
        Add(position.X, position.Y + 1);

        for (int left = 1; left < count; left++)
        {
            MapCellId value = candidates[left];
            int right = left - 1;
            while (right >= 0 && candidates[right].Value > value.Value)
            {
                candidates[right + 1] = candidates[right];
                right--;
            }
            candidates[right + 1] = value;
        }

        for (int index = 0; index < count; index++)
        {
            yield return candidates[index];
        }

        void Add(int x, int y)
        {
            if ((uint)x < (uint)map.Width && (uint)y < (uint)map.Height)
            {
                candidates[count++] = MapCellId.FromPosition(new GridPosition(x, y), map.Width, map.Height);
            }
        }
    }

    private static IReadOnlyList<MapCellId> Reconstruct(
        MapCellId start,
        MapCellId destination,
        IReadOnlyList<int> previous)
    {
        var reversed = new List<MapCellId> { destination };
        int current = destination.Value;
        while (current != start.Value)
        {
            current = previous[current];
            if (current < 0)
            {
                throw new InvalidOperationException("Path reconstruction encountered an incomplete predecessor chain.");
            }
            reversed.Add(new MapCellId(current));
        }
        reversed.Reverse();
        return reversed;
    }

    private static void ValidateCell(MapState map, MapCellId cell, string parameterName)
    {
        if ((uint)cell.Value >= (uint)map.CellCount)
        {
            throw new ArgumentOutOfRangeException(parameterName, cell, $"Cell ID must be between 0 and {map.CellCount - 1}.");
        }
    }
}
