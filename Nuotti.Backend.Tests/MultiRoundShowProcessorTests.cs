using Microsoft.Extensions.Logging.Abstractions;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Retention;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Tests.TestSupport;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;
using Nuotti.Contracts.V1.Reducer;
using Xunit;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;

namespace Nuotti.Backend.Tests;

/// <summary>
/// Issue #260 — multi-Round show at the processor seam: NextRound advances SongIndex,
/// only Performers advance the flow, and EndGame retains identifiable scores for 30 days.
/// </summary>
public class MultiRoundShowProcessorTests
{
    const string Session = "MULTI1";

    static Actor Performer => Actor.Verified(Role.Performer, "perf-1");
    static Actor Audience => Actor.Verified(Role.Audience, "aud-1");

    static SongRef Song(string id, string title) => new(new SongId(id), title, "Artist");

    static void SeededIntermission(IGameStateStore store, IReadOnlyList<SongRef> catalog, int songIndex = 0)
    {
        store.Set(Session, GameReducer.Initial(Session) with
        {
            Phase = PhaseEnum.Intermission,
            Catalog = catalog,
            SongIndex = songIndex,
            CurrentSong = catalog[songIndex],
            Scores = new Dictionary<string, int> { ["aud-1"] = 10 }
        });
    }

    static SessionCommandProcessor CreateProcessor(
        out IGameStateStore store,
        out CapturingEventBus bus,
        ISessionResultsStore? results = null)
    {
        store = new InMemoryGameStateStore();
        bus = new CapturingEventBus();
        return new SessionCommandProcessor(
            store,
            Harness.IdempotencyStore(),
            bus,
            NullLogger<SessionCommandProcessor>.Instance,
            results: results);
    }

    [Fact]
    public async Task NextRound_from_Intermission_advances_SongIndex_and_CurrentSong()
    {
        var processor = CreateProcessor(out var store, out var bus);
        var catalog = new[] { Song("s1", "One"), Song("s2", "Two") };
        SeededIntermission(store, catalog, songIndex: 0);

        var result = await processor.ApplyAsync(Session, Performer,
            new NextRound(new SongId("s2"))
            {
                SessionCode = Session,
                IssuedByRole = Role.Performer,
                IssuedById = "perf-1"
            });

        Assert.Equal(Outcome.Applied, result.Outcome);
        Assert.True(store.TryGet(Session, out var next));
        Assert.Equal(PhaseEnum.Start, next!.Phase);
        Assert.Equal(1, next.SongIndex);
        Assert.Equal("s2", next.CurrentSong!.Id.Value);
        Assert.Equal("Two", next.CurrentSong.Title);
        Assert.Contains(bus.Published.OfType<CurrentSongSet>(), e => e.SongIndex == 1);
    }

    [Fact]
    public async Task Audience_cannot_advance_NextRound_or_EndGame()
    {
        var processor = CreateProcessor(out var store, out var bus);
        var catalog = new[] { Song("s1", "One"), Song("s2", "Two") };
        SeededIntermission(store, catalog);

        var next = await processor.ApplyAsync(Session, Audience,
            new NextRound(new SongId("s2"))
            {
                SessionCode = Session,
                IssuedByRole = Role.Audience,
                IssuedById = "aud-1"
            });
        Assert.Equal(Outcome.Rejected, next.Outcome);
        Assert.Equal(403, next.Problem!.Status);

        var end = await processor.ApplyAsync(Session, Audience,
            new EndGame
            {
                SessionCode = Session,
                IssuedByRole = Role.Audience,
                IssuedById = "aud-1"
            });
        Assert.Equal(Outcome.Rejected, end.Outcome);
        Assert.Equal(403, end.Problem!.Status);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task EndGame_moves_to_Finished_and_retains_identifiable_scores_for_30_days()
    {
        var results = new InMemorySessionResultsStore();
        var processor = CreateProcessor(out var store, out _, results);
        var catalog = new[] { Song("s1", "One"), Song("s2", "Two") };
        SeededIntermission(store, catalog, songIndex: 1);

        var cmd = new EndGame
        {
            SessionCode = Session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-1"
        };
        var result = await processor.ApplyAsync(Session, Performer, cmd, workspaceId: "ws-1");

        Assert.Equal(Outcome.Applied, result.Outcome);
        Assert.True(store.TryGet(Session, out var finished));
        Assert.Equal(PhaseEnum.Finished, finished!.Phase);

        var retained = await results.GetAsync("ws-1", Session);
        Assert.NotNull(retained);
        Assert.Equal(10, retained!.Scores["aud-1"]);
        Assert.Equal(cmd.CommandId, retained.CausingCommandId);
        Assert.True(retained.SongCount >= 2);

        Assert.Equal(0, await results.PruneExpiredAsync(DateTimeOffset.UtcNow.AddDays(29)));
        Assert.Equal(1, await results.PruneExpiredAsync(DateTimeOffset.UtcNow.AddDays(31)));
        Assert.Null(await results.GetAsync("ws-1", Session));
    }
}
