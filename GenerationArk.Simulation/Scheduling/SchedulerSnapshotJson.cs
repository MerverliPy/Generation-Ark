using System;
using System.Text.Json;

namespace GenerationArk.Simulation.Scheduling;

public static class SchedulerSnapshotJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string Serialize(SchedulerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, Options);
    }

    public static SchedulerSnapshot Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<SchedulerSnapshot>(json, Options)
            ?? throw new InvalidOperationException("Scheduler snapshot JSON produced no object.");
    }
}
