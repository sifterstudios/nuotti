namespace Nuotti.SimKit.Time;

/// <summary>
/// Per-lane random sources derived from one run seed.
/// </summary>
/// <remarks>
/// One shared Random across concurrently running lanes is not thread-safe, and interleaving
/// would make the draw order — and therefore the run — irreproducible. Deriving one instance
/// per lane keeps each lane's sequence stable no matter how the lanes interleave.
/// </remarks>
public static class DeterministicRandom
{
    public static Random ForLane(int seed, int laneIndex) => new(HashCode.Combine(seed, laneIndex));
}
