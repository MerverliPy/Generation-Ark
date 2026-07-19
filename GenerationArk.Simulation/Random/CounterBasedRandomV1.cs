using System;
using GenerationArk.Simulation.Diagnostics;

namespace GenerationArk.Simulation.Random;

/// <summary>
/// Project-owned keyed random algorithm. Each output is a pure function of the root seed,
/// algorithm version, domain, owner, purpose, occurrence, local counter, and internal lane.
/// </summary>
public sealed class CounterBasedRandomV1 : ISimRandom
{
    private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15UL;
    private const ulong SplitMixMultiplierOne = 0xBF58476D1CE4E5B9UL;
    private const ulong SplitMixMultiplierTwo = 0x94D049BB133111EBUL;

    public static readonly RandomAlgorithmVersion Version = new(1);

    private readonly IRandomRequestTracer? _tracer;

    public CounterBasedRandomV1(
        SimulationSeed seed,
        IRandomRequestTracer? tracer = null)
    {
        Seed = seed;
        _tracer = tracer;
    }

    public SimulationSeed Seed { get; }
    public RandomAlgorithmVersion AlgorithmVersion => Version;

    public uint UInt32(RandomScope scope, uint counter)
    {
        ValidateScope(scope);
        ulong raw = GenerateRaw(scope, counter, lane: 0);
        uint result = (uint)(raw >> 32);
        Record(scope, counter, RandomRequestKind.UInt32, 0, 0, result);
        return result;
    }

    public ulong UInt64(RandomScope scope, uint counter)
    {
        ValidateScope(scope);
        ulong result = GenerateRaw(scope, counter, lane: 0);
        Record(scope, counter, RandomRequestKind.UInt64, 0, 0, result);
        return result;
    }

    public int Range(
        RandomScope scope,
        uint counter,
        int minimumInclusive,
        int maximumExclusive)
    {
        if (minimumInclusive >= maximumExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumExclusive),
                maximumExclusive,
                "Maximum must be greater than minimum.");
        }

        ValidateScope(scope);
        ulong width = checked((ulong)((long)maximumExclusive - minimumInclusive));
        ulong offset = UniformBelow(scope, counter, width);
        int result = checked((int)((long)minimumInclusive + (long)offset));
        Record(
            scope,
            counter,
            RandomRequestKind.Range,
            minimumInclusive,
            maximumExclusive,
            unchecked((ulong)(long)result));
        return result;
    }

    public bool Chance(
        RandomScope scope,
        uint counter,
        uint numerator,
        uint denominator)
    {
        if (denominator == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(denominator),
                denominator,
                "Probability denominator must be positive.");
        }

        if (numerator > denominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numerator),
                numerator,
                "Probability numerator cannot exceed denominator.");
        }

        ValidateScope(scope);
        bool result = numerator == denominator
            || (numerator != 0 && UniformBelow(scope, counter, denominator) < numerator);
        Record(
            scope,
            counter,
            RandomRequestKind.Chance,
            numerator,
            denominator,
            result ? 1UL : 0UL);
        return result;
    }

    public RandomStateSnapshot CaptureSnapshot()
        => new(Seed.Value, AlgorithmVersion.Value);

    public static CounterBasedRandomV1 Restore(
        RandomStateSnapshot snapshot,
        IRandomRequestTracer? tracer = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.AlgorithmVersion != Version.Value)
        {
            throw new NotSupportedException(
                $"Random algorithm version {snapshot.AlgorithmVersion} is not supported by {nameof(CounterBasedRandomV1)}.");
        }

        return new CounterBasedRandomV1(new SimulationSeed(snapshot.RootSeed), tracer);
    }

    private ulong UniformBelow(RandomScope scope, uint counter, ulong bound)
    {
        if (bound == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bound), bound, "Bound must be positive.");
        }

        // Rejection sampling removes modulo bias. Retry lanes are private to this public
        // request, so retries cannot consume or perturb another operation's local counter.
        ulong rejectionThreshold = unchecked(0UL - bound) % bound;
        ulong lane = 0;
        while (true)
        {
            ulong sample = GenerateRaw(scope, counter, lane);
            if (sample >= rejectionThreshold)
            {
                return sample % bound;
            }

            lane = checked(lane + 1);
        }
    }

    private ulong GenerateRaw(RandomScope scope, uint counter, ulong lane)
    {
        var hash = new StableHash64();
        hash.AddUInt64(Seed.Value);
        hash.AddUInt32(AlgorithmVersion.Value);
        hash.AddString(scope.Domain.Value);
        hash.AddUInt64(scope.Owner);
        hash.AddUInt64(scope.Purpose);
        hash.AddUInt64(scope.Occurrence);
        hash.AddUInt32(counter);
        hash.AddUInt64(lane);
        return SplitMix64(hash.Value);
    }

    private static ulong SplitMix64(ulong value)
    {
        ulong mixed = unchecked(value + SplitMixIncrement);
        mixed = unchecked((mixed ^ (mixed >> 30)) * SplitMixMultiplierOne);
        mixed = unchecked((mixed ^ (mixed >> 27)) * SplitMixMultiplierTwo);
        return mixed ^ (mixed >> 31);
    }

    private static void ValidateScope(RandomScope scope)
    {
        if (string.IsNullOrWhiteSpace(scope.Domain.Value))
        {
            throw new ArgumentException(
                "Random scope must contain a stable non-empty domain ID.",
                nameof(scope));
        }
    }

    private void Record(
        RandomScope scope,
        uint counter,
        RandomRequestKind kind,
        long parameterA,
        long parameterB,
        ulong result)
    {
        _tracer?.Record(
            new RandomRequest(
                Seed,
                AlgorithmVersion,
                scope,
                counter,
                kind,
                parameterA,
                parameterB,
                result));
    }
}
