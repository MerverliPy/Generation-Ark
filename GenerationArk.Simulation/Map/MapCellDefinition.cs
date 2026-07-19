namespace GenerationArk.Simulation.Map;

public sealed record MapCellDefinition(
    MapCellDefinitionId Id,
    bool ParticipatesInRoomTopology);
