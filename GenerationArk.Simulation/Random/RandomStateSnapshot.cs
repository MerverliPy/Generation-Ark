namespace GenerationArk.Simulation.Random;

public sealed record RandomStateSnapshot(
    ulong RootSeed,
    uint AlgorithmVersion);
