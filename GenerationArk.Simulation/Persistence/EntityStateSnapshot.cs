using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Persistence;

public sealed record EntityStateSnapshot(
    int SchemaVersion,
    ulong NextEntityId,
    EntityId[] RetiredEntityIds,
    EntityStateEntrySnapshot[] Entities)
{
    public const int CurrentSchemaVersion = 1;
}
