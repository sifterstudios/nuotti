using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Reducer;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Reducer;

public class QuestionOfferedTests
{
    [Fact]
    public void Puts_the_choices_on_the_snapshot()
    {
        var state = GameReducer.Initial("dev");

        var (next, error) = GameReducer.Reduce(state, new QuestionOffered("Which song?", ["a", "b", "c", "d"])
        {
            Text = "Which song?",
            Choices = ["a", "b", "c", "d"],
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        error.Should().BeNull();
        next.Choices.Should().Equal("a", "b", "c", "d");
    }

    [Fact]
    public void Sizes_the_tallies_to_the_choices_and_zeroes_them()
    {
        var state = GameReducer.Initial("dev");

        var (next, error) = GameReducer.Reduce(state, new QuestionOffered("Which song?", ["a", "b", "c", "d"])
        {
            Text = "Which song?",
            Choices = ["a", "b", "c", "d"],
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        error.Should().BeNull();
        // Without this, AnswerSubmitted's bounds check against Choices.Count rejects every
        // answer and no tally ever moves — the defect this task exists to fix.
        next.Tallies.Should().HaveCount(4);
        next.Tallies.Should().OnlyContain(t => t == 0);
    }

    [Fact]
    public void An_answer_is_counted_once_choices_are_offered()
    {
        var state = GameReducer.Initial("dev");
        (state, var offerError) = GameReducer.Reduce(state, new QuestionOffered("Which song?", ["a", "b", "c", "d"])
        {
            Text = "Which song?",
            Choices = ["a", "b", "c", "d"],
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        offerError.Should().BeNull();

        // AnswerSubmitted is only aggregated while Guessing; drive the phase there first, the
        // same way GameReducerTests does for every other AnswerSubmitted assertion.
        (state, var phaseError) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lobby, Phase.Guessing)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Guessing,
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        phaseError.Should().BeNull();

        var (next, error) = GameReducer.Reduce(state, new AnswerSubmitted("aud-1", 2)
        {
            AudienceId = "aud-1",
            ChoiceIndex = 2,
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        error.Should().BeNull();
        next.Tallies[2].Should().Be(1);
    }
}
