namespace Nuotti.SimKit.Time;

/// <summary>
/// Per-lane random sources derived from one run seed.
/// </summary>
/// <remarks>
/// One shared Random across concurrently running lanes is not thread-safe, and interleaving
/// would make the draw order — and therefore the run — irreproducible. Deriving one instance
/// per lane keeps each lane's sequence stable no matter how the lanes interleave.
///
/// Do NOT derive the per-lane seed with <see cref="HashCode.Combine{T1, T2}(T1, T2)"/> or any other
/// <c>System.HashCode</c> API. <c>HashCode.Combine</c> mixes in a per-process random seed (generated
/// once from a CSPRNG at process start) specifically so its output is *not* reproducible across
/// runs — the same anti-hash-flooding design as randomized <c>string.GetHashCode()</c>. That means
/// the same (seed, laneIndex) pair would derive a different <see cref="Random"/> seed every time the
/// process starts, which defeats the entire point of this type: "run the same scenario seed again
/// and get the same sequence." The plain integer arithmetic below has no process-local entropy, so
/// the same inputs always produce the same output on every run, on every machine.
///
/// Also do NOT derive the per-lane seed with a linear combination of adjacent inputs, e.g.
/// <c>seed * 397 + laneIndex</c>. .NET's seeded <see cref="Random"/> initializes its internal state
/// linearly from the seed, so consecutive derived seeds produce draws that are themselves a smooth,
/// deterministic function of laneIndex — consecutive lanes are correlated, not independent, and the
/// mapping is not even injective (that formula sends both (seed: 0, laneIndex: 397) and
/// (seed: 1, laneIndex: 0) to the same derived seed, 397). A harness that spreads chaos or jittered
/// latency across 200+ lanes needs those lanes to look like independent samples, not an arithmetic
/// ramp. The finalizer-style integer mix below (Murmur3-inspired) scrambles the two inputs together
/// so adjacent (seed, laneIndex) pairs land on unrelated derived seeds, while remaining pure integer
/// arithmetic with no process-local entropy — so cross-process reproducibility is preserved.
/// </remarks>
public static class LaneRandom
{
    public static Random ForLane(int seed, int laneIndex)
    {
        unchecked
        {
            uint h = (uint)seed * 2654435761u + (uint)laneIndex * 2246822519u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return new Random((int)h);
        }
    }
}
