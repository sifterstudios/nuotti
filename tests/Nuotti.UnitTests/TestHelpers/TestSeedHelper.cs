namespace Nuotti.UnitTests.TestHelpers;

/// <summary>
/// Helper for managing test seeds and ensuring deterministic test behavior.
/// Seeds are logged to help debug flaky tests.
/// </summary>
public static class TestSeedHelper
{
    /// <summary>
    /// Gets a deterministic seed for a test based on its name and optional parameters.
    /// </summary>
    /// <param name="testName">Name of the test method.</param>
    /// <param name="additionalData">Optional additional data to include in seed calculation.</param>
    /// <returns>A deterministic integer seed.</returns>
    public static int GetSeedForTest(string testName, string? additionalData = null)
    {
        var seedSource = testName;
        if (!string.IsNullOrWhiteSpace(additionalData))
        {
            seedSource += additionalData;
        }
        var seed = seedSource.GetHashCode(StringComparison.Ordinal);
        // Ensure positive seed
        return Math.Abs(seed);
    }

    /// <summary>
    /// Creates a Random instance with a seed based on the test name.
    /// Logs the seed for debugging flaky tests.
    /// </summary>
    /// <param name="testName">Name of the test method.</param>
    /// <param name="logger">Optional action to log the seed (for debugging).</param>
    /// <returns>A Random instance with a deterministic seed.</returns>
    public static Random CreateRandomForTest(string testName, Action<string>? logger = null)
    {
        var seed = GetSeedForTest(testName);
        logger?.Invoke($"Test '{testName}' using seed: {seed}");
        return new Random(seed);
    }
}




