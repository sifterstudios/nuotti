using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class HubSubscriptionAsyncTests
{
    /// <summary>
    /// A publisher that awaits each handler, the way a deterministic in-process bus will.
    /// </summary>
    sealed class AwaitingHubClient : IHubClient
    {
        Func<GameStateSnapshot, Task>? _handler;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler)
        {
            _handler = handler;
            return new Sub(this);
        }

        public Task PublishAsync(GameStateSnapshot snapshot) => _handler?.Invoke(snapshot) ?? Task.CompletedTask;

        sealed class Sub(AwaitingHubClient owner) : IDisposable
        {
            public void Dispose() => owner._handler = null;
        }
    }

    static GameStateSnapshot ASnapshot() => new(sessionCode: "dev", phase: Phase.Lobby, songIndex: 0);

    [Fact]
    public async Task Publisher_can_await_an_async_handler_to_completion()
    {
        var client = new AwaitingHubClient();
        var finished = false;

        using var sub = client.OnGameStateChanged(async _ =>
        {
            await Task.Yield();
            finished = true;
        });

        await client.PublishAsync(ASnapshot());

        // With Action<T> this assertion raced: the publisher could not await the handler.
        finished.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_exceptions_reach_the_publisher_instead_of_vanishing()
    {
        var client = new AwaitingHubClient();
        using var sub = client.OnGameStateChanged(async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        });

        var act = async () => await client.PublishAsync(ASnapshot());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}
