namespace Nuotti.UnitTests.TestHelpers;

/// <summary>
/// Helper for creating deterministic Random instances in tests.
/// Use a fixed seed to ensure reproducible test behavior.
/// </summary>
public static class DeterministicRandom
{
    /// <summary>
    /// Creates a Random instance with a fixed seed for deterministic behavior.
    /// </summary>
    /// <param name="seed">Optional seed value. Defaults to 42 for consistency.</param>
    /// <returns>A Random instance with the specified seed.</returns>
    public static Random Create(int seed = 42) => new Random(seed);

    /// <summary>
    /// Creates a Random instance with a seed derived from a test name for per-test determinism.
    /// </summary>
    /// <param name="testName">Test name to derive seed from.</param>
    /// <returns>A Random instance with a seed based on the test name hash.</returns>
    public static Random CreateFromTestName(string testName)
    {
        var seed = testName.GetHashCode(StringComparison.Ordinal);
        return new Random(seed);
    }
}

