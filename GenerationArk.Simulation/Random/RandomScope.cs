namespace GenerationArk.Simulation.Random;

public readonly record struct RandomScope(
    RandomDomainId Domain,
    ulong Owner,
    ulong Purpose,
    ulong Occurrence);
