using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Persistence;

public sealed record ComponentStateSnapshot(
    ComponentTypeId ComponentTypeId,
    string Payload);
