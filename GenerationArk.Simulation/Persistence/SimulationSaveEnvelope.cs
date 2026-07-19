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
        ReadOnlySpan<byte> payload)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Checksum = checksum;
        _payload = payload.ToArray();
    }

    public SimulationSaveMetadata Metadata { get; }

    public ulong Checksum { get; }

    public ReadOnlyMemory<byte> Payload => _payload;

    public byte[] CopyPayload()
        => (byte[])_payload.Clone();
}
