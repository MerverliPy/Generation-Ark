using GenerationArk.Simulation.Map;

namespace GenerationArk.Simulation.Persistence;

public sealed record MapCellStateSnapshot(
    MapCellId CellId,
    MapCellDefinitionId DefinitionId);
