using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Persistence;

public static class EntityStateSerializer
{
    public static EntityStateSnapshot Capture(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Mutations.Count != 0)
        {
            throw new InvalidOperationException(
                "Entity state can only be saved with an empty mutation buffer.");
        }

        EntityStateEntrySnapshot[] entities = world.Entities.AllEntityIds
            .Select(entityId => new EntityStateEntrySnapshot(
                entityId,
                world.Entities.GetLifecycleState(entityId),
                world.Components.GetComponentTypeIds(entityId)
                    .Select(componentTypeId => ComponentStateSerializer.Capture(
                        world.Components,
                        entityId,
                        componentTypeId))
                    .ToArray()))
            .ToArray();

        return new EntityStateSnapshot(
            EntityStateSnapshot.CurrentSchemaVersion,
            world.Entities.NextEntityId,
            world.Entities.RetiredEntityIds.ToArray(),
            entities);
    }

    public static byte[] ToUtf8(WorldState world)
        => ToUtf8(Capture(world));

    public static string ToJson(WorldState world)
        => Encoding.UTF8.GetString(ToUtf8(world));

    public static byte[] ToUtf8(EntityStateSnapshot snapshot)
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
            writer.WriteString("nextEntityId", FormatEntityId(snapshot.NextEntityId));

            writer.WritePropertyName("retiredEntityIds");
            writer.WriteStartArray();
            foreach (EntityId entityId in snapshot.RetiredEntityIds.OrderBy(static id => id))
            {
                writer.WriteStringValue(FormatEntityId(entityId.Value));
            }
            writer.WriteEndArray();

            writer.WritePropertyName("entities");
            writer.WriteStartArray();
            foreach (EntityStateEntrySnapshot entity in snapshot.Entities.OrderBy(static item => item.EntityId))
            {
                writer.WriteStartObject();
                writer.WriteString("entityId", FormatEntityId(entity.EntityId.Value));
                writer.WriteNumber("lifecycleState", (byte)entity.LifecycleState);
                writer.WritePropertyName("components");
                writer.WriteStartArray();
                foreach (ComponentStateSnapshot component in entity.Components
                             .OrderBy(static item => item.ComponentTypeId))
                {
                    writer.WriteStartObject();
                    writer.WriteString("componentTypeId", component.ComponentTypeId.Value);
                    writer.WriteString("payload", component.Payload);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static EntityStateSnapshot FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return FromUtf8(Encoding.UTF8.GetBytes(json));
    }

    public static EntityStateSnapshot FromUtf8(ReadOnlySpan<byte> utf8)
    {
        using JsonDocument document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        JsonElement root = document.RootElement;
        int schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        if (schemaVersion != EntityStateSnapshot.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported entity-state schema version {schemaVersion}.");
        }

        ulong nextEntityId = ParseEntityId(
            root.GetProperty("nextEntityId").GetString(),
            "nextEntityId");

        var retired = new List<EntityId>();
        foreach (JsonElement item in root.GetProperty("retiredEntityIds").EnumerateArray())
        {
            retired.Add(new EntityId(ParseEntityId(item.GetString(), "retiredEntityId")));
        }

        var entities = new List<EntityStateEntrySnapshot>();
        foreach (JsonElement item in root.GetProperty("entities").EnumerateArray())
        {
            var components = new List<ComponentStateSnapshot>();
            foreach (JsonElement component in item.GetProperty("components").EnumerateArray())
            {
                string typeId = component.GetProperty("componentTypeId").GetString()
                    ?? throw new FormatException("componentTypeId cannot be null.");
                string payload = component.GetProperty("payload").GetString()
                    ?? throw new FormatException("component payload cannot be null.");
                components.Add(new ComponentStateSnapshot(
                    new ComponentTypeId(typeId),
                    payload));
            }

            entities.Add(new EntityStateEntrySnapshot(
                new EntityId(ParseEntityId(
                    item.GetProperty("entityId").GetString(),
                    "entityId")),
                (EntityLifecycleState)item.GetProperty("lifecycleState").GetByte(),
                components.ToArray()));
        }

        return new EntityStateSnapshot(
            schemaVersion,
            nextEntityId,
            retired.ToArray(),
            entities.ToArray());
    }

    public static WorldState Restore(
        ReadOnlySpan<byte> utf8,
        IEnumerable<ComponentRegistration> componentRegistrations,
        IEnumerable<IEntityCleanupHook>? cleanupHooks = null,
        int lifecycleEventRetentionCapacity = 2048)
    {
        ArgumentNullException.ThrowIfNull(componentRegistrations);
        EntityStateSnapshot snapshot = FromUtf8(utf8);
        var world = new WorldState(
            componentRegistrations,
            cleanupHooks,
            lifecycleEventRetentionCapacity);
        var live = snapshot.Entities.Select(static entity =>
            new KeyValuePair<EntityId, EntityLifecycleState>(
                entity.EntityId,
                entity.LifecycleState));
        var components = snapshot.Entities.SelectMany(static entity =>
            entity.Components.Select(component => (
                entity.EntityId,
                component.ComponentTypeId,
                component.Payload)));
        world.RestoreEntityState(
            snapshot.NextEntityId,
            live,
            snapshot.RetiredEntityIds,
            components);
        return world;
    }

    public static WorldState Restore(
        string json,
        IEnumerable<ComponentRegistration> componentRegistrations,
        IEnumerable<IEntityCleanupHook>? cleanupHooks = null,
        int lifecycleEventRetentionCapacity = 2048)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Restore(
            Encoding.UTF8.GetBytes(json),
            componentRegistrations,
            cleanupHooks,
            lifecycleEventRetentionCapacity);
    }

    private static string FormatEntityId(ulong value)
        => $"0x{value:X16}";

    private static ulong ParseEntityId(string? value, string propertyName)
    {
        if (value is null
            || !value.StartsWith("0x", StringComparison.Ordinal)
            || value.Length != 18)
        {
            throw new FormatException(
                $"{propertyName} must be a 16-digit 0x-prefixed hexadecimal value.");
        }

        return ulong.Parse(
            value.AsSpan(2),
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture);
    }
}
