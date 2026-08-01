using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Contracts.Tests.V1.Model;

public class ScoringCalculatorTests
{
    readonly ScoringPolicy _policy = ScoringPolicy.Standard;

    [Fact]
    public void Immediate_correct_answer_earns_ceiling()
    {
        var opened = DateTime.UtcNow;
        var points = ScoringCalculator.PointsForCorrect(_policy, opened, opened);
        Assert.Equal(1500, points);
    }

    [Fact]
    public void Answer_after_speed_window_earns_correct_floor()
    {
        var opened = DateTime.UtcNow;
        var points = ScoringCalculator.PointsForCorrect(_policy, opened, opened.AddMilliseconds(10_000));
        Assert.Equal(1000, points);
    }

    [Fact]
    public void Mid_window_answer_decays_bonus()
    {
        var opened = DateTime.UtcNow;
        var points = ScoringCalculator.PointsForCorrect(_policy, opened, opened.AddMilliseconds(5_000));
        Assert.Equal(1250, points);
    }

    [Fact]
    public void Preserved_earlier_correctness_wins()
    {
        var opened = DateTime.UtcNow;
        var points = ScoringCalculator.PointsForCorrect(_policy, opened, opened.AddMinutes(1), preservedPoints: 1500);
        Assert.Equal(1500, points);
    }
}

public class GameStateSnapshotViewsRankingTests
{
    [Fact]
    public void TopPlayers_uses_shared_tie_ranks()
    {
        var state = new GameStateSnapshot("S", Phase.Reveal, 0, scores: new Dictionary<string, int>
        {
            ["a"] = 10,
            ["b"] = 10,
            ["c"] = 5,
            ["d"] = 3
        });

        var top = state.TopPlayers(10);
        Assert.Equal(4, top.Count);
        Assert.Equal(1, top[0].Rank);
        Assert.Equal(1, top[1].Rank);
        Assert.Equal(3, top[2].Rank);
        Assert.Equal(4, top[3].Rank);
        Assert.Equal(10, top[0].Score);
        Assert.Equal(10, top[1].Score);
        Assert.Equal(5, top[2].Score);
    }
}
