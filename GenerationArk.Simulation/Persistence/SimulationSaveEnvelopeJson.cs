using System;
using System.IO;
using System.Text;
using System.Text.Json;
using GenerationArk.Simulation.Replay;

namespace GenerationArk.Simulation.Persistence;

/// <summary>
/// Canonical save-envelope JSON. The opaque payload is Base64 and must itself be canonical.
/// </summary>
public static class SimulationSaveEnvelopeJson
{
    public static byte[] ToUtf8(SimulationSaveEnvelope envelope)
    {
        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        SimulationSaveMetadata metadata = envelope.Metadata;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        }))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("metadata");
            writer.WriteStartObject();
            writer.WriteNumber("simulationSchemaVersion", metadata.SimulationSchemaVersion);
            writer.WriteNumber("replayFormatVersion", metadata.ReplayFormatVersion);
            writer.WriteNumber("checksumFormatVersion", metadata.ChecksumFormatVersion);
            writer.WriteNumber("randomAlgorithmVersion", metadata.RandomAlgorithmVersion);
            writer.WriteString("rootSeed", ReplayLogJson.FormatHex(metadata.RootSeed));
            writer.WriteNumber("currentTick", metadata.CurrentTick);
            writer.WriteNumber("requestedSpeedMultiplier", metadata.RequestedSpeedMultiplier);
            writer.WriteBoolean("isPaused", metadata.IsPaused);
            writer.WriteNumber("nextEntityId", metadata.NextEntityId);
            writer.WriteNumber("nextCommandSequence", metadata.NextCommandSequence);
            writer.WriteNumber("nextSchedulerEventId", metadata.NextSchedulerEventId);
            writer.WriteNumber("nextSchedulerCreationSequence", metadata.NextSchedulerCreationSequence);
            writer.WriteString("calendarDefinitionId", metadata.CalendarDefinitionId);
            writer.WriteString("buildVersion", metadata.BuildVersion);
            writer.WriteEndObject();
            writer.WriteString("checksum", ReplayLogJson.FormatHex(envelope.Checksum));
            writer.WriteBase64String("payloadBase64", envelope.Payload.Span);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static string ToJson(SimulationSaveEnvelope envelope)
        => Encoding.UTF8.GetString(ToUtf8(envelope));

    public static SimulationSaveEnvelope FromJson(string json)
    {
        if (json is null)
        {
            throw new ArgumentNullException(nameof(json));
        }
        return FromUtf8(Encoding.UTF8.GetBytes(json));
    }

    public static SimulationSaveEnvelope FromUtf8(ReadOnlySpan<byte> utf8)
    {
        using JsonDocument document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });

        JsonElement root = document.RootElement;
        JsonElement metadataElement = root.GetProperty("metadata");
        var metadata = new SimulationSaveMetadata(
            metadataElement.GetProperty("simulationSchemaVersion").GetInt32(),
            metadataElement.GetProperty("replayFormatVersion").GetInt32(),
            metadataElement.GetProperty("checksumFormatVersion").GetInt32(),
            metadataElement.GetProperty("randomAlgorithmVersion").GetInt32(),
            ReplayLogJson.ParseHex(metadataElement.GetProperty("rootSeed").GetString(), "rootSeed"),
            metadataElement.GetProperty("currentTick").GetInt64(),
            metadataElement.GetProperty("requestedSpeedMultiplier").GetInt32(),
            metadataElement.GetProperty("isPaused").GetBoolean(),
            metadataElement.GetProperty("nextEntityId").GetInt64(),
            metadataElement.GetProperty("nextCommandSequence").GetInt64(),
            metadataElement.GetProperty("nextSchedulerEventId").GetInt64(),
            metadataElement.GetProperty("nextSchedulerCreationSequence").GetInt64(),
            ReplayLogJson.RequireString(metadataElement, "calendarDefinitionId"),
            ReplayLogJson.RequireString(metadataElement, "buildVersion"));

        ulong checksum = ReplayLogJson.ParseHex(root.GetProperty("checksum").GetString(), "checksum");
        string payloadBase64 = root.GetProperty("payloadBase64").GetString()
            ?? throw new FormatException("payloadBase64 cannot be null.");
        byte[] payload = Convert.FromBase64String(payloadBase64);
        return new SimulationSaveEnvelope(metadata, checksum, payload);
    }
}
