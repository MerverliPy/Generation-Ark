using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GenerationArk.Simulation.Map;

namespace GenerationArk.Simulation.Persistence;

public static class MapStateSerializer
{
    public static MapStateSnapshot Capture(MapState map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return new MapStateSnapshot(
            MapStateSnapshot.CurrentSchemaVersion,
            map.Width,
            map.Height,
            map.EnumerateCellsCanonical()
                .Select(cell => new MapCellStateSnapshot(
                    cell,
                    map.GetCellDefinition(cell))));
    }

    public static byte[] ToUtf8(MapState map)
        => ToUtf8(Capture(map));

    public static string ToJson(MapState map)
        => Encoding.UTF8.GetString(ToUtf8(map));

    public static byte[] ToUtf8(MapStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", snapshot.SchemaVersion);
            writer.WriteNumber("width", snapshot.Width);
            writer.WriteNumber("height", snapshot.Height);
            writer.WritePropertyName("cells");
            writer.WriteStartArray();
            foreach (MapCellStateSnapshot cell in snapshot.Cells
                         .OrderBy(static item => item.CellId))
            {
                writer.WriteStartObject();
                writer.WriteNumber("cellId", cell.CellId.Value);
                writer.WriteNumber("definitionId", cell.DefinitionId.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public static string ToJson(MapStateSnapshot snapshot)
        => Encoding.UTF8.GetString(ToUtf8(snapshot));

    public static MapStateSnapshot FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return FromUtf8(Encoding.UTF8.GetBytes(json));
    }

    public static MapStateSnapshot FromUtf8(ReadOnlySpan<byte> utf8)
    {
        using JsonDocument document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });

        JsonElement root = document.RootElement;
        var cells = new List<MapCellStateSnapshot>();
        foreach (JsonElement cell in root.GetProperty("cells").EnumerateArray())
        {
            cells.Add(new MapCellStateSnapshot(
                new MapCellId(cell.GetProperty("cellId").GetInt32()),
                new MapCellDefinitionId(cell.GetProperty("definitionId").GetInt32())));
        }

        return new MapStateSnapshot(
            root.GetProperty("schemaVersion").GetInt32(),
            root.GetProperty("width").GetInt32(),
            root.GetProperty("height").GetInt32(),
            cells);
    }

    public static MapState Restore(
        MapStateSnapshot snapshot,
        MapCellDefinitionRegistry definitions)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(definitions);
        if (snapshot.SchemaVersion != MapStateSnapshot.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported map-state schema version {snapshot.SchemaVersion}; expected {MapStateSnapshot.CurrentSchemaVersion}.");
        }

        int cellCount = MapCellId.GetCellCount(snapshot.Width, snapshot.Height);
        if (snapshot.Cells.Count != cellCount)
        {
            throw new InvalidOperationException(
                $"Map snapshot requires exactly {cellCount} cell entries, got {snapshot.Cells.Count}.");
        }

        var orderedDefinitions = new MapCellDefinitionId[cellCount];
        var seen = new bool[cellCount];
        foreach (MapCellStateSnapshot cell in snapshot.Cells)
        {
            if ((uint)cell.CellId.Value >= (uint)cellCount)
            {
                throw new InvalidOperationException(
                    $"Map snapshot contains out-of-range cell ID {cell.CellId}.");
            }
            if (seen[cell.CellId.Value])
            {
                throw new InvalidOperationException(
                    $"Map snapshot contains duplicate cell ID {cell.CellId}.");
            }
            definitions.Get(cell.DefinitionId);
            seen[cell.CellId.Value] = true;
            orderedDefinitions[cell.CellId.Value] = cell.DefinitionId;
        }

        for (int value = 0; value < seen.Length; value++)
        {
            if (!seen[value])
            {
                throw new InvalidOperationException(
                    $"Map snapshot is missing cell ID {value}.");
            }
        }

        return new MapState(
            snapshot.Width,
            snapshot.Height,
            definitions,
            orderedDefinitions);
    }

    public static MapState Restore(
        ReadOnlySpan<byte> utf8,
        MapCellDefinitionRegistry definitions)
        => Restore(FromUtf8(utf8), definitions);

    public static MapState Restore(
        string json,
        MapCellDefinitionRegistry definitions)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Restore(FromJson(json), definitions);
    }
}
