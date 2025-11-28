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
    /// Comma-separated list of allowed CORS origins (only used in non-Development environments).
    /// Example: "https://app.example.com,https://projector.example.com".
    /// Environment variable: NUOTTI_ALLOWEDORIGINS
    /// </summary>
    public string? AllowedOrigins { get; init; }
        = null;

    /// <summary>
    /// Idle timeout (in seconds) after which a session is evicted if no connections touched it.
    /// </summary>
    public int SessionIdleTimeoutSeconds { get; init; } = 900; // 15 minutes

    /// <summary>
    /// How often the eviction loop scans for idle sessions (in seconds).
    /// </summary>
    public int SessionEvictionIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Idempotency: how long a CommandId is remembered (seconds).
    /// </summary>
    public int IdempotencyTtlSeconds { get; init; } = 600; // 10 minutes

    /// <summary>
    /// Idempotency: maximum number of recent CommandIds stored per session.
    /// </summary>
    public int IdempotencyMaxPerSession { get; init; } = 128;

    /// <summary>
    /// Alerting: threshold in seconds before alerting on missing Engine/Projector role.
    /// Default: 30 seconds.
    /// </summary>
    public int MissingRoleAlertThresholdSeconds { get; init; } = 30;

    /// <summary>
    /// Alerting: webhook URL for sending alerts (optional, disabled if not set).
    /// Environment variable: NUOTTI_ALERTINGWEBHOOKURL
    /// </summary>
    public string? AlertingWebhookUrl { get; init; }

    /// <summary>
    /// Feature flags dictionary for runtime feature toggles.
    /// Example: Features:ExperimentalFeature: true
    /// Environment variable: NUOTTI_FEATURES__EXPERIMENTALFEATURE=true
    /// </summary>
    public Dictionary<string, bool> Features { get; init; } = new();
}