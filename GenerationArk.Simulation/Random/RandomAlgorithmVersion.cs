using System.Globalization;

namespace GenerationArk.Simulation.Random;

public readonly record struct RandomAlgorithmVersion(uint Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
