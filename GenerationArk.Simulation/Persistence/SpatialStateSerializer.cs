using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Persistence;

public static class SpatialStateSerializer
{
    public static SpatialStateSnapshot Capture(SpatialEntityIndex spatial)
    {
        ArgumentNullException.ThrowIfNull(spatial);
        return new SpatialStateSnapshot(
            SpatialStateSnapshot.CurrentSchemaVersion,
            spatial.EnumerateCanonical()
                .Select(static entry => new SpatialStateEntrySnapshot(
                    entry.Key,
                    entry.Value.Cell))
                .ToArray());
    }

    public static byte[] ToUtf8(SpatialEntityIndex spatial) => ToUtf8(Capture(spatial));

    public static string ToJson(SpatialEntityIndex spatial) => Encoding.UTF8.GetString(ToUtf8(spatial));

    public static byte[] ToUtf8(SpatialStateSnapshot snapshot)
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
            writer.WritePropertyName("positions");
            writer.WriteStartArray();
            foreach (SpatialStateEntrySnapshot entry in snapshot.Positions
                         .OrderBy(static entry => entry.EntityId))
            {
                writer.WriteStartObject();
                writer.WriteString("entityId", FormatEntityId(entry.EntityId));
                writer.WriteNumber("cellId", entry.CellId.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public static SpatialStateSnapshot FromUtf8(ReadOnlySpan<byte> utf8)
    {
        using JsonDocument document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        JsonElement root = document.RootElement;
        var positions = new List<SpatialStateEntrySnapshot>();
        foreach (JsonElement item in root.GetProperty("positions").EnumerateArray())
        {
            positions.Add(new SpatialStateEntrySnapshot(
                new EntityId(ParseEntityId(item.GetProperty("entityId").GetString())),
                new MapCellId(item.GetProperty("cellId").GetInt32())));
        }
        return new SpatialStateSnapshot(
            root.GetProperty("schemaVersion").GetInt32(),
            positions.ToArray());
    }

    public static SpatialStateSnapshot FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return FromUtf8(Encoding.UTF8.GetBytes(json));
    }

    public static void Restore(SpatialStateSnapshot snapshot, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(world);
        if (snapshot.SchemaVersion != SpatialStateSnapshot.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported spatial-state schema version {snapshot.SchemaVersion}; expected {SpatialStateSnapshot.CurrentSchemaVersion}.");
        }
        world.RestoreSpatialState(snapshot.Positions.Select(static entry =>
            new KeyValuePair<EntityId, EntityPosition>(
                entry.EntityId,
                new EntityPosition(entry.CellId))));
    }

    public static void Restore(ReadOnlySpan<byte> utf8, WorldState world)
        => Restore(FromUtf8(utf8), world);

    private static string FormatEntityId(EntityId entityId)
        => $"0x{entityId.Value:X16}";

    private static ulong ParseEntityId(string? value)
    {
        if (value is null
            || !value.StartsWith("0x", StringComparison.Ordinal)
            || value.Length != 18)
        {
            throw new FormatException(
                "entityId must be a 16-digit 0x-prefixed hexadecimal value.");
        }
        return ulong.Parse(value.AsSpan(2), System.Globalization.NumberStyles.AllowHexSpecifier,
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
