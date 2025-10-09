using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Performer.Shared;
using System.Net;
using Xunit;
namespace Nuotti.Performer.Tests;

public class LiveHeaderEnginePanelTests : TestContext
{
    sealed class FakeHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? OnSendAsync;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => OnSendAsync is not null ? OnSendAsync(request, cancellationToken) : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    sealed class FactoryFromHandler : IHttpClientFactory
    {
        readonly HttpMessageHandler _handler;
        public FactoryFromHandler(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
    }

    [Fact]
    public async Task Engine_status_changes_reflect_in_UI()
    {
        var handler = new FakeHandler
        {
            OnSendAsync = (req, ct) =>
            {
                // Return counts with engine=1
                var json = "{\"performer\":1,\"projector\":1,\"engine\":1,\"audiences\":0}";
                var msg = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                };
                return Task.FromResult(msg);
            }
        };
        var factory = new FactoryFromHandler(handler);
        Services.AddSingleton<IHttpClientFactory>(factory);
        var state = new PerformerUiState(factory);
        Services.AddSingleton(state);

        // Render header
        var cut = RenderComponent<LiveHeader>();

        // Set session and pull counts so EngineCount becomes 1
        state.SetSession("dev", new Uri("http://localhost"));
        await state.RefreshCountsAsync();

        // Update phase to Play to show Playing
        var snapshot = new GameStateSnapshot("dev", phase: Phase.Play, songIndex: 0, currentSong: null, catalog: Array.Empty<SongRef>(), choices: Array.Empty<string>(), hintIndex: 0, tallies: Array.Empty<int>(), scores: null, songStartedAtUtc: null);
        state.UpdateGameState(snapshot);

        Assert.Contains("Engine status: <strong>Playing</strong>", cut.Markup);

        // Change to non-play phase to show Ready
        var snapshot2 = snapshot with { Phase = Phase.Idle };
        state.UpdateGameState(snapshot2);
        Assert.Contains("Engine status: <strong>Ready</strong>", cut.Markup);
    }

    [Fact]
    public async Task Ping_timeout_shows_warning()
    {
        var handler = new FakeHandler
        {
            OnSendAsync = (req, ct) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timed out"))
        };
        var factory = new FactoryFromHandler(handler);
        Services.AddSingleton<IHttpClientFactory>(factory);
        var state = new PerformerUiState(factory);
        state.SetSession("dev", new Uri("http://localhost"));
        Services.AddSingleton(state);

        var cut = RenderComponent<LiveHeader>();

        // Click Ping
        cut.Find("button").Click();

        // Allow async to settle
        await Task.Delay(10);

        Assert.Contains("Ping timeout", cut.Markup);
        Assert.Contains("Engine status: <strong>Error</strong>", cut.Markup);
    }
}
