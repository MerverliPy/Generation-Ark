using System.Globalization;

namespace GenerationArk.Simulation.Diagnostics;

public readonly record struct ChecksumFormatVersion(uint Value)
{
    public static ChecksumFormatVersion Current => new(3);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
