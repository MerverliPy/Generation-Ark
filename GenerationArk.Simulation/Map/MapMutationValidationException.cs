using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.Map;

public sealed class MapMutationValidationException : InvalidOperationException
{
    public MapMutationValidationException(
        string message,
        IEnumerable<MapCellMutation> conflictingMutations)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(conflictingMutations);
        ConflictingMutations = conflictingMutations
            .OrderBy(static mutation => mutation, MapCellMutationComparer.Instance)
            .ToArray();
    }

    public IReadOnlyList<MapCellMutation> ConflictingMutations { get; }
}
