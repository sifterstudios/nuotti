using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Script;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class ScriptMappingTests
{
    static PerformerActor CreateActor()
    {
        var factory = new FakeHubClientFactory();
        return new PerformerActor(factory, new Uri("http://localhost:5000"), "SESS01");
    }

    [Fact]
    public void StartSet_maps_to_StartGame()
    {
        var actor = CreateActor();
        var script = new ScriptModel
        {
            Steps = [ new ScriptStep { Kind = StepKind.StartSet } ]
        };

        var cmd = actor.BuildCommandsFromScript(script).Single();

        var sg = Assert.IsType<StartGame>(cmd);
        Assert.Equal("SESS01", sg.SessionCode);
        Assert.Equal(Role.Performer, sg.IssuedByRole);
        Assert.False(sg.CommandId == Guid.Empty);
    }

    [Fact]
    public void NextSong_maps_to_NextRound_with_SongId()
    {
        var actor = CreateActor();
        var script = new ScriptModel
        {
            Steps = [ new ScriptStep { Kind = StepKind.NextSong, SongId = "song-1" } ]
        };

        var cmd = actor.BuildCommandsFromScript(script).Single();

        var nr = Assert.IsType<NextRound>(cmd);
        Assert.Equal(new SongId("song-1"), nr.SongId);
        Assert.Equal("SESS01", nr.SessionCode);
    }

    [Fact]
    public void GiveHint_maps_to_GiveHint_with_payload()
    {
        var actor = CreateActor();
        var script = new ScriptModel
        {
            Steps = [ new ScriptStep { Kind = StepKind.GiveHint, SongId = "song-2", HintIndex = 1, HintText = "lyric", PerformerInstructions = "whistle" } ]
        };

        var cmd = actor.BuildCommandsFromScript(script).Single();

        var gh = Assert.IsType<GiveHint>(cmd);
        Assert.Equal(1, gh.Hint.Index);
        Assert.Equal("lyric", gh.Hint.Text);
        Assert.Equal("whistle", gh.Hint.PerformerInstructions);
        Assert.Equal(new SongId("song-2"), gh.Hint.SongId);
        Assert.Equal("SESS01", gh.SessionCode);
    }

    [Fact]
    public void LockAnswers_maps_to_LockAnswers()
    {
        var actor = CreateActor();
        var script = new ScriptModel
        {
            Steps = [ new ScriptStep { Kind = StepKind.LockAnswers } ]
        };

        var cmd = actor.BuildCommandsFromScript(script).Single();
        var la = Assert.IsType<LockAnswers>(cmd);
        Assert.Equal("SESS01", la.SessionCode);
    }

    [Fact]
    public void RevealAnswer_maps_to_RevealAnswer_with_SongRef()
    {
        var actor = CreateActor();
        var script = new ScriptModel
        {
            Steps = [ new ScriptStep { Kind = StepKind.RevealAnswer, SongId = "song-3", Title = "Title", Artist = "Artist" } ]
        };

        var cmd = actor.BuildCommandsFromScript(script).Single();
        var ra = Assert.IsType<RevealAnswer>(cmd);
        Assert.Equal(new SongRef(new SongId("song-3"), "Title", "Artist"), ra.SongRef);
        Assert.Equal("SESS01", ra.SessionCode);
    }

    [Fact]
    public void EndSong_maps_to_EndSong_with_SongId()
    {
        var actor = CreateActor();
        var script = new ScriptModel
        {
            Steps = [ new ScriptStep { Kind = StepKind.EndSong, SongId = "song-4" } ]
        };

        var cmd = actor.BuildCommandsFromScript(script).Single();
        var es = Assert.IsType<EndSong>(cmd);
        Assert.Equal(new SongId("song-4"), es.SongId);
        Assert.Equal("SESS01", es.SessionCode);
    }

    [Fact]
    public void Play_maps_to_PlaySong_with_SongId()
    {
        var actor = CreateActor();
        var script = new ScriptModel
        {
            Steps = [ new ScriptStep { Kind = StepKind.Play, SongId = "song-5" } ]
        };

        var cmd = actor.BuildCommandsFromScript(script).Single();
        var ps = Assert.IsType<PlaySong>(cmd);
        Assert.Equal(new SongId("song-5"), ps.SongId);
        Assert.Equal("SESS01", ps.SessionCode);
    }

    [Fact]
    public void Stop_maps_to_EndSong_with_SongId()
    {
        var actor = CreateActor();
        var script = new ScriptModel
        {
            Steps = [ new ScriptStep { Kind = StepKind.Stop, SongId = "song-6" } ]
        };

        var cmd = actor.BuildCommandsFromScript(script).Single();
        var es = Assert.IsType<EndSong>(cmd);
        Assert.Equal(new SongId("song-6"), es.SongId);
        Assert.Equal("SESS01", es.SessionCode);
    }
}

file sealed class FakeHubClientFactory : IHubClientFactory
{
    public FakeHubClient? Client { get; private set; }
    public IHubClient Create(Uri baseAddress)
    {
        Client = new FakeHubClient(baseAddress);
        return Client;
    }
}

file sealed class FakeHubClient : IHubClient
{
    public Uri BaseAddress { get; }
    public List<(string Session, string Role, string? Name)> Calls { get; } = new();

    public FakeHubClient(Uri baseAddress)
    {
        BaseAddress = baseAddress;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((session, role, name));
        return Task.CompletedTask;
    }

    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler)
        => new NoopDisposable();

    sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}