using System;
using System.Collections.Generic;

namespace GenerationArk.Simulation.Core;

public static class SimPhaseOrder
{
    private static readonly SimPhase[] OrderedPhases =
    {
        SimPhase.CommandApply,
        SimPhase.PreSimulation,
        SimPhase.ShipInfrastructure,
        SimPhase.ResourceNetworks,
        SimPhase.AgentState,
        SimPhase.AgentDecision,
        SimPhase.AgentAction,
        SimPhase.SocialAndInstitutions,
        SimPhase.Narrative,
        SimPhase.Commit,
        SimPhase.Diagnostics
    };

    public static IReadOnlyList<SimPhase> All => OrderedPhases;

    public static int IndexOf(SimPhase phase)
    {
        int index = Array.IndexOf(OrderedPhases, phase);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown simulation phase.");
        }

        return index;
    }
}
