using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Performer.Services;
using Nuotti.Performer.Shared;
using System.Net;
using Xunit;
namespace Nuotti.Performer.Tests;

public class ControlPanelFlowTests : MudTestContext
{
    sealed class CapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? OnSendAsync;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (OnSendAsync is not null) return OnSendAsync(request, cancellationToken);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }

    sealed class FactoryFromHandler : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FactoryFromHandler(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
    }

    [Fact]
    public void E2E_cannot_send_two_next_song_commands_rapidly()
    {
        var handler = new CapturingHandler
        {
            OnSendAsync = (req, ct) =>
            {
                // respond 200 with counts for GET counts
                if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/api/sessions/") && req.RequestUri!.AbsolutePath.EndsWith("/counts"))
                {
                    var json = new StringContent("{\"performer\":1,\"projector\":1,\"engine\":1,\"audiences\":0}");
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = json });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
            }
        };
        var httpClient = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton<HttpClient>(httpClient);
        Services.AddSingleton<IHttpClientFactory>(new FactoryFromHandler(handler));

        // DI registrations used by ControlPanel
        var state = new PerformerUiState(new FactoryFromHandler(handler));
        state.SetSession("dev", new Uri("http://localhost"));
        // Ensure engine count > 0
        state.RefreshCountsAsync().GetAwaiter().GetResult();
        // Put UI in Guessing phase with a current song to enable Next Song
        state.UpdateGameState(new GameStateSnapshot(
            sessionCode: "dev",
            phase: Phase.Guessing,
            songIndex: 1,
            currentSong: new SongRef(new SongId("song-1"), "Title", "Artist"),
            catalog: Array.Empty<SongRef>(),
            choices: Array.Empty<string>(),
            hintIndex: 0,
            tallies: Array.Empty<int>(),
            scores: new Dictionary<string, int>(),
            songStartedAtUtc: null
        ));
        Services.AddSingleton(state);
        Services.AddSingleton(new CommandHistoryService());
        Services.AddSingleton(new OfflineCommandQueue());
        Services.AddScoped<PerformerCommands>();

        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<ControlPanel>();

        // Act: two rapid Shift-clicks on Next Song (bypass confirm)
        var nextBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Next Song"));
        nextBtn.Click(new MouseEventArgs { ShiftKey = true });
        // re-query to avoid stale element after potential re-render
        nextBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Next Song"));
        nextBtn.Click(new MouseEventArgs { ShiftKey = true });

        // Assert: only one POST to next-round
        var posts = handler.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        var nextPosts = posts.Where(r => r.RequestUri!.AbsolutePath.Contains("/v1/message/phase/next-round/")).ToList();
        Assert.Equal(1, nextPosts.Count);
    }

    [Fact]
    public async Task UI_cancel_stops_pending_end_song()
    {
        var handler = new CapturingHandler
        {
            OnSendAsync = (req, ct) =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/api/sessions/") && req.RequestUri!.AbsolutePath.EndsWith("/counts"))
                {
                    var json = new StringContent("{\"performer\":1,\"projector\":1,\"engine\":1,\"audiences\":0}");
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = json });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
            }
        };
        var httpClient = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton<HttpClient>(httpClient);
        Services.AddSingleton<IHttpClientFactory>(new FactoryFromHandler(handler));

        var state = new PerformerUiState(new FactoryFromHandler(handler));
        state.SetSession("dev", new Uri("http://localhost"));
        state.RefreshCountsAsync().GetAwaiter().GetResult();
        // Put UI in Play phase with a current song to enable End Song
        state.UpdateGameState(new GameStateSnapshot(
            sessionCode: "dev",
            phase: Phase.Play,
            songIndex: 1,
            currentSong: new SongRef(new SongId("song-1"), "Title", "Artist"),
            catalog: Array.Empty<SongRef>(),
            choices: Array.Empty<string>(),
            hintIndex: 0,
            tallies: Array.Empty<int>(),
            scores: new Dictionary<string, int>(),
            songStartedAtUtc: null
        ));
        Services.AddSingleton(state);
        Services.AddSingleton(new CommandHistoryService());
        Services.AddSingleton(new OfflineCommandQueue());
        Services.AddScoped<PerformerCommands>();

        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<ControlPanel>();

        // Start End Song with Shift (bypass confirm), which should show countdown UI
        var endBtn = cut.FindAll("button").First(b => b.TextContent.Contains("End Song"));
        endBtn.Click(new MouseEventArgs { ShiftKey = true });

        // Countdown block should appear with Cancel button
        var cancelBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Cancel"));
        cancelBtn.Click();

        // Wait beyond 3 seconds to ensure command would have fired if not canceled
        await Task.Delay(3200);

        var posts = handler.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        var endPosts = posts.Where(r => r.RequestUri!.AbsolutePath.Contains("/v1/message/phase/end-song/")).ToList();
        Assert.Empty(endPosts);
    }
}
