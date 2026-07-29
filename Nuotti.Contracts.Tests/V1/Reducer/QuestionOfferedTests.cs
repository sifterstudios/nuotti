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

    [Fact]
    public void Re_offering_the_same_choices_does_not_wipe_the_tallies()
    {
        // QuestionPushed skips idempotency by design (docs/adr/0002), so a client re-sending
        // after a dropped connection reaches the reducer as a second QuestionOffered for the
        // same question. It must not cost the audience their votes.
        var state = GameReducer.Initial("dev");
        var offer = new QuestionOffered("Which song?", ["a", "b", "c", "d"])
        {
            Text = "Which song?",
            Choices = ["a", "b", "c", "d"],
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        };

        (state, var offerError) = GameReducer.Reduce(state, offer);
        offerError.Should().BeNull();

        (state, var phaseError) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lobby, Phase.Guessing)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Guessing,
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        phaseError.Should().BeNull();

        (state, var answerError) = GameReducer.Reduce(state, new AnswerSubmitted("aud-1", 2)
        {
            AudienceId = "aud-1",
            ChoiceIndex = 2,
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        answerError.Should().BeNull();
        state.Tallies[2].Should().Be(1);

        // The duplicate: the same QuestionPushed relay re-arrives and produces an identical
        // QuestionOffered again.
        var (next, error) = GameReducer.Reduce(state, new QuestionOffered("Which song?", ["a", "b", "c", "d"])
        {
            Text = "Which song?",
            Choices = ["a", "b", "c", "d"],
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        error.Should().BeNull();
        next.Tallies[2].Should().Be(1);
        next.Tallies.Should().Equal(state.Tallies);
    }

    [Fact]
    public void Offering_different_choices_replaces_them_and_rezeroes()
    {
        // The other half of the idempotency fix: a genuinely new question must still reset the
        // tally, or stale votes from the previous question would carry over onto the new one.
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

        (state, var phaseError) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lobby, Phase.Guessing)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Guessing,
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        phaseError.Should().BeNull();

        (state, var answerError) = GameReducer.Reduce(state, new AnswerSubmitted("aud-1", 2)
        {
            AudienceId = "aud-1",
            ChoiceIndex = 2,
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        answerError.Should().BeNull();
        state.Tallies[2].Should().Be(1);

        // A genuinely new question, with a different set of choices.
        var (next, error) = GameReducer.Reduce(state, new QuestionOffered("Next song?", ["x", "y"])
        {
            Text = "Next song?",
            Choices = ["x", "y"],
            SessionCode = "dev",
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        error.Should().BeNull();
        next.Choices.Should().Equal("x", "y");
        next.Tallies.Should().Equal(0, 0);
    }
}
