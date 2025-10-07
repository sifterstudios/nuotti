namespace Nuotti.SimKit.Actors;

public sealed record AudienceOptions
{
    /// <summary>
    /// Probability [0..1] to pick the correct answer when answering.
    /// </summary>
    public double CorrectnessRatio { get; init; } = 0.5;

    /// <summary>
    /// Minimum delay before answering, inclusive.
    /// </summary>
    public TimeSpan MinDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Maximum delay before answering, inclusive.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Probability [0..1] to not answer at all in a round.
    /// </summary>
    public double DropRate { get; init; } = 0.0;

    /// <summary>
    /// Optional seed to make randomness deterministic for tests.
    /// </summary>
    public int? RandomSeed { get; init; }
}