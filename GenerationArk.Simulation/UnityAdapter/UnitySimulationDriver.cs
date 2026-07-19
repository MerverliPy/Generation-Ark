#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace GenerationArk.Simulation.UnityAdapter;

/// <summary>
/// Thin Unity boundary. Time.unscaledDeltaTime is consumed only to request calls to RunOneTick.
/// Authoritative systems never receive Unity time or Unity scene objects.
/// </summary>
public sealed class UnitySimulationDriver : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int _maxTicksPerFrame = 512;

    [SerializeField]
    private int _initialSpeedMultiplier = SimulationSpeedProfile.Normal;

    [SerializeField]
    private SimulationPresentationBridge[] _presentationBridges =
        Array.Empty<SimulationPresentationBridge>();

    private UnitySimulationAdapter? _adapter;

    public bool IsInitialized => _adapter is not null;

    public FrameAdvanceResult LastFrameResult { get; private set; }

    /// <summary>
    /// Called by the Unity composition root after constructing the engine-independent simulation runner.
    /// Example: driver.Initialize(runner.RunOneTick).
    /// </summary>
    public void Initialize(Action runOneTick)
    {
        if (runOneTick is null)
        {
            throw new ArgumentNullException(nameof(runOneTick));
        }
        SimulationSpeedProfile.ValidateRunning(
            _initialSpeedMultiplier,
            nameof(_initialSpeedMultiplier));

        _adapter = new UnitySimulationAdapter(
            runOneTick,
            _maxTicksPerFrame);
        _adapter.SetSpeedMultiplier(_initialSpeedMultiplier);
    }

    public void SetSpeedMultiplier(int multiplier)
        => RequireAdapter().SetSpeedMultiplier(multiplier);

    public void Pause()
        => RequireAdapter().Pause();

    public void Resume()
        => RequireAdapter().Resume();

    public void StepOneTick()
        => RequireAdapter().StepOneTick();

    public void ResetPresentationState()
        => RequireAdapter().ResetPresentationState();

    private void Update()
    {
        if (_adapter is null)
        {
            return;
        }

        LastFrameResult = _adapter.AdvanceFrame(Time.unscaledDeltaTime);
        if (!LastFrameResult.ShouldPresent)
        {
            return;
        }

        foreach (SimulationPresentationBridge bridge in _presentationBridges)
        {
            if (bridge is not null)
            {
                bridge.Present(LastFrameResult);
            }
        }
    }

    private UnitySimulationAdapter RequireAdapter()
        => _adapter ?? throw new InvalidOperationException(
            "UnitySimulationDriver.Initialize must be called before using simulation controls.");
}
#endif
