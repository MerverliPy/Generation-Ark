using System.Globalization;

namespace GenerationArk.Simulation.Random;

public readonly record struct SimulationSeed(ulong Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
