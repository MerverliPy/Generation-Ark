namespace GenerationArk.Simulation.Diagnostics;

public readonly record struct ComponentChecksum(
    ChecksumComponentId ComponentId,
    ulong Checksum);
