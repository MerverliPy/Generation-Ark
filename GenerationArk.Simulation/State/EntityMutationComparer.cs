using System.Collections.Generic;

namespace GenerationArk.Simulation.State;

internal sealed class EntityMutationComparer : IComparer<EntityMutation>
{
    public static EntityMutationComparer Instance { get; } = new();

    public int Compare(EntityMutation? left, EntityMutation? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }
        if (left is null)
        {
            return -1;
        }
        if (right is null)
        {
            return 1;
        }

        int sequence = left.MutationSequence.CompareTo(right.MutationSequence);
        if (sequence != 0)
        {
            return sequence;
        }

        int kind = left.Kind.CompareTo(right.Kind);
        if (kind != 0)
        {
            return kind;
        }

        int entity = left.EntityId.CompareTo(right.EntityId);
        if (entity != 0)
        {
            return entity;
        }

        ComponentTypeId leftType = left.ComponentTypeId
            ?? left.Component?.ComponentTypeId
            ?? default;
        ComponentTypeId rightType = right.ComponentTypeId
            ?? right.Component?.ComponentTypeId
            ?? default;
        return leftType.CompareTo(rightType);
    }
}
