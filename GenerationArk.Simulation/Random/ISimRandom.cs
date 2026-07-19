namespace GenerationArk.Simulation.Random;

public interface ISimRandom
{
    SimulationSeed Seed { get; }
    RandomAlgorithmVersion AlgorithmVersion { get; }

    uint UInt32(RandomScope scope, uint counter);
    ulong UInt64(RandomScope scope, uint counter);

    int Range(
        RandomScope scope,
        uint counter,
        int minimumInclusive,
        int maximumExclusive);

    bool Chance(
        RandomScope scope,
        uint counter,
        uint numerator,
        uint denominator);

    RandomStateSnapshot CaptureSnapshot();
}
