using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Map;

/// <summary>Sets an entity position when Position has a value, or clears it otherwise.</summary>
public readonly record struct PositionMutation(
    ulong MutationSequence,
    EntityId EntityId,
    EntityPosition? Position);

public sealed class PositionMutationValidationException : InvalidOperationException
{
    public PositionMutationValidationException(
        string message,
        IEnumerable<PositionMutation> conflictingMutations)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(conflictingMutations);
        ConflictingMutations = conflictingMutations
            .OrderBy(static mutation => mutation, PositionMutationComparer.Instance)
            .ToArray();
    }

    public IReadOnlyList<PositionMutation> ConflictingMutations { get; }
}
