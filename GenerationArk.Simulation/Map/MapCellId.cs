using System;

namespace GenerationArk.Simulation.Map;

public readonly record struct MapCellId(int Value) : IComparable<MapCellId>
{
    public static MapCellId FromPosition(GridPosition position, int width, int height)
    {
        int cellCount = GetCellCount(width, height);
        if ((uint)position.X >= (uint)width || (uint)position.Y >= (uint)height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                $"Grid position ({position.X},{position.Y}) is outside {width}x{height}.");
        }

        int value = checked((position.Y * width) + position.X);
        if ((uint)value >= (uint)cellCount)
        {
            throw new InvalidOperationException("Canonical row-major cell ID calculation failed.");
        }
        return new MapCellId(value);
    }

    public GridPosition ToPosition(int width, int height)
    {
        int cellCount = GetCellCount(width, height);
        if ((uint)Value >= (uint)cellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Value),
                Value,
                $"Cell ID must be between 0 and {cellCount - 1} for a {width}x{height} grid.");
        }

        return new GridPosition(Value % width, Value / width);
    }

    public int CompareTo(MapCellId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static int GetCellCount(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Grid width must be positive.");
        }
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Grid height must be positive.");
        }
        return checked(width * height);
    }
}
