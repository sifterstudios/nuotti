using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.InProc.Tests;

public class InProcHubClientTests
{
    static readonly Uri Unused = new("http://in-proc");

    static async Task<InProcBackend> ASessionAsync(string session = "dev")
    {
        var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor);
        await emitter.EmitAsync(new CreateSession(session)
        {
            SessionCode = session, IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });
        return backend;
    }

    [Fact]
    public async Task Delivers_the_bare_snapshot_not_the_event_envelope()
    {
        using var backend = await ASessionAsync();
        var factory = new InProcHubClientFactory(backend, "dev");
        var client = factory.Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "projector");

        var received = new List<GameStateSnapshot>();
        using var sub = client.On<GameStateSnapshot>(s => { received.Add(s); return Task.CompletedTask; });

        var emitter = new InProcCommandEmitter(backend.Processor);
        await emitter.EmitAsync(new StartGame
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });

        received.Should().NotBeEmpty();
        received[^1].Phase.Should().Be(Phase.Start);
    }

    [Fact]
    public async Task Does_not_deliver_messages_from_another_session()
    {
        using var backend = await ASessionAsync("dev");
        var emitterOther = new InProcCommandEmitter(backend.Processor);
        await emitterOther.EmitAsync(new CreateSession("other")
        {
            SessionCode = "other", IssuedByRole = Role.Performer, IssuedById = "perf-2"
        });

        var factory = new InProcHubClientFactory(backend, "dev");
        var client = factory.Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "projector");

        var received = new List<GameStateSnapshot>();
        using var sub = client.On<GameStateSnapshot>(s => { received.Add(s); return Task.CompletedTask; });

        await emitterOther.EmitAsync(new StartGame
        {
            SessionCode = "other", IssuedByRole = Role.Performer, IssuedById = "perf-2"
        });

        // The real hub sends to Clients.Group(session); the in-proc client must filter the
        // same way or lanes in different sessions cross-talk.
        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Stops_delivering_after_the_subscription_is_disposed()
    {
        using var backend = await ASessionAsync();
        var factory = new InProcHubClientFactory(backend, "dev");
        var client = factory.Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "projector");

        var received = new List<GameStateSnapshot>();
        var sub = client.On<GameStateSnapshot>(s => { received.Add(s); return Task.CompletedTask; });
        sub.Dispose();

        var emitter = new InProcCommandEmitter(backend.Processor);
        await emitter.EmitAsync(new StartGame
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Delivers_a_relay_command_to_its_payload_subscribers()
    {
        using var backend = await ASessionAsync();
        var factory = new InProcHubClientFactory(backend, "dev");
        var client = factory.Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "engine");

        var plays = new List<PlayTrack>();
        using var sub = client.On<PlayTrack>(p => { plays.Add(p); return Task.CompletedTask; });

        await backend.Bus.PublishAsync(new PlayTrack("file:///song.mp3")
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        }, CancellationToken.None);

        plays.Should().HaveCount(1);
    }

    [Fact]
    public async Task Delivers_nothing_before_the_client_has_started()
    {
        using var backend = await ASessionAsync();
        var client = new InProcHubClientFactory(backend, "dev").Create(Unused);
        // deliberately no StartAsync

        var received = new List<GameStateSnapshot>();
        using var sub = client.On<GameStateSnapshot>(s => { received.Add(s); return Task.CompletedTask; });

        await new InProcCommandEmitter(backend.Processor).EmitAsync(new StartGame
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Stops_delivering_after_the_client_stops()
    {
        using var backend = await ASessionAsync();
        var client = new InProcHubClientFactory(backend, "dev").Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "projector");

        var received = new List<GameStateSnapshot>();
        using var sub = client.On<GameStateSnapshot>(s => { received.Add(s); return Task.CompletedTask; });

        await client.StopAsync();

        await new InProcCommandEmitter(backend.Processor).EmitAsync(new StartGame
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });

        // Subscribed while started, stopped afterwards: proves the gate is read at delivery
        // time, not captured when the subscription was created.
        received.Should().BeEmpty();
    }

    [Fact]
    public async Task An_audience_can_submit_an_answer_through_the_processor()
    {
        using var backend = await ASessionAsync();
        var client = new InProcHubClientFactory(backend, "dev").Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "audience", "aud-1");

        // Drive the session to Guessing phase: Lobby -> Start -> Guessing
        var emitter = new InProcCommandEmitter(backend.Processor);
        await emitter.EmitAsync(new StartGame
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });
        await emitter.EmitAsync(new OpenAnswers
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });

        var act = async () => await client.SubmitAnswerAsync("dev", 0);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_non_audience_cannot_submit_an_answer()
    {
        using var backend = await ASessionAsync();
        var client = new InProcHubClientFactory(backend, "dev").Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "projector");

        var act = async () => await client.SubmitAnswerAsync("dev", 0);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
