using System;

namespace GenerationArk.Simulation.State;

public sealed record ComponentValue
{
    public ComponentValue(ComponentTypeId componentTypeId, object value)
    {
        ComponentTypeId = componentTypeId;
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public ComponentTypeId ComponentTypeId { get; }
    public object Value { get; }
}
