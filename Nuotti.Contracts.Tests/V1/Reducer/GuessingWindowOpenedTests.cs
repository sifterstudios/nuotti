using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Reducer;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Reducer;

public class GuessingWindowOpenedTests
{
    static GuessingWindowOpened Window(int seconds, DateTime? emittedAt = null) => new(seconds)
    {
        WindowSeconds = seconds,
        SessionCode = "dev",
        CorrelationId = Guid.Empty,
        CausedByCommandId = Guid.Empty,
        EmittedAtUtc = emittedAt ?? new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void Stamps_the_window_clock_from_EmittedAtUtc()
    {
        var opened = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var state = GameReducer.Initial("dev") with
        {
            Phase = Phase.Guessing,
            Choices = ["a", "b"],
            Tallies = [0, 0]
        };

        var (next, error) = GameReducer.Reduce(state, Window(45, opened));

        error.Should().BeNull();
        next.GuessingWindowSeconds.Should().Be(45);
        next.GuessingWindowOpenedAtUtc.Should().Be(opened);
        next.GuessingWindowDeadlineUtc.Should().Be(opened.AddSeconds(45));
    }

    [Fact]
    public void Clears_live_answers_and_rezeroes_tallies_for_a_new_window()
    {
        var state = GameReducer.Initial("dev") with
        {
            Phase = Phase.Guessing,
            Choices = ["a", "b", "c"],
            Tallies = [1, 2, 0],
            Answers = new Dictionary<string, int> { ["aud-1"] = 1 },
            AnswerReceivedAtUtc = new Dictionary<string, DateTime>
            {
                ["aud-1"] = new DateTime(2024, 6, 1, 11, 59, 0, DateTimeKind.Utc)
            },
            LockedAnswers = new Dictionary<string, int> { ["aud-2"] = 0 }
        };

        var (next, error) = GameReducer.Reduce(state, Window(30));

        error.Should().BeNull();
        next.Answers.Should().BeEmpty();
        next.AnswerReceivedAtUtc.Should().BeEmpty();
        next.Tallies.Should().Equal(0, 0, 0);
        // Lock-held answers survive for Reveal.
        next.LockedAnswers.Should().ContainKey("aud-2");
    }

    [Fact]
    public void Phase_then_window_opens_Guessing_with_a_reconstructable_clock()
    {
        // Mirrors EffectsFor(OpenAnswers): GamePhaseChanged then GuessingWindowOpened.
        var opened = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var state = GameReducer.Initial("dev") with
        {
            Phase = Phase.Start,
            Choices = ["a", "b"],
            Tallies = [0, 0]
        };

        (state, var phaseError) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Start, Phase.Guessing)
        {
            CurrentPhase = Phase.Start,
            NewPhase = Phase.Guessing,
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        phaseError.Should().BeNull();
        state.Phase.Should().Be(Phase.Guessing);

        var (next, error) = GameReducer.Reduce(state, Window(20, opened));

        error.Should().BeNull();
        next.GuessingWindowDeadlineUtc.Should().Be(opened.AddSeconds(20));
        next.GuessingWindowOpenedAtUtc.Should().Be(opened);
    }
}
