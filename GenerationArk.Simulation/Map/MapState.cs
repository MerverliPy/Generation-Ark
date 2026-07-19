using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Persistence;

namespace GenerationArk.Simulation.Map;

public sealed class MapState
{
    public static readonly MapCellDefinitionId DefaultDefinitionId = new(0);

    private readonly MapCellDefinitionId[] _cellDefinitions;

    public MapState(
        int width,
        int height,
        MapCellDefinitionRegistry definitions,
        MapCellDefinitionId initialDefinition)
    {
        Width = width;
        Height = height;
        CellCount = MapCellId.GetCellCount(width, height);
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        Definitions.Get(initialDefinition);
        _cellDefinitions = Enumerable.Repeat(initialDefinition, CellCount).ToArray();
        Topology = RoomTopologyBuilder.Build(this);
    }

    internal MapState(
        int width,
        int height,
        MapCellDefinitionRegistry definitions,
        IReadOnlyList<MapCellDefinitionId> cellDefinitions)
    {
        Width = width;
        Height = height;
        CellCount = MapCellId.GetCellCount(width, height);
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        ArgumentNullException.ThrowIfNull(cellDefinitions);
        if (cellDefinitions.Count != CellCount)
        {
            throw new InvalidOperationException(
                $"Map restore requires exactly {CellCount} cell definitions, got {cellDefinitions.Count}.");
        }

        _cellDefinitions = new MapCellDefinitionId[CellCount];
        for (int index = 0; index < CellCount; index++)
        {
            MapCellDefinitionId definitionId = cellDefinitions[index];
            Definitions.Get(definitionId);
            _cellDefinitions[index] = definitionId;
        }
        Topology = RoomTopologyBuilder.Build(this);
    }

    public int Width { get; }
    public int Height { get; }
    public int CellCount { get; }
    public MapCellDefinitionRegistry Definitions { get; }
    public RoomTopology Topology { get; private set; }

    public static MapState CreateDefault()
    {
        var registry = new MapCellDefinitionRegistry(new[]
        {
            new MapCellDefinition(DefaultDefinitionId, ParticipatesInRoomTopology: false)
        });
        return new MapState(1, 1, registry, DefaultDefinitionId);
    }

    public MapCellDefinitionId GetCellDefinition(MapCellId cell)
    {
        ValidateCell(cell);
        return _cellDefinitions[cell.Value];
    }

    public IEnumerable<MapCellId> EnumerateCellsCanonical()
    {
        for (int value = 0; value < CellCount; value++)
        {
            yield return new MapCellId(value);
        }
    }

    public void ValidateInvariants()
    {
        if (_cellDefinitions.Length != CellCount)
        {
            throw new InvalidOperationException(
                $"Map cell storage length {_cellDefinitions.Length} differs from cell count {CellCount}.");
        }

        foreach (MapCellDefinitionId definitionId in _cellDefinitions)
        {
            Definitions.Get(definitionId);
        }

        RoomTopology rebuilt = RoomTopologyBuilder.Build(this);
        if (!Topology.CanonicallyEquals(rebuilt))
        {
            throw new InvalidOperationException("Cached room topology differs from canonical reconstruction.");
        }
    }

    public void WriteChecksum(StateChecksumWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.AddInt32(MapStateSnapshot.CurrentSchemaVersion);
        writer.AddInt32(Width);
        writer.AddInt32(Height);
        writer.AddInt32(CellCount);
        foreach (MapCellId cell in EnumerateCellsCanonical())
        {
            writer.AddInt32(cell.Value);
            writer.AddInt32(GetCellDefinition(cell).Value);
        }

        writer.AddInt32(Topology.RoomCount);
        foreach (RoomId roomId in Topology.RoomIds)
        {
            writer.AddInt32(roomId.Value);
            IReadOnlyList<MapCellId> members = Topology.GetMembers(roomId);
            writer.AddInt32(members.Count);
            foreach (MapCellId member in members)
            {
                writer.AddInt32(member.Value);
            }
        }
    }

    internal void ValidateMutationBatch(IReadOnlyList<MapCellMutation> orderedMutations)
    {
        ArgumentNullException.ThrowIfNull(orderedMutations);
        var conflicts = new List<MapCellMutation>();
        for (int index = 0; index < orderedMutations.Count; index++)
        {
            MapCellMutation mutation = orderedMutations[index];
            if (mutation.Kind != MapCellMutationKind.SetCellDefinition)
            {
                throw new MapMutationValidationException(
                    $"Unknown map mutation kind {(byte)mutation.Kind}.",
                    new[] { mutation });
            }

            try
            {
                ValidateCell(mutation.Cell);
                Definitions.Get(mutation.Definition);
            }
            catch (Exception exception) when (exception is ArgumentOutOfRangeException or InvalidOperationException)
            {
                throw new MapMutationValidationException(
                    $"Map mutation {mutation.MutationSequence} is invalid: {exception.Message}",
                    new[] { mutation });
            }

            if (index > 0 && orderedMutations[index - 1].Cell == mutation.Cell)
            {
                if (conflicts.Count == 0 || conflicts[^1] != orderedMutations[index - 1])
                {
                    conflicts.Add(orderedMutations[index - 1]);
                }
                conflicts.Add(mutation);
            }
        }

        if (conflicts.Count > 0)
        {
            string sequences = string.Join(",", conflicts.Select(static mutation => mutation.MutationSequence));
            throw new MapMutationValidationException(
                $"Map mutation batch rejected. Multiple writes target the same cell. Mutation sequences: {sequences}.",
                conflicts);
        }
    }

    internal void ApplyValidatedMutations(IReadOnlyList<MapCellMutation> orderedMutations)
    {
        ArgumentNullException.ThrowIfNull(orderedMutations);
        if (orderedMutations.Count == 0)
        {
            return;
        }
        foreach (MapCellMutation mutation in orderedMutations)
        {
            _cellDefinitions[mutation.Cell.Value] = mutation.Definition;
        }
        Topology = RoomTopologyBuilder.Build(this);
    }

    private void ValidateCell(MapCellId cell)
    {
        if ((uint)cell.Value >= (uint)CellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cell),
                cell,
                $"Cell ID must be between 0 and {CellCount - 1}.");
        }
    }
}
