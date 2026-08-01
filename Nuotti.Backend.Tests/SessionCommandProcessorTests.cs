using Nuotti.Backend.Commands;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Tests.TestSupport;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;
using Nuotti.Contracts.V1.Reducer;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;
namespace Nuotti.Backend.Tests;

/// <summary>
/// The processor's interface is its test surface: no WebApplicationFactory, no SignalR, no hub
/// double. Every assertion here previously required booting a host.
/// </summary>
public class SessionCommandProcessorTests
{
    const string Session = "S1";

    static Actor Performer => Actor.Verified(Role.Performer, "perf-1");
    static Actor Audience => Actor.Verified(Role.Audience, "aud-1");

    static StartGame Start() => new()
    {
        SessionCode = Session,
        IssuedByRole = Role.Performer,
        IssuedById = "perf-1"
    };

    static void SeedPhase(IGameStateStore store, PhaseEnum phase)
        => store.Set(Session, GameReducer.Initial(Session) with { Phase = phase });

    [Fact]
    public async Task Wrong_role_is_rejected_as_403_without_touching_state()
    {
        var processor = Harness.Processor(out var store, out var bus);

        var result = await processor.ApplyAsync(Session, Audience, Start());

        Assert.Equal(Outcome.Rejected, result.Outcome);
        Assert.Equal(403, result.Problem!.Status);
        Assert.Equal(ReasonCode.UnauthorizedRole, result.Problem.Reason);
        Assert.Equal("issuedByRole", result.Problem.Field);
        Assert.Empty(bus.Published);
        Assert.False(store.TryGet(Session, out _));
    }

    [Fact]
    public async Task Audience_may_submit_an_answer_but_a_performer_may_not()
    {
        var processor = Harness.Processor(out var store, out _);
        SeedPhase(store, PhaseEnum.Guessing);

        var rejected = await processor.ApplyAsync(Session, Performer,
            new SubmitAnswer(null, 1) { SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = "perf-1" });

        Assert.Equal(Outcome.Rejected, rejected.Outcome);
        Assert.Equal(ReasonCode.UnauthorizedRole, rejected.Problem!.Reason);
    }

