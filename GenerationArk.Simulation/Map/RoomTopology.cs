using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GenerationArk.Simulation.Map;

public sealed class RoomTopology
{
    private readonly SortedDictionary<RoomId, ReadOnlyCollection<MapCellId>> _membersByRoom;
    private readonly int[] _roomIdByCell;

    internal RoomTopology(
        int cellCount,
        IEnumerable<KeyValuePair<RoomId, MapCellId[]>> rooms)
    {
        if (cellCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellCount), cellCount, "Cell count must be positive.");
        }
        ArgumentNullException.ThrowIfNull(rooms);

        _membersByRoom = new SortedDictionary<RoomId, ReadOnlyCollection<MapCellId>>();
        _roomIdByCell = Enumerable.Repeat(-1, cellCount).ToArray();
        foreach ((RoomId roomId, MapCellId[] members) in rooms)
        {
            ArgumentNullException.ThrowIfNull(members);
            MapCellId[] canonicalMembers = members.OrderBy(static cell => cell).ToArray();
            if (canonicalMembers.Length == 0)
            {
                throw new InvalidOperationException($"Room {roomId} cannot be empty.");
            }
            if (roomId.Value != canonicalMembers[0].Value)
            {
                throw new InvalidOperationException(
                    $"Room ID {roomId} must equal minimum member cell {canonicalMembers[0]}.");
            }
            if (!_membersByRoom.TryAdd(roomId, Array.AsReadOnly(canonicalMembers)))
            {
                throw new InvalidOperationException($"Duplicate room ID {roomId}.");
            }

            foreach (MapCellId cell in canonicalMembers)
            {
                if ((uint)cell.Value >= (uint)_roomIdByCell.Length)
                {
                    throw new InvalidOperationException(
                        $"Room {roomId} contains out-of-range cell {cell}.");
                }
                if (_roomIdByCell[cell.Value] >= 0)
                {
                    throw new InvalidOperationException(
                        $"Cell {cell} belongs to multiple rooms.");
                }
                _roomIdByCell[cell.Value] = roomId.Value;
            }
        }
    }

    public int RoomCount => _membersByRoom.Count;

    public IReadOnlyList<RoomId> RoomIds => _membersByRoom.Keys.ToArray();

    public IReadOnlyList<MapCellId> GetMembers(RoomId roomId)
        => _membersByRoom.TryGetValue(roomId, out ReadOnlyCollection<MapCellId>? members)
            ? members
            : throw new KeyNotFoundException($"Unknown room ID {roomId}.");

    public RoomId? GetRoomId(MapCellId cell)
    {
        if ((uint)cell.Value >= (uint)_roomIdByCell.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cell ID is outside the map.");
        }

        int roomId = _roomIdByCell[cell.Value];
        return roomId >= 0 ? new RoomId(roomId) : null;
    }

    public bool TryGetRoomId(MapCellId cell, out RoomId roomId)
    {
        RoomId? value = GetRoomId(cell);
        if (value is RoomId found)
        {
            roomId = found;
            return true;
        }

        roomId = default;
        return false;
    }

    internal bool CanonicallyEquals(RoomTopology other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (RoomCount != other.RoomCount)
        {
            return false;
        }

        RoomId[] leftIds = RoomIds.ToArray();
        RoomId[] rightIds = other.RoomIds.ToArray();
        if (!leftIds.SequenceEqual(rightIds))
        {
            return false;
        }

        foreach (RoomId roomId in leftIds)
        {
            if (!GetMembers(roomId).SequenceEqual(other.GetMembers(roomId)))
            {
                return false;
            }
        }
        return true;
    }
}
