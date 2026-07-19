using System;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;
using GenerationArk.Simulation.Random;

namespace GenerationArk.Simulation.Tests;

internal static class RandomMilestoneTests
{
    private static readonly SimulationSeed Seed = new(0x0123456789ABCDEFUL);

    public static void IdenticalScopesAndCountersProduceIdenticalOutputs()
    {
        var first = new CounterBasedRandomV1(Seed);
        var second = new CounterBasedRandomV1(Seed);
        RandomScope scope = Scope(RandomDomains.ColonistGeneration, owner: 42, purpose: 7, occurrence: 3);

        TestAssert.Equal(0xC2140C0AEA994494UL, first.UInt64(scope, 0));
        TestAssert.Equal(0xC2140C0AU, first.UInt32(scope, 0));
        TestAssert.Equal(4, first.Range(scope, 0, -1000, 1001));
        TestAssert.True(!first.Chance(scope, 0, 17, 101));

        for (uint counter = 0; counter < 64; counter++)
        {
            TestAssert.Equal(first.UInt32(scope, counter), second.UInt32(scope, counter));
            TestAssert.Equal(first.UInt64(scope, counter), second.UInt64(scope, counter));
            TestAssert.Equal(first.Range(scope, counter, -1000, 1001), second.Range(scope, counter, -1000, 1001));
            TestAssert.Equal(first.Chance(scope, counter, 17, 101), second.Chance(scope, counter, 17, 101));
        }
    }

    public static void DifferentDomainsAndOwnersProduceIndependentOutputSequences()
    {
        var random = new CounterBasedRandomV1(Seed);
        RandomScope colonist = Scope(RandomDomains.ColonistGeneration, owner: 10, purpose: 20, occurrence: 30);
        RandomScope incident = Scope(RandomDomains.IncidentSelection, owner: 10, purpose: 20, occurrence: 30);
        RandomScope otherOwner = Scope(RandomDomains.ColonistGeneration, owner: 11, purpose: 20, occurrence: 30);

        int domainDifferences = 0;
        int ownerDifferences = 0;
        for (uint counter = 0; counter < 64; counter++)
        {
            ulong baseline = random.UInt64(colonist, counter);
            if (baseline != random.UInt64(incident, counter))
            {
                domainDifferences++;
            }

            if (baseline != random.UInt64(otherOwner, counter))
            {
                ownerDifferences++;
            }
        }

        TestAssert.True(domainDifferences > 0, "Different domains unexpectedly produced identical sequences.");
        TestAssert.True(ownerDifferences > 0, "Different owners unexpectedly produced identical sequences.");
    }

    public static void UnrelatedDrawsDoNotAlterOtherDomains()
    {
        var baseline = new CounterBasedRandomV1(Seed);
        var perturbed = new CounterBasedRandomV1(Seed);
        RandomDomainId[] protectedDomains =
        {
            RandomDomains.IncidentSelection,
            RandomDomains.ColonistGeneration,
            RandomDomains.BirthOutcomes,
            RandomDomains.EquipmentFailures,
            RandomDomains.InstitutionalSuccession
        };
        RandomScope unrelated = Scope(new RandomDomainId("food-production"), owner: 900, purpose: 12, occurrence: 44);

        foreach (RandomDomainId domain in protectedDomains)
        {
            RandomScope protectedScope = Scope(domain, owner: 77, purpose: 5, occurrence: 9);
            for (uint counter = 0; counter < 32; counter++)
            {
                for (uint unrelatedCounter = 0; unrelatedCounter < 19; unrelatedCounter++)
                {
                    _ = perturbed.UInt64(unrelated, checked(counter * 19 + unrelatedCounter));
                }

                TestAssert.Equal(
                    baseline.UInt64(protectedScope, counter),
                    perturbed.UInt64(protectedScope, counter),
                    $"Unrelated draws changed domain '{domain}' at counter {counter}.");
            }
        }
    }

    public static void RandomSnapshotRestorePreservesFutureResultsAndVersion()
    {
        var original = new CounterBasedRandomV1(Seed);
        RandomScope scope = Scope(RandomDomains.HealthEvents, owner: 100, purpose: 2, occurrence: 88);
        _ = original.UInt64(scope, 0);
        _ = original.Range(scope, 1, -50, 50);

        string json = RandomStateSnapshotJson.Serialize(original.CaptureSnapshot());
        RandomStateSnapshot snapshot = RandomStateSnapshotJson.Deserialize(json);
        CounterBasedRandomV1 restored = CounterBasedRandomV1.Restore(snapshot);

        TestAssert.Equal(Seed, restored.Seed);
        TestAssert.Equal(CounterBasedRandomV1.Version, restored.AlgorithmVersion);
        TestAssert.Equal(CounterBasedRandomV1.Version.Value, snapshot.AlgorithmVersion);

        for (uint counter = 100; counter < 164; counter++)
        {
            TestAssert.Equal(original.UInt64(scope, counter), restored.UInt64(scope, counter));
        }

        TestAssert.Throws<NotSupportedException>(
            () => CounterBasedRandomV1.Restore(
                new RandomStateSnapshot(snapshot.RootSeed, snapshot.AlgorithmVersion + 1)));
    }

    public static void RangeGenerationRespectsBoundsAndCursorCounters()
    {
        var random = new CounterBasedRandomV1(Seed);
        RandomScope scope = Scope(RandomDomains.JobSelectionTieBreaking, owner: 19, purpose: 23, occurrence: 29);

        for (uint counter = 0; counter < 10_000; counter++)
        {
            int narrow = random.Range(scope, counter, -7, 8);
            TestAssert.True(narrow >= -7 && narrow < 8);

            int wide = random.Range(scope, counter, int.MinValue, int.MaxValue);
            TestAssert.True(wide >= int.MinValue && wide < int.MaxValue);

            TestAssert.Equal(0, random.Range(scope, counter, 0, 1));
        }

        TestAssert.Throws<ArgumentOutOfRangeException>(() => random.Range(scope, 0, 5, 5));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => random.Range(scope, 0, 6, 5));

