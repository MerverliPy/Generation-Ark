using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Map;

/// <summary>Canonical entity-to-cell mapping and validated derived cell membership.</summary>
public sealed class SpatialEntityIndex
{
    private readonly SortedDictionary<EntityId, EntityPosition> _positions = new();
    private readonly SortedDictionary<MapCellId, SortedSet<EntityId>> _membersByCell = new();

    public int Count => _positions.Count;

    public IReadOnlyList<EntityId> PositionedEntityIds => _positions.Keys.ToArray();

    public IReadOnlyList<MapCellId> OccupiedCells => _membersByCell.Keys.ToArray();

    public bool TryGetPosition(EntityId entityId, out EntityPosition position)
        => _positions.TryGetValue(entityId, out position);

    public IReadOnlyList<EntityId> GetEntities(MapCellId cell)
        => _membersByCell.TryGetValue(cell, out SortedSet<EntityId>? members)
            ? members.ToArray()
            : Array.Empty<EntityId>();

    public IReadOnlyList<KeyValuePair<EntityId, EntityPosition>> EnumerateCanonical()
        => _positions.ToArray();

    internal void ApplySet(EntityId entityId, EntityPosition position)
    {
        if (_positions.TryGetValue(entityId, out EntityPosition previous))
        {
            RemoveMembership(previous.Cell, entityId);
        }

        _positions[entityId] = position;
        if (!_membersByCell.TryGetValue(position.Cell, out SortedSet<EntityId>? members))
        {
            members = new SortedSet<EntityId>();
            _membersByCell.Add(position.Cell, members);
        }
        members.Add(entityId);
    }

    internal void ApplyClear(EntityId entityId)
    {
        if (_positions.TryGetValue(entityId, out EntityPosition position))
        {
            _positions.Remove(entityId);
            RemoveMembership(position.Cell, entityId);
        }
    }

    internal void Restore(
        IEnumerable<KeyValuePair<EntityId, EntityPosition>> positions,
        EntityRegistry entities,
        MapState map)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(map);

        KeyValuePair<EntityId, EntityPosition>[] ordered = positions
            .OrderBy(static entry => entry.Key)
            .ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].Key == ordered[index].Key)
            {
                throw new InvalidOperationException(
                    $"Spatial state contains duplicate entity {ordered[index].Key}.");
            }
        }

        foreach ((EntityId entityId, EntityPosition position) in ordered)
        {
            ValidateTarget(entityId, position, entities, map);
        }

        _positions.Clear();
        _membersByCell.Clear();
        foreach ((EntityId entityId, EntityPosition position) in ordered)
        {
            ApplySet(entityId, position);
        }
        ValidateInvariants(entities, map);
    }

    internal void ValidateInvariants(EntityRegistry entities, MapState map)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(map);
        foreach ((EntityId entityId, EntityPosition position) in _positions)
        {
            ValidateTarget(entityId, position, entities, map);
            if (!_membersByCell.TryGetValue(position.Cell, out SortedSet<EntityId>? members)
                || !members.Contains(entityId))
            {
                throw new InvalidOperationException(
                    $"Spatial reverse membership is missing entity {entityId} at cell {position.Cell}.");
            }
        }

        foreach ((MapCellId cell, SortedSet<EntityId> members) in _membersByCell)
        {
            map.GetCellDefinition(cell);
            if (members.Count == 0)
            {
                throw new InvalidOperationException($"Spatial membership for cell {cell} is empty.");
            }
            foreach (EntityId entityId in members)
            {
                if (!_positions.TryGetValue(entityId, out EntityPosition position)
                    || position.Cell != cell)
                {
                    throw new InvalidOperationException(
                        $"Spatial reverse membership disagrees for entity {entityId} at cell {cell}.");
                }
            }
        }
    }

    internal void WriteChecksum(StateChecksumWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.AddInt32(Count);
        foreach ((EntityId entityId, EntityPosition position) in _positions)
        {
            writer.AddUInt64(entityId.Value);
            writer.AddInt32(position.Cell.Value);
        }
        writer.AddInt32(_membersByCell.Count);
        foreach ((MapCellId cell, SortedSet<EntityId> members) in _membersByCell)
        {
            writer.AddInt32(cell.Value);
            writer.AddInt32(members.Count);
            foreach (EntityId entityId in members)
            {
                writer.AddUInt64(entityId.Value);
            }
        }
    }

    private void RemoveMembership(MapCellId cell, EntityId entityId)
    {
        if (!_membersByCell.TryGetValue(cell, out SortedSet<EntityId>? members)
            || !members.Remove(entityId))
        {
            throw new InvalidOperationException(
                $"Spatial reverse membership is missing entity {entityId} at cell {cell}.");
        }
        if (members.Count == 0)
        {
            _membersByCell.Remove(cell);
        }
    }

    private static void ValidateTarget(
        EntityId entityId,
        EntityPosition position,
        EntityRegistry entities,
        MapState map)
    {
        if (!entities.Contains(entityId))
        {
            throw new InvalidOperationException($"Spatial position targets missing entity {entityId}.");
        }
        if (entities.GetLifecycleState(entityId) != EntityLifecycleState.Active)
        {
            throw new InvalidOperationException(
                $"Spatial position targets non-active entity {entityId}.");
        }
        map.GetCellDefinition(position.Cell);
    }
}
