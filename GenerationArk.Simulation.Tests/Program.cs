using System;
using System.Collections.Generic;

namespace GenerationArk.Simulation.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new List<(string Name, Action Run)>
        {
            (nameof(ClockMilestoneTests.SimTickRelationalOperatorsCompareUnderlyingValues), ClockMilestoneTests.SimTickRelationalOperatorsCompareUnderlyingValues),
            (nameof(ClockMilestoneTests.PauseAndSingleStepAdvanceExactlyOneTick), ClockMilestoneTests.PauseAndSingleStepAdvanceExactlyOneTick),
            (nameof(ClockMilestoneTests.FramePatternsProduceIdenticalState), ClockMilestoneTests.FramePatternsProduceIdenticalState),
            (nameof(ClockMilestoneTests.RegistrationOrderDoesNotAffectExecutionOrder), ClockMilestoneTests.RegistrationOrderDoesNotAffectExecutionOrder),
            (nameof(ClockMilestoneTests.DuplicateSystemIdFailsStartupValidation), ClockMilestoneTests.DuplicateSystemIdFailsStartupValidation),
            (nameof(ClockMilestoneTests.CommandsApplyByTickThenSequenceThenId), ClockMilestoneTests.CommandsApplyByTickThenSequenceThenId),
            (nameof(ClockMilestoneTests.SpeedChangesDoNotChangeTickOutcomes), ClockMilestoneTests.SpeedChangesDoNotChangeTickOutcomes),
            (nameof(ClockMilestoneTests.TickBudgetRetainsBacklogInsteadOfDroppingTicks), ClockMilestoneTests.TickBudgetRetainsBacklogInsteadOfDroppingTicks),
            (nameof(SchedulerMilestoneTests.EventsNeverExecuteBeforeTheirDueTick), SchedulerMilestoneTests.EventsNeverExecuteBeforeTheirDueTick),
            (nameof(SchedulerMilestoneTests.EqualTickEventsExecuteByPriorityThenCreationSequence), SchedulerMilestoneTests.EqualTickEventsExecuteByPriorityThenCreationSequence),
            (nameof(SchedulerMilestoneTests.SnapshotArrayOrderDoesNotAffectExecutionOrder), SchedulerMilestoneTests.SnapshotArrayOrderDoesNotAffectExecutionOrder),
            (nameof(SchedulerMilestoneTests.CancellationByIdAndOwnerPreventsExecution), SchedulerMilestoneTests.CancellationByIdAndOwnerPreventsExecution),
            (nameof(SchedulerMilestoneTests.RepeatingEventsRemainAlignedToOriginalCadence), SchedulerMilestoneTests.RepeatingEventsRemainAlignedToOriginalCadence),
            (nameof(SchedulerMilestoneTests.JsonSnapshotRestorePreservesPendingOrderAndFutureState), SchedulerMilestoneTests.JsonSnapshotRestorePreservesPendingOrderAndFutureState),
            (nameof(SchedulerMilestoneTests.SamePhaseSchedulingDefersToTheNextTick), SchedulerMilestoneTests.SamePhaseSchedulingDefersToTheNextTick),
            (nameof(SchedulerMilestoneTests.LaterPhaseSchedulingCanExecuteInTheCurrentTick), SchedulerMilestoneTests.LaterPhaseSchedulingCanExecuteInTheCurrentTick),
            (nameof(SchedulerMilestoneTests.SchedulerStateParticipatesInCanonicalChecksums), SchedulerMilestoneTests.SchedulerStateParticipatesInCanonicalChecksums),
            (nameof(SchedulerMilestoneTests.PastDueEventsAndNonPositiveRepeatsAreRejected), SchedulerMilestoneTests.PastDueEventsAndNonPositiveRepeatsAreRejected),
            (nameof(RandomMilestoneTests.IdenticalScopesAndCountersProduceIdenticalOutputs), RandomMilestoneTests.IdenticalScopesAndCountersProduceIdenticalOutputs),
            (nameof(RandomMilestoneTests.DifferentDomainsAndOwnersProduceIndependentOutputSequences), RandomMilestoneTests.DifferentDomainsAndOwnersProduceIndependentOutputSequences),
            (nameof(RandomMilestoneTests.UnrelatedDrawsDoNotAlterOtherDomains), RandomMilestoneTests.UnrelatedDrawsDoNotAlterOtherDomains),
            (nameof(RandomMilestoneTests.RandomSnapshotRestorePreservesFutureResultsAndVersion), RandomMilestoneTests.RandomSnapshotRestorePreservesFutureResultsAndVersion),
            (nameof(RandomMilestoneTests.RangeGenerationRespectsBoundsAndCursorCounters), RandomMilestoneTests.RangeGenerationRespectsBoundsAndCursorCounters),
            (nameof(RandomMilestoneTests.ProbabilityEdgeCasesAndValidationBehaveCorrectly), RandomMilestoneTests.ProbabilityEdgeCasesAndValidationBehaveCorrectly),
            (nameof(RandomMilestoneTests.RandomMetadataParticipatesInCanonicalChecksums), RandomMilestoneTests.RandomMetadataParticipatesInCanonicalChecksums),
            (nameof(RandomMilestoneTests.RandomRequestTracingIsDiagnosticAndDoesNotAffectOutputs), RandomMilestoneTests.RandomRequestTracingIsDiagnosticAndDoesNotAffectOutputs),
            (nameof(DiagnosticsMilestoneTests.CanonicalChecksumIsRepeatableForIdenticalState), DiagnosticsMilestoneTests.CanonicalChecksumIsRepeatableForIdenticalState),
            (nameof(DiagnosticsMilestoneTests.ComponentRegistrationOrderDoesNotAffectChecksums), DiagnosticsMilestoneTests.ComponentRegistrationOrderDoesNotAffectChecksums),
            (nameof(DiagnosticsMilestoneTests.DuplicateComponentIdsFailValidation), DiagnosticsMilestoneTests.DuplicateComponentIdsFailValidation),
            (nameof(DiagnosticsMilestoneTests.DiagnosticTracingDoesNotAlterAuthoritativeOutcomes), DiagnosticsMilestoneTests.DiagnosticTracingDoesNotAlterAuthoritativeOutcomes),
            (nameof(DiagnosticsMilestoneTests.TickTracePreservesStableExecutionOrder), DiagnosticsMilestoneTests.TickTracePreservesStableExecutionOrder),
            (nameof(DiagnosticsMilestoneTests.TraceRetentionIsBoundedWithoutChangingChecksums), DiagnosticsMilestoneTests.TraceRetentionIsBoundedWithoutChangingChecksums),
            (nameof(DiagnosticsMilestoneTests.DesyncDetectionFindsTheFirstDivergentTick), DiagnosticsMilestoneTests.DesyncDetectionFindsTheFirstDivergentTick),
            (nameof(UnityAdapterMilestoneTests.FrameAccumulatorRetainsFractionalTicks), UnityAdapterMilestoneTests.FrameAccumulatorRetainsFractionalTicks),
            (nameof(UnityAdapterMilestoneTests.PerFrameBudgetRetainsBacklog), UnityAdapterMilestoneTests.PerFrameBudgetRetainsBacklog),
            (nameof(UnityAdapterMilestoneTests.PausedFramesRetainExistingBacklog), UnityAdapterMilestoneTests.PausedFramesRetainExistingBacklog),
            (nameof(UnityAdapterMilestoneTests.ManualStepAdvancesExactlyOneTickWhilePaused), UnityAdapterMilestoneTests.ManualStepAdvancesExactlyOneTickWhilePaused),
            (nameof(UnityAdapterMilestoneTests.SpeedControlsUseDocumentedMultipliers), UnityAdapterMilestoneTests.SpeedControlsUseDocumentedMultipliers),
            (nameof(UnityAdapterMilestoneTests.FastForwardThrottlingDoesNotChangeTickExecution), UnityAdapterMilestoneTests.FastForwardThrottlingDoesNotChangeTickExecution),
            (nameof(UnityAdapterMilestoneTests.InvalidFrameInputsAreRejected), UnityAdapterMilestoneTests.InvalidFrameInputsAreRejected),
            (nameof(UnityAdapterMilestoneTests.FramePatternsProduceIdenticalAdapterTickCounts), UnityAdapterMilestoneTests.FramePatternsProduceIdenticalAdapterTickCounts),
            (nameof(ReplayContinuityMilestoneTests.HeadlessRunnerAdvancesExactTicksAndCapturesCheckpoints), ReplayContinuityMilestoneTests.HeadlessRunnerAdvancesExactTicksAndCapturesCheckpoints),
            (nameof(ReplayContinuityMilestoneTests.HeadlessRunnerRejectsInvalidTickProgress), ReplayContinuityMilestoneTests.HeadlessRunnerRejectsInvalidTickProgress),
            (nameof(ReplayContinuityMilestoneTests.ReplayLogJsonRoundTripIsCanonical), ReplayContinuityMilestoneTests.ReplayLogJsonRoundTripIsCanonical),
            (nameof(ReplayContinuityMilestoneTests.ReplayRunnerAppliesCommandsInStableOrder), ReplayContinuityMilestoneTests.ReplayRunnerAppliesCommandsInStableOrder),
            (nameof(ReplayContinuityMilestoneTests.ReplayRunnerDetectsCheckpointMismatch), ReplayContinuityMilestoneTests.ReplayRunnerDetectsCheckpointMismatch),
            (nameof(ReplayContinuityMilestoneTests.SaveEnvelopeRoundTripPreservesMetadataAndPayload), ReplayContinuityMilestoneTests.SaveEnvelopeRoundTripPreservesMetadataAndPayload),
            (nameof(ReplayContinuityMilestoneTests.SaveLoadContinuityMatchesUninterruptedRun), ReplayContinuityMilestoneTests.SaveLoadContinuityMatchesUninterruptedRun),
            (nameof(ReplayContinuityMilestoneTests.FramePatternsProduceIdenticalCheckpointChecksums), ReplayContinuityMilestoneTests.FramePatternsProduceIdenticalCheckpointChecksums),
            (nameof(ReplayContinuityMilestoneTests.TenYearFoundationSoakRepeatsFinalChecksum), ReplayContinuityMilestoneTests.TenYearFoundationSoakRepeatsFinalChecksum),
            (nameof(ReplayContinuityMilestoneTests.SoakCheckpointRetentionRemainsBounded), ReplayContinuityMilestoneTests.SoakCheckpointRetentionRemainsBounded),
            (nameof(DiagnosticsMilestoneTests.DesyncReportIdentifiesChangedAndMissingComponents), DiagnosticsMilestoneTests.DesyncReportIdentifiesChangedAndMissingComponents),
            (nameof(EntityLifecycleMilestoneTests.EntityIdsAreMonotonicAndNeverReused), EntityLifecycleMilestoneTests.EntityIdsAreMonotonicAndNeverReused),
            (nameof(EntityLifecycleMilestoneTests.EntityIterationIsCanonicalRegardlessOfInsertionOrder), EntityLifecycleMilestoneTests.EntityIterationIsCanonicalRegardlessOfInsertionOrder),
            (nameof(EntityLifecycleMilestoneTests.DuplicateEntityAndComponentTypeRegistrationFailsFast), EntityLifecycleMilestoneTests.DuplicateEntityAndComponentTypeRegistrationFailsFast),
            (nameof(EntityLifecycleMilestoneTests.StructuralMutationsRemainInvisibleUntilCommit), EntityLifecycleMilestoneTests.StructuralMutationsRemainInvisibleUntilCommit),
            (nameof(EntityLifecycleMilestoneTests.CreatedEntitiesActivateAtNextPreSimulationPhase), EntityLifecycleMilestoneTests.CreatedEntitiesActivateAtNextPreSimulationPhase),
            (nameof(EntityLifecycleMilestoneTests.CommitAppliesMutationsInStableBufferedOrder), EntityLifecycleMilestoneTests.CommitAppliesMutationsInStableBufferedOrder),
            (nameof(EntityLifecycleMilestoneTests.DestroyEntityRemovesComponentsAndCancelsOwnedEvents), EntityLifecycleMilestoneTests.DestroyEntityRemovesComponentsAndCancelsOwnedEvents),
            (nameof(EntityLifecycleMilestoneTests.ConflictingMutationBatchFailsBeforePartialApplication), EntityLifecycleMilestoneTests.ConflictingMutationBatchFailsBeforePartialApplication),
            (nameof(EntityLifecycleMilestoneTests.EntityStateSaveLoadRoundTripIsCanonical), EntityLifecycleMilestoneTests.EntityStateSaveLoadRoundTripIsCanonical),
            (nameof(EntityLifecycleMilestoneTests.EntityLifecycleReplayFramePatternsAndChurnSoakMatchChecksums), EntityLifecycleMilestoneTests.EntityLifecycleReplayFramePatternsAndChurnSoakMatchChecksums),
            (nameof(MapTopologyMilestoneTests.CellIdsUseCanonicalRowMajorCoordinates), MapTopologyMilestoneTests.CellIdsUseCanonicalRowMajorCoordinates),
            (nameof(MapTopologyMilestoneTests.InvalidGridDimensionsAndCoordinatesFailFast), MapTopologyMilestoneTests.InvalidGridDimensionsAndCoordinatesFailFast),
            (nameof(MapTopologyMilestoneTests.CellIterationIsCanonicalRegardlessOfWriteOrder), MapTopologyMilestoneTests.CellIterationIsCanonicalRegardlessOfWriteOrder),
            (nameof(MapTopologyMilestoneTests.DuplicateMapCellDefinitionIdsFailFast), MapTopologyMilestoneTests.DuplicateMapCellDefinitionIdsFailFast),
            (nameof(MapTopologyMilestoneTests.MapMutationsRemainInvisibleUntilCommit), MapTopologyMilestoneTests.MapMutationsRemainInvisibleUntilCommit),
            (nameof(MapTopologyMilestoneTests.ConflictingMapMutationBatchFailsBeforePartialApplication), MapTopologyMilestoneTests.ConflictingMapMutationBatchFailsBeforePartialApplication),
            (nameof(MapTopologyMilestoneTests.RoomTopologyUsesCardinalConnectivityAndStableRoomIds), MapTopologyMilestoneTests.RoomTopologyUsesCardinalConnectivityAndStableRoomIds),
            (nameof(MapTopologyMilestoneTests.RoomTopologySplitAndMergeRebuildsDeterministically), MapTopologyMilestoneTests.RoomTopologySplitAndMergeRebuildsDeterministically),
            (nameof(MapTopologyMilestoneTests.MapStateSaveLoadRoundTripIsCanonical), MapTopologyMilestoneTests.MapStateSaveLoadRoundTripIsCanonical),
            (nameof(MapTopologyMilestoneTests.MapReplayFramePatternsAndTopologyChurnMatchChecksums), MapTopologyMilestoneTests.MapReplayFramePatternsAndTopologyChurnMatchChecksums)
        };

        int failures = 0;
        foreach ((string name, Action run) in tests)
        {
            try
            {
                run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}");
                Console.Error.WriteLine(exception);
            }
        }

        Console.WriteLine($"{tests.Count - failures}/{tests.Count} tests passed.");
        return failures == 0 ? 0 : 1;
    }
}
