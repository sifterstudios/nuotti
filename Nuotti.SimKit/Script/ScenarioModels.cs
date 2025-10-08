using Nuotti.SimKit.Actors;
namespace Nuotti.SimKit.Script;

/// <summary>
/// High-level scenario document that combines audience simulation options with a performer script.
/// Intended for baseline end-to-end runs in CI.
/// </summary>
public sealed record BaselineScenario
{
    /// <summary>
    /// Number of simulated audience clients to spawn.
    /// </summary>
    public int AudienceCount { get; init; } = 0;

    /// <summary>
    /// Options that control audience answer timing and behavior.
    /// </summary>
    public AudienceOptions Audience { get; init; } = new();

    /// <summary>
    /// The performer script to drive the session flow.
    /// </summary>
    public ScriptModel Script { get; init; } = new();
}