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

public class KeyboardShortcutTests : MudTestContext
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
    public void E2E_keyboard_triggers_next_song_same_as_clicks()
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

        var kb = new KeyboardShortcutsService();
        Services.AddSingleton(kb);

        var state = new PerformerUiState(new FactoryFromHandler(handler));
        state.SetSession("dev", new Uri("http://localhost"));
        state.RefreshCountsAsync().GetAwaiter().GetResult();
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
        Services.AddScoped<PerformerCommands>();

        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<ControlPanel>();

        // Fire keyboard: Shift+N should bypass confirm and post next-round
        cut.Find("div").KeyDown(new KeyboardEventArgs { Key = "n", ShiftKey = true });

        var posts = handler.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        var nextPosts = posts.Where(r => r.RequestUri!.AbsolutePath.Contains("/v1/message/phase/next-round/")).ToList();
        Assert.Equal(1, nextPosts.Count);
    }

    [Fact]
    public void Unit_global_handler_respects_modal_focus()
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

        var kb = new KeyboardShortcutsService { Suspended = true };
        Services.AddSingleton(kb);

        var state = new PerformerUiState(new FactoryFromHandler(handler));
        state.SetSession("dev", new Uri("http://localhost"));
        state.RefreshCountsAsync().GetAwaiter().GetResult();
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
        Services.AddScoped<PerformerCommands>();

        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<ControlPanel>();

        // Attempt to trigger Next via keyboard should be ignored due to suspension
        cut.Find("div").KeyDown(new KeyboardEventArgs { Key = "n", ShiftKey = true });

        var posts = handler.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        var nextPosts = posts.Where(r => r.RequestUri!.AbsolutePath.Contains("/v1/message/phase/next-round/")).ToList();
        Assert.Empty(nextPosts);
    }
}