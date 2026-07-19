using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.State;

public sealed class MutationValidationException : InvalidOperationException
{
    public MutationValidationException(
        string message,
        IEnumerable<EntityMutation> conflictingMutations)
        : base(message)
    {
        ConflictingMutations = conflictingMutations
            .OrderBy(static mutation => mutation.MutationSequence)
            .ToArray();
    }

    public IReadOnlyList<EntityMutation> ConflictingMutations { get; }
}
