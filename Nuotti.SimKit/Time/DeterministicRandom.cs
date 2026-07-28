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
/// </remarks>
public static class DeterministicRandom
{
    public static Random ForLane(int seed, int laneIndex) => new(unchecked(seed * 397 + laneIndex));
}
