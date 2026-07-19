using System;
using System.Text.Json;

namespace GenerationArk.Simulation.Random;

public static class RandomStateSnapshotJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string Serialize(RandomStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, Options);
    }

    public static RandomStateSnapshot Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<RandomStateSnapshot>(json, Options)
            ?? throw new InvalidOperationException("Random-state JSON produced no object.");
    }
}
