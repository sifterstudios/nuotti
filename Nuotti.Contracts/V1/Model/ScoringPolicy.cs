namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// Versioned scoring parameters captured for a Session (ceilings, speed bonus, decay window).
/// </summary>
public sealed record ScoringPolicy(
    string PolicyId,
    int Version,
    int CorrectPoints,
    int SpeedBonusPoints,
    long SpeedBonusWindowMs)
{
    public static ScoringPolicy Standard { get; } = new("standard", 1, 1000, 500, 10_000);
}

/// <summary>
/// Pure scoring for a locked correct answer. Earlier correctness is preserved by the caller
/// (passing a prior award skips re-calculation).
/// </summary>
public static class ScoringCalculator
{
    /// <summary>
    /// Points for a correct answer received at <paramref name="receivedAtUtc"/> relative to the
    /// Guessing Window open time. Decays the speed bonus over <see cref="ScoringPolicy.SpeedBonusWindowMs"/>.
    /// </summary>
    public static int PointsForCorrect(
        ScoringPolicy policy,
        DateTime windowOpenedAtUtc,
        DateTime receivedAtUtc,
        int? preservedPoints = null)
    {
        if (preservedPoints is int prior && prior > 0)
            return prior;

        var ceiling = checked(policy.CorrectPoints + policy.SpeedBonusPoints);
        var elapsedMs = Math.Max(0d, (receivedAtUtc - windowOpenedAtUtc).TotalMilliseconds);
        var windowMs = Math.Max(1d, policy.SpeedBonusWindowMs);
        if (elapsedMs >= windowMs)
            return policy.CorrectPoints;

        var decayedBonus = (int)Math.Round(policy.SpeedBonusPoints * (1d - elapsedMs / windowMs));
        return Math.Clamp(policy.CorrectPoints + decayedBonus, policy.CorrectPoints, ceiling);
    }
}
