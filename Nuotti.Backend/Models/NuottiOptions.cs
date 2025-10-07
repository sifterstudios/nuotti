namespace Nuotti.Backend.Models;

/// <summary>
/// Strongly-typed configuration options for the Backend.
/// These are bound from the configuration section "Nuotti".
/// Environment variables with prefix "NUOTTI_" override matching keys.
/// Example env var: NUOTTI_GREETING
/// </summary>
public sealed class NuottiOptions
{
    /// <summary>
    /// Logical name of the service instance.
    /// </summary>
    public string ServiceName { get; init; } = "Nuotti.Backend";

    /// <summary>
    /// Demo greeting to validate binding in tests.
    /// </summary>
    public string Greeting { get; init; } = "Hello";

    /// <summary>
    /// Idle timeout (in seconds) after which a session is evicted if no connections touched it.
    /// </summary>
    public int SessionIdleTimeoutSeconds { get; init; } = 900; // 15 minutes

    /// <summary>
    /// How often the eviction loop scans for idle sessions (in seconds).
    /// </summary>
    public int SessionEvictionIntervalSeconds { get; init; } = 30;
}