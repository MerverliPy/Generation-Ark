using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Commands;

public readonly record struct CommandResult(
    CommandId CommandId,
    SimTick AppliedAt,
    bool Accepted,
    string? RejectionReason);
