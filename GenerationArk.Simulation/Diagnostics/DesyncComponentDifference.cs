namespace GenerationArk.Simulation.Diagnostics;

public readonly record struct DesyncComponentDifference(
    ChecksumComponentId ComponentId,
    ulong? ExpectedChecksum,
    ulong? ActualChecksum);
