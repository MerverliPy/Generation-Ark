using System;

namespace GenerationArk.Simulation.Random;

/// <summary>
/// Operation-local convenience cursor. Never retain or share it as global simulation state.
/// </summary>
public ref struct RandomCursor
{
    private readonly ISimRandom _random;
    private readonly RandomScope _scope;
    private uint _counter;

    public RandomCursor(
        ISimRandom random,
        RandomScope scope,
        uint initialCounter = 0)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _scope = scope;
        _counter = initialCounter;
    }

    public readonly uint Counter => _counter;

    public uint UInt32() => _random.UInt32(_scope, NextCounter());

    public ulong UInt64() => _random.UInt64(_scope, NextCounter());

    public int Range(int minimumInclusive, int maximumExclusive)
        => _random.Range(_scope, NextCounter(), minimumInclusive, maximumExclusive);

    public bool Chance(uint numerator, uint denominator)
        => _random.Chance(_scope, NextCounter(), numerator, denominator);

    private uint NextCounter()
    {
        if (_counter == uint.MaxValue)
        {
            throw new InvalidOperationException("Random cursor exhausted its local counter space.");
        }

        uint current = _counter;
        _counter++;
        return current;
    }
}
