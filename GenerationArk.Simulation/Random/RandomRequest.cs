namespace GenerationArk.Simulation.Random;

/// <summary>
/// Diagnostic record for an authoritative random request. Range results are encoded
/// as unchecked 64-bit values so negative integers remain losslessly recoverable.
/// </summary>
public readonly record struct RandomRequest(
    SimulationSeed Seed,
    RandomAlgorithmVersion AlgorithmVersion,
    RandomScope Scope,
    uint Counter,
    RandomRequestKind Kind,
    long ParameterA,
    long ParameterB,
    ulong Result);
