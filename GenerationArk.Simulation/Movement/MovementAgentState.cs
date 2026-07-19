using System;
using System.Globalization;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Movement;

public sealed record MovementAgentState(
    MapCellId CurrentCell,
    MapCellId DestinationCell,
    ulong RouteRevision)
{
    public static readonly ComponentTypeId ComponentTypeId = new("movement-agent");

    public static ComponentRegistration CreateRegistration()
        => ComponentRegistration.Create<MovementAgentState>(
            ComponentTypeId,
            Serialize,
            Deserialize,
            WriteChecksum);

    public static string Serialize(MovementAgentState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{state.CurrentCell.Value}:{state.DestinationCell.Value}:{state.RouteRevision}");
    }

    public static MovementAgentState Deserialize(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        string[] fields = payload.Split(':', StringSplitOptions.None);
        if (fields.Length != 3
            || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int current)
            || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int destination)
            || !ulong.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong revision))
        {
            throw new InvalidOperationException("Movement agent payload must be '<current-cell>:<destination-cell>:<route-revision>'.");
        }

        return new MovementAgentState(new MapCellId(current), new MapCellId(destination), revision);
    }

    public static void WriteChecksum(StateChecksumWriter writer, MovementAgentState state)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(state);
        writer.AddInt32(state.CurrentCell.Value);
        writer.AddInt32(state.DestinationCell.Value);
        writer.AddUInt64(state.RouteRevision);
    }
}
