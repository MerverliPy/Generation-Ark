using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.Map;

public static class RoomTopologyBuilder
{
    public static RoomTopology Build(MapState map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var visited = new bool[map.CellCount];
        var rooms = new SortedDictionary<RoomId, MapCellId[]>();

        foreach (MapCellId seed in map.EnumerateCellsCanonical())
        {
            if (visited[seed.Value]
                || !map.Definitions.Get(map.GetCellDefinition(seed)).ParticipatesInRoomTopology)
            {
                continue;
            }

            var queue = new Queue<MapCellId>();
            var members = new List<MapCellId>();
            visited[seed.Value] = true;
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                MapCellId current = queue.Dequeue();
                members.Add(current);
                GridPosition position = current.ToPosition(map.Width, map.Height);
                Enqueue(position.X, position.Y - 1);
                Enqueue(position.X + 1, position.Y);
                Enqueue(position.X, position.Y + 1);
                Enqueue(position.X - 1, position.Y);
            }

            MapCellId[] canonicalMembers = members.OrderBy(static cell => cell).ToArray();
            var roomId = new RoomId(canonicalMembers[0].Value);
            rooms.Add(roomId, canonicalMembers);

            void Enqueue(int x, int y)
            {
                if ((uint)x >= (uint)map.Width || (uint)y >= (uint)map.Height)
                {
                    return;
                }

                MapCellId neighbor = MapCellId.FromPosition(
                    new GridPosition(x, y),
                    map.Width,
                    map.Height);
                if (visited[neighbor.Value]
                    || !map.Definitions.Get(map.GetCellDefinition(neighbor)).ParticipatesInRoomTopology)
                {
                    return;
                }

                visited[neighbor.Value] = true;
                queue.Enqueue(neighbor);
            }
        }

        return new RoomTopology(map.CellCount, rooms);
    }
}
