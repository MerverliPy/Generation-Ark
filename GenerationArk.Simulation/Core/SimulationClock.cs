using System;

namespace GenerationArk.Simulation.Core;

public sealed class SimulationClock : ISimulationClock
{
    private bool _singleStepRequested;

    public SimTick CurrentTick { get; private set; } = SimTick.Zero;
    public SimulationSpeed RequestedSpeed { get; private set; } = SimulationSpeed.Paused;
    public bool IsPaused => RequestedSpeed == SimulationSpeed.Paused;

    public void SetSpeed(SimulationSpeed speed)
    {
        if (!Enum.IsDefined(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Unsupported simulation speed.");
        }

        RequestedSpeed = speed;
    }

    public void Pause() => RequestedSpeed = SimulationSpeed.Paused;

    public void RequestSingleStep() => _singleStepRequested = true;

    internal bool ConsumeSingleStepRequest()
    {
        bool requested = _singleStepRequested;
        _singleStepRequested = false;
        return requested;
    }

    internal SimTick AdvanceOneTick()
    {
        CurrentTick += 1;
        return CurrentTick;
    }

    internal void Restore(SimTick tick, SimulationSpeed speed)
    {
        if (tick.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), tick, "Tick cannot be negative.");
        }

        CurrentTick = tick;
        SetSpeed(speed);
        _singleStepRequested = false;
    }
}
