#if UNITY_5_3_OR_NEWER
using UnityEngine;

namespace GenerationArk.Simulation.UnityAdapter;

/// <summary>
/// Unity presentation components receive snapshots only when the throttle policy requests a refresh.
/// Implementations must never feed interpolated values back into authoritative simulation state.
/// </summary>
public abstract class SimulationPresentationBridge : MonoBehaviour
{
    public abstract void Present(FrameAdvanceResult frameResult);
}
#endif
