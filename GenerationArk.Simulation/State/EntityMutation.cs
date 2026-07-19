using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.State;

public sealed record EntityMutation
{
    internal EntityMutation(
        ulong mutationSequence,
        EntityMutationKind kind,
        EntityId entityId,
        ComponentValue? component,
        ComponentTypeId? componentTypeId,
        IEnumerable<ComponentValue>? initialComponents)
    {
        MutationSequence = mutationSequence;
        Kind = kind;
        EntityId = entityId;
        Component = component;
        ComponentTypeId = componentTypeId;
        InitialComponents = (initialComponents ?? Array.Empty<ComponentValue>())
            .Select(value => value
                ?? throw new ArgumentException(
                    "Initial component sets cannot contain null entries.",
                    nameof(initialComponents)))
            .OrderBy(static value => value.ComponentTypeId)
            .ToArray();
    }

    public ulong MutationSequence { get; }
    public EntityMutationKind Kind { get; }
    public EntityId EntityId { get; }
    public ComponentValue? Component { get; }
    public ComponentTypeId? ComponentTypeId { get; }
    public IReadOnlyList<ComponentValue> InitialComponents { get; }
}
