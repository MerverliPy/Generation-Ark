using System;
using System.Collections.Generic;

namespace GenerationArk.Simulation.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new List<(string Name, Action Run)>
        {
            (nameof(ClockMilestoneTests.PauseAndSingleStepAdvanceExactlyOneTick), ClockMilestoneTests.PauseAndSingleStepAdvanceExactlyOneTick),
            (nameof(ClockMilestoneTests.FramePatternsProduceIdenticalState), ClockMilestoneTests.FramePatternsProduceIdenticalState),
            (nameof(ClockMilestoneTests.RegistrationOrderDoesNotAffectExecutionOrder), ClockMilestoneTests.RegistrationOrderDoesNotAffectExecutionOrder),
            (nameof(ClockMilestoneTests.DuplicateSystemIdFailsStartupValidation), ClockMilestoneTests.DuplicateSystemIdFailsStartupValidation),
            (nameof(ClockMilestoneTests.CommandsApplyByTickThenSequenceThenId), ClockMilestoneTests.CommandsApplyByTickThenSequenceThenId),
            (nameof(ClockMilestoneTests.SpeedChangesDoNotChangeTickOutcomes), ClockMilestoneTests.SpeedChangesDoNotChangeTickOutcomes),
            (nameof(ClockMilestoneTests.TickBudgetRetainsBacklogInsteadOfDroppingTicks), ClockMilestoneTests.TickBudgetRetainsBacklogInsteadOfDroppingTicks)
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
