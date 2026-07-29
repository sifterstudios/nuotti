using Nuotti.Backend.Commands;
using Nuotti.Backend.Tests.TestSupport;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Reducer;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;
namespace Nuotti.Backend.Tests;

/// <summary>
/// End-to-end through the real processor: nothing populated GameStateSnapshot.Choices before
/// this, so every AnswerSubmitted failed its bounds check against an empty Choices and no tally
/// ever moved. See docs/adr/0002's amendment and CONTEXT.md's Choices entry.
/// </summary>
public class QuestionPushedEffectsTests
{
    const string Session = "S1";

    static Actor Performer => Actor.Verified(Role.Performer, "perf-1");
    static Actor Audience => Actor.Verified(Role.Audience, "aud-1");

    [Fact]
    public async Task QuestionPushed_populates_choices_and_a_subsequent_answer_is_tallied()
    {
        var processor = Harness.Processor(out var store, out var bus);
        store.Set(Session, GameReducer.Initial(Session) with { Phase = PhaseEnum.Guessing });

        var pushResult = await processor.ApplyAsync(Session, Performer,
            new QuestionPushed("Which song?", ["a", "b", "c"])
            {
                SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = "perf-1"
            });

        Assert.Equal(Outcome.Applied, pushResult.Outcome);
        Assert.True(store.TryGet(Session, out var afterPush));
        Assert.Equal(new[] { "a", "b", "c" }, afterPush.Choices);
        Assert.Equal(new[] { 0, 0, 0 }, afterPush.Tallies);

        // The relay still reaches the wire untouched, alongside the new state event.
        Assert.Single(bus.Published.OfType<QuestionPushed>());
        Assert.Single(bus.Published.OfType<QuestionOffered>());

        var answerResult = await processor.ApplyAsync(Session, Audience,
            new SubmitAnswer(null, 1) { SessionCode = Session, IssuedByRole = Role.Audience, IssuedById = "aud-1" });

        Assert.Equal(Outcome.Applied, answerResult.Outcome);
        Assert.Equal(1, answerResult.State!.Tallies[1]);
    }

    [Fact]
    public async Task QuestionPushed_stays_at_least_once_like_the_other_relays()
    {
        // docs/adr/0002's amendment: QuestionPushed now changes state, but the idempotency
        // decision is unchanged — re-offering the same choices is idempotent in effect.
        var idempotency = Harness.IdempotencyStore();
        var processor = Harness.Processor(out _, out var bus, idempotency);
        var cmd = new QuestionPushed("Which song?", ["a", "b"])
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = "perf-1"
        };

        var first = await processor.ApplyAsync(Session, Performer, cmd);
        var second = await processor.ApplyAsync(Session, Performer, cmd);

        Assert.Equal(Outcome.Applied, first.Outcome);
        Assert.Equal(Outcome.Applied, second.Outcome);
        Assert.Equal(2, bus.Published.OfType<QuestionPushed>().Count());
        Assert.Equal(2, bus.Published.OfType<QuestionOffered>().Count());
    }
}
