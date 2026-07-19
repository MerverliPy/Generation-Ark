using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Canonical UTF-8 JSON replay serializer with fixed property and array ordering.
/// </summary>
public static class ReplayLogJson
{
    public static byte[] ToUtf8(ReplayLog log)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", log.FormatVersion);
            writer.WriteString("rootSeed", FormatHex(log.RootSeed));
            writer.WriteString("buildVersion", log.BuildVersion);
            writer.WriteNumber("finalTick", log.FinalTick);

            writer.WritePropertyName("commands");
            writer.WriteStartArray();
            foreach (ReplayCommand command in log.Commands)
            {
                writer.WriteStartObject();
                writer.WriteNumber("acceptedTick", command.AcceptedTick);
                writer.WriteNumber("targetTick", command.TargetTick);
                writer.WriteNumber("sequence", command.Sequence);
                writer.WriteString("commandId", command.CommandId);
                writer.WriteString("commandType", command.CommandType);
                writer.WriteString("payloadBase64", command.PayloadBase64);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WritePropertyName("checkpoints");
            writer.WriteStartArray();
            foreach (ReplayCheckpoint checkpoint in log.Checkpoints)
            {
                writer.WriteStartObject();
                writer.WriteNumber("tick", checkpoint.Tick);
                writer.WriteString("checksum", FormatHex(checkpoint.Checksum));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static string ToJson(ReplayLog log)
        => Encoding.UTF8.GetString(ToUtf8(log));

    public static ReplayLog FromJson(string json)
    {
        if (json is null)
        {
            throw new ArgumentNullException(nameof(json));
        }
        return FromUtf8(Encoding.UTF8.GetBytes(json));
    }

    public static ReplayLog FromUtf8(ReadOnlySpan<byte> utf8)
    {
        using JsonDocument document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });

        JsonElement root = document.RootElement;
        int formatVersion = root.GetProperty("formatVersion").GetInt32();
        ulong rootSeed = ParseHex(root.GetProperty("rootSeed").GetString(), "rootSeed");
        string buildVersion = RequireString(root, "buildVersion");
        long finalTick = root.GetProperty("finalTick").GetInt64();

        var commands = new List<ReplayCommand>();
        foreach (JsonElement item in root.GetProperty("commands").EnumerateArray())
        {
            commands.Add(new ReplayCommand(
                item.GetProperty("acceptedTick").GetInt64(),
                item.GetProperty("targetTick").GetInt64(),
                item.GetProperty("sequence").GetInt64(),
                RequireString(item, "commandId"),
                RequireString(item, "commandType"),
                item.GetProperty("payloadBase64").GetString()
                    ?? throw new FormatException("payloadBase64 cannot be null.")));
        }

        var checkpoints = new List<ReplayCheckpoint>();
        foreach (JsonElement item in root.GetProperty("checkpoints").EnumerateArray())
        {
            checkpoints.Add(new ReplayCheckpoint(
                item.GetProperty("tick").GetInt64(),
                ParseHex(item.GetProperty("checksum").GetString(), "checksum")));
        }

        return new ReplayLog(
            formatVersion,
            rootSeed,
            buildVersion,
            finalTick,
            commands,
            checkpoints);
    }

    internal static string FormatHex(ulong value)
        => $"0x{value:X16}";

    internal static ulong ParseHex(string? value, string propertyName)
    {
        if (value is null || !value.StartsWith("0x", StringComparison.Ordinal) || value.Length != 18)
        {
            throw new FormatException($"{propertyName} must be a 16-digit 0x-prefixed hexadecimal value.");
        }

        return ulong.Parse(
            value.AsSpan(2),
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture);
    }

    internal static string RequireString(JsonElement element, string propertyName)
        => element.GetProperty(propertyName).GetString()
            ?? throw new FormatException($"{propertyName} cannot be null.");
}
