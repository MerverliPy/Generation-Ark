using System;

namespace GenerationArk.Simulation.Persistence;

/// <summary>
/// Versioned metadata, checksum, and opaque canonical authoritative-state payload.
/// </summary>
public sealed class SimulationSaveEnvelope
{
    private readonly byte[] _payload;

    public SimulationSaveEnvelope(
        SimulationSaveMetadata metadata,
        ulong checksum,
        ReadOnlySpan<byte> payload,
        SpatialStateSnapshot? spatialState = null)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Checksum = checksum;
        _payload = payload.ToArray();
        SpatialState = spatialState;
    }

    public SimulationSaveMetadata Metadata { get; }

    public ulong Checksum { get; }

    /// <summary>
    /// Step 11 spatial payload. A null value is valid only for simulations with no spatial state.
    /// Missing JSON data is rejected as an unsupported pre-Step 11 save schema.
    /// </summary>
    public SpatialStateSnapshot? SpatialState { get; }

    public ReadOnlyMemory<byte> Payload => _payload;

    public byte[] CopyPayload()
        => (byte[])_payload.Clone();
}