        var cursor = new RandomCursor(random, scope);
        TestAssert.Equal(random.UInt64(scope, 0), cursor.UInt64());
        TestAssert.Equal(random.Range(scope, 1, 10, 20), cursor.Range(10, 20));
        TestAssert.Equal(2U, cursor.Counter);
    }

    public static void ProbabilityEdgeCasesAndValidationBehaveCorrectly()
    {
        var random = new CounterBasedRandomV1(Seed);
        RandomScope scope = Scope(RandomDomains.EquipmentFailures, owner: 4, purpose: 55, occurrence: 144);

        for (uint counter = 0; counter < 128; counter++)
        {
            TestAssert.True(!random.Chance(scope, counter, numerator: 0, denominator: 100));
            TestAssert.True(random.Chance(scope, counter, numerator: 100, denominator: 100));
        }

        bool sawTrue = false;
        bool sawFalse = false;
        for (uint counter = 0; counter < 128; counter++)
        {
            if (random.Chance(scope, counter, numerator: 1, denominator: 2))
            {
                sawTrue = true;
            }
            else
            {
                sawFalse = true;
            }
        }

        TestAssert.True(sawTrue && sawFalse, "Half-probability sample did not exercise both outcomes.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => random.Chance(scope, 0, 0, 0));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => random.Chance(scope, 0, 2, 1));
        TestAssert.Throws<ArgumentException>(() => random.UInt64(default, 0));
        TestAssert.Throws<ArgumentException>(() => random.Chance(default, 0, 0, 100));
    }

    public static void RandomMetadataParticipatesInCanonicalChecksums()
    {
        var first = new SimulationFixture(
            Array.Empty<ISimSystem>(),
            seed: new SimulationSeed(100));
        var same = new SimulationFixture(
            Array.Empty<ISimSystem>(),
            seed: new SimulationSeed(100));
        var different = new SimulationFixture(
            Array.Empty<ISimSystem>(),
            seed: new SimulationSeed(101));

        ulong worldOnlyFirst = StateChecksum.Compute(first.Clock.CurrentTick, first.World, first.Scheduler);
        ulong worldOnlyDifferent = StateChecksum.Compute(different.Clock.CurrentTick, different.World, different.Scheduler);
        TestAssert.Equal(worldOnlyFirst, worldOnlyDifferent);

        ulong completeFirst = StateChecksum.Compute(first.Clock.CurrentTick, first.World, first.Scheduler, first.Random);
        ulong completeSame = StateChecksum.Compute(same.Clock.CurrentTick, same.World, same.Scheduler, same.Random);
        ulong completeDifferent = StateChecksum.Compute(different.Clock.CurrentTick, different.World, different.Scheduler, different.Random);
        TestAssert.Equal(completeFirst, completeSame);
        TestAssert.True(completeFirst != completeDifferent);

        first.Runner.RunOneTick();
        different.Runner.RunOneTick();
        TestAssert.True(
            first.Checksums.At(new SimTick(1)) != different.Checksums.At(new SimTick(1)),
            "Runner diagnostics omitted random metadata from canonical state.");
    }

    public static void RandomRequestTracingIsDiagnosticAndDoesNotAffectOutputs()
    {
        var trace = new RandomRequestTrace();
        var traced = new CounterBasedRandomV1(Seed, trace);
        var untraced = new CounterBasedRandomV1(Seed);
        RandomScope scope = Scope(RandomDomains.CulturalChange, owner: 8, purpose: 13, occurrence: 21);

        ulong value64 = traced.UInt64(scope, 4);
        uint value32 = traced.UInt32(scope, 5);
        int ranged = traced.Range(scope, 6, 10, 20);
        bool chance = traced.Chance(scope, 7, 3, 10);

        TestAssert.Equal(untraced.UInt64(scope, 4), value64);
        TestAssert.Equal(untraced.UInt32(scope, 5), value32);
        TestAssert.Equal(untraced.Range(scope, 6, 10, 20), ranged);
        TestAssert.Equal(untraced.Chance(scope, 7, 3, 10), chance);

        TestAssert.Equal(4, trace.Requests.Count);
        TestAssert.Equal(RandomRequestKind.UInt64, trace.Requests[0].Kind);
        TestAssert.Equal(RandomRequestKind.UInt32, trace.Requests[1].Kind);
        TestAssert.Equal(RandomRequestKind.Range, trace.Requests[2].Kind);
        TestAssert.Equal(RandomRequestKind.Chance, trace.Requests[3].Kind);
        TestAssert.Equal(scope, trace.Requests[2].Scope);
        TestAssert.Equal(6U, trace.Requests[2].Counter);
        TestAssert.Equal(10L, trace.Requests[2].ParameterA);
        TestAssert.Equal(20L, trace.Requests[2].ParameterB);
        TestAssert.Equal(Seed, trace.Requests[2].Seed);
        TestAssert.Equal(CounterBasedRandomV1.Version, trace.Requests[2].AlgorithmVersion);

        trace.Clear();
        TestAssert.Equal(0, trace.Requests.Count);
    }

    private static RandomScope Scope(
        RandomDomainId domain,
        ulong owner,
        ulong purpose,
        ulong occurrence)
        => new(domain, owner, purpose, occurrence);
}
