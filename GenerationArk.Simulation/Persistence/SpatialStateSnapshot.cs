using GenerationArk.Simulation.Map;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Persistence;

public sealed record SpatialStateSnapshot(
    int SchemaVersion,
    SpatialStateEntrySnapshot[] Positions)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record SpatialStateEntrySnapshot(
    EntityId EntityId,
    MapCellId CellId);