    [Fact]
    public async Task Illegal_phase_transition_is_rejected_as_409()
    {
        var processor = Harness.Processor(out var store, out var bus);
        // StartGame is allowed from Lobby or Finished; Guessing is neither.
        SeedPhase(store, PhaseEnum.Guessing);

        var result = await processor.ApplyAsync(Session, Performer, Start());

        Assert.Equal(Outcome.Rejected, result.Outcome);
        Assert.Equal(409, result.Problem!.Status);
        Assert.Equal(ReasonCode.InvalidStateTransition, result.Problem.Reason);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task Applied_phase_change_stores_the_new_state_and_publishes_both_events()
    {
        var processor = Harness.Processor(out var store, out var bus);

        var result = await processor.ApplyAsync(Session, Performer, Start());

        Assert.Equal(Outcome.Applied, result.Outcome);
        Assert.Equal(PhaseEnum.Start, result.State!.Phase);
        Assert.True(store.TryGet(Session, out var stored));
        Assert.Equal(PhaseEnum.Start, stored.Phase);

        Assert.Single(bus.Published.OfType<GamePhaseChanged>());
        var broadcast = Assert.Single(bus.Published.OfType<GameStateChanged>());
        Assert.Equal(PhaseEnum.Start, broadcast.Snapshot.Phase);
    }

    [Fact]
    public async Task Same_command_id_twice_is_a_duplicate_and_applies_once()
    {
        var idempotency = Harness.IdempotencyStore();
        var processor = Harness.Processor(out _, out var bus, idempotency);
        var cmd = Start();

        var first = await processor.ApplyAsync(Session, Performer, cmd);
        var second = await processor.ApplyAsync(Session, Performer, cmd);

        Assert.Equal(Outcome.Applied, first.Outcome);
        Assert.Equal(Outcome.Duplicate, second.Outcome);
        Assert.Single(bus.Published.OfType<GamePhaseChanged>());
    }

    [Fact]
    public async Task GiveHint_goes_through_the_reducer_and_emits_HintGiven()
    {
        var processor = Harness.Processor(out var store, out var bus);
        SeedPhase(store, PhaseEnum.Guessing);

        var result = await processor.ApplyAsync(Session, Performer,
            new GiveHint(new Hint(0, "a hint", null, new SongId("song-1")))
            {
                SessionCode = Session,
                IssuedByRole = Role.Performer,
                IssuedById = "perf-1"
            });

        Assert.Equal(Outcome.Applied, result.Outcome);
        Assert.Equal(1, result.State!.HintIndex);
        var hint = Assert.Single(bus.Published.OfType<HintGiven>());
        Assert.Equal(1, hint.HintIndex);
    }

    [Fact]
    public async Task GiveHint_outside_Guessing_is_rejected()
    {
        var processor = Harness.Processor(out var store, out _);
        SeedPhase(store, PhaseEnum.Lobby);

        var result = await processor.ApplyAsync(Session, Performer,
            new GiveHint(new Hint(0, "a hint", null, new SongId("song-1")))
            {
                SessionCode = Session,
                IssuedByRole = Role.Performer,
                IssuedById = "perf-1"
            });

        Assert.Equal(Outcome.Rejected, result.Outcome);
        Assert.Equal(ReasonCode.InvalidStateTransition, result.Problem!.Reason);
    }

    [Fact]
    public async Task RevealAnswer_emits_two_events_and_awards_a_point_to_a_correct_answer()
    {
        var processor = Harness.Processor(out var store, out var bus);
        store.Set(Session, GameReducer.Initial(Session) with
        {
            Phase = PhaseEnum.Guessing,
            Choices = ["A", "B"],
            Tallies = [0, 0]
        });

        await processor.ApplyAsync(Session, Audience,
            new SubmitAnswer(null, 1) { SessionCode = Session, IssuedByRole = Role.Audience, IssuedById = "aud-1" });

        store.TryGet(Session, out var afterAnswer);
        store.Set(Session, afterAnswer with { Phase = PhaseEnum.Lock });

        var result = await processor.ApplyAsync(Session, Performer,
            new RevealAnswer(new SongRef(new SongId("song-1"), "T", "A"), 1)
            {
                SessionCode = Session,
                IssuedByRole = Role.Performer,
                IssuedById = "perf-1"
            });

        Assert.Equal(Outcome.Applied, result.Outcome);
        Assert.Equal(PhaseEnum.Reveal, result.State!.Phase);
        Assert.Single(bus.Published.OfType<CorrectAnswerRevealed>());
        Assert.Equal(1, result.State.Scores["aud-1"]);
    }

    [Fact]
    public async Task An_answer_updates_tallies_but_does_not_broadcast_a_snapshot()
    {
        var processor = Harness.Processor(out var store, out var bus);
        store.Set(Session, GameReducer.Initial(Session) with
        {
            Phase = PhaseEnum.Guessing,
            Choices = ["A", "B"],
            Tallies = [0, 0]
        });

        var result = await processor.ApplyAsync(Session, Audience,
            new SubmitAnswer(null, 0) { SessionCode = Session, IssuedByRole = Role.Audience, IssuedById = "aud-1" });

        Assert.Equal(Outcome.Applied, result.Outcome);
        Assert.Equal(1, result.State!.Tallies[0]);

        // A snapshot per answer would be quadratic in audience size; clients reduce the event.
        Assert.Single(bus.Published.OfType<AnswerSubmitted>());
        Assert.Empty(bus.Published.OfType<GameStateChanged>());
    }

    [Fact]
    public async Task Create_session_seeds_state_and_then_refuses_to_wipe_it()
    {
        var processor = Harness.Processor(out var store, out _);

        var created = await processor.ApplyAsync(Session, Performer, NewCreate());
        Assert.Equal(Outcome.Applied, created.Outcome);
        Assert.Equal(PhaseEnum.Lobby, created.State!.Phase);

        // Get the session into a state worth protecting.
        store.TryGet(Session, out var live);
        store.Set(Session, live with { Phase = PhaseEnum.Guessing, Scores = new Dictionary<string, int> { ["aud-1"] = 7 } });

        var again = await processor.ApplyAsync(Session, Performer, NewCreate());

        Assert.Equal(Outcome.Rejected, again.Outcome);
        Assert.Equal(409, again.Problem!.Status);
        store.TryGet(Session, out var after);
        Assert.Equal(PhaseEnum.Guessing, after.Phase);
        Assert.Equal(7, after.Scores["aud-1"]);

        static CreateSession NewCreate() => new(Session)
        {
            SessionCode = Session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-1"
        };
    }

    [Fact]
    public async Task Relay_commands_are_published_untouched_and_change_no_state()
    {
        var processor = Harness.Processor(out var store, out var bus);

        var result = await processor.ApplyAsync(Session, Performer,
            new PlayTrack("https://example.test/a.mp3")
            {
                SessionCode = Session,
                IssuedByRole = Role.Performer,
                IssuedById = "perf-1"
            });

        Assert.Equal(Outcome.Applied, result.Outcome);
        Assert.Null(result.State);
        Assert.Single(bus.Published.OfType<PlayTrack>());
        Assert.Empty(bus.Published.OfType<GameStateChanged>());
    }

    [Fact]
    public async Task Relay_commands_stay_at_least_once()
    {
        // docs/adr/0002: a swallowed play retry is worse than a repeated one.
        var idempotency = Harness.IdempotencyStore();
        var processor = Harness.Processor(out _, out var bus, idempotency);
        var cmd = new PlayTrack("https://example.test/a.mp3")
        {
            SessionCode = Session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-1"
        };

        var first = await processor.ApplyAsync(Session, Performer, cmd);
        var second = await processor.ApplyAsync(Session, Performer, cmd);

        Assert.Equal(Outcome.Applied, first.Outcome);
        Assert.Equal(Outcome.Applied, second.Outcome);
        Assert.Equal(2, bus.Published.OfType<PlayTrack>().Count());
    }

    [Fact]
    public async Task A_manifest_replaces_the_catalog_through_the_reducer()
    {
        var processor = Harness.Processor(out _, out var bus);
        var manifest = new SetlistManifest
        {
            Songs =
            [
                new SetlistManifest.SongEntry { Title = "One", Artist = "A", File = "https://example.test/1.mp3" },
                new SetlistManifest.SongEntry { Title = "Two", Artist = "B", File = "https://example.test/2.mp3" }
            ]
        };

        var result = await processor.ApplyAsync(Session, Performer, new UpdateCatalog(manifest)
        {
            SessionCode = Session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-1"
        });

        Assert.Equal(Outcome.Applied, result.Outcome);
        Assert.Equal(2, result.State!.Catalog.Count);
        Assert.Equal("One", result.State.Catalog[0].Title);
        Assert.Single(bus.Published.OfType<CatalogUpdated>());
    }

    [Fact]
    public async Task An_empty_manifest_is_rejected_as_422()
    {
        var processor = Harness.Processor(out _, out var bus);

        var result = await processor.ApplyAsync(Session, Performer,
            new UpdateCatalog(new SetlistManifest())
            {
                SessionCode = Session,
                IssuedByRole = Role.Performer,
                IssuedById = "perf-1"
            });

        Assert.Equal(Outcome.Rejected, result.Outcome);
        Assert.Equal(422, result.Problem!.Status);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task Every_published_event_carries_the_supplied_correlation_id()
    {
        var processor = Harness.Processor(out _, out var bus);
        var correlation = Guid.NewGuid();

        await processor.ApplyAsync(Session, Performer, Start(), correlation);

        Assert.All(bus.Published.OfType<EventBase>(),
            evt => Assert.Equal(correlation, evt.CorrelationId));
    }

    [Fact]
    public async Task Correlation_defaults_to_the_command_id()
    {
        var processor = Harness.Processor(out _, out var bus);
        var cmd = Start();

        await processor.ApplyAsync(Session, Performer, cmd);

        Assert.All(bus.Published.OfType<EventBase>(),
            evt => Assert.Equal(cmd.CommandId, evt.CorrelationId));
    }
}
