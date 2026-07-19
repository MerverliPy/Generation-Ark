namespace GenerationArk.Simulation.Map;

public readonly record struct MapCellMutation(
    ulong MutationSequence,
    MapCellMutationKind Kind,
    MapCellId Cell,
    MapCellDefinitionId Definition);
