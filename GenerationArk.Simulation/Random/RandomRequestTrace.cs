using System.Collections.Generic;

namespace GenerationArk.Simulation.Random;

/// <summary>
/// Diagnostic-only request history. It is deliberately excluded from authoritative checksums.
/// </summary>
public sealed class RandomRequestTrace : IRandomRequestTracer
{
    private readonly List<RandomRequest> _requests = new();

    public IReadOnlyList<RandomRequest> Requests => _requests;

    public void Record(RandomRequest request) => _requests.Add(request);

    public void Clear() => _requests.Clear();
}
