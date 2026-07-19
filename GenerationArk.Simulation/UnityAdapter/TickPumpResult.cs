namespace GenerationArk.Simulation.UnityAdapter;

/// <summary>
/// Non-authoritative frame-pump telemetry. None of these values participate in simulation state.
/// </summary>
public readonly record struct TickPumpResult(
    int TicksExecuted,
    long WholeTicksBacklogged,
    double FractionalTicks,
    double AccumulatedTicks,
    double RequestedTicksThisFrame,
    bool BudgetLimited)
{
    public bool IsCatchingUp => WholeTicksBacklogged > 0;
}
