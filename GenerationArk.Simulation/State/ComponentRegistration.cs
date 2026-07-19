using System;
using GenerationArk.Simulation.Diagnostics;

namespace GenerationArk.Simulation.State;

public sealed class ComponentRegistration
{
    private readonly Func<object, string> _serialize;
    private readonly Func<string, object> _deserialize;
    private readonly Action<StateChecksumWriter, object> _writeChecksum;

    private ComponentRegistration(
        ComponentTypeId componentTypeId,
        Type runtimeType,
        Func<object, string> serialize,
        Func<string, object> deserialize,
        Action<StateChecksumWriter, object> writeChecksum)
    {
        ComponentTypeId = componentTypeId;
        RuntimeType = runtimeType ?? throw new ArgumentNullException(nameof(runtimeType));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
        _writeChecksum = writeChecksum ?? throw new ArgumentNullException(nameof(writeChecksum));
    }

    public ComponentTypeId ComponentTypeId { get; }
    public Type RuntimeType { get; }

    public static ComponentRegistration Create<T>(
        ComponentTypeId componentTypeId,
        Func<T, string> serialize,
        Func<string, T> deserialize,
        Action<StateChecksumWriter, T> writeChecksum)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(serialize);
        ArgumentNullException.ThrowIfNull(deserialize);
        ArgumentNullException.ThrowIfNull(writeChecksum);

        return new ComponentRegistration(
            componentTypeId,
            typeof(T),
            value => serialize(RequireType<T>(value)),
            payload =>
            {
                T deserialized = deserialize(payload);
                if (deserialized is null)
                {
                    throw new InvalidOperationException(
                        $"Component deserializer for {componentTypeId} returned null.");
                }
                return deserialized;
            },
            (writer, value) => writeChecksum(writer, RequireType<T>(value)));
    }

    public string Serialize(object value)
    {
        ValidateRuntimeType(value);
        return _serialize(value)
            ?? throw new InvalidOperationException(
                $"Component serializer for {ComponentTypeId} returned null.");
    }

    public object Deserialize(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        object value = _deserialize(payload)
            ?? throw new InvalidOperationException(
                $"Component deserializer for {ComponentTypeId} returned null.");
        ValidateRuntimeType(value);
        return value;
    }

    public void WriteChecksum(StateChecksumWriter writer, object value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ValidateRuntimeType(value);
        _writeChecksum(writer, value);
    }

    public void ValidateRuntimeType(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.GetType() != RuntimeType)
        {
            throw new InvalidOperationException(
                $"Component {ComponentTypeId} requires runtime type {RuntimeType.FullName}, got {value.GetType().FullName}.");
        }
    }

    private static T RequireType<T>(object value)
        where T : notnull
        => value is T typed
            ? typed
            : throw new InvalidOperationException(
                $"Expected component runtime type {typeof(T).FullName}, got {value.GetType().FullName}.");
}
