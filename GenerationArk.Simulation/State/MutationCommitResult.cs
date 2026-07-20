using System.Collections.Generic;

namespace GenerationArk.Simulation.State;

public sealed record MutationCommitResult(
    int AppliedMutationCount,
    int CancelledScheduledEventCount,
    IReadOnlyList<EntityId> CreatedEntityIds,
    int AppliedPositionMutationCount = 0);
