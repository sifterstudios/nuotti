using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Performer.Services;
using Nuotti.Performer.Shared;
using System.Net;
using Xunit;
namespace Nuotti.Performer.Tests;

public class CommandPaletteTests : MudTestContext
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

    void SetupCommon(IServiceCollection services, CapturingHandler handler)
    {
        var httpClient = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton<HttpClient>(httpClient);
        Services.AddSingleton<IHttpClientFactory>(new FactoryFromHandler(handler));

        Services.AddSingleton(new ThemeService());
        Services.AddSingleton(new CommandHistoryService());
        Services.AddSingleton(new OfflineCommandQueue());
        Services.AddSingleton(new KeyboardShortcutsService());
        Services.AddScoped<PerformerCommands>();
        Services.AddScoped<CommandPaletteService>();
    }

    [Fact]
    public void Keyboard_only_flow_opens_palette_and_executes_hint()
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
        SetupCommon(Services, handler);

        var state = new PerformerUiState(new FactoryFromHandler(handler));
        state.SetSession("dev", new Uri("http://localhost"));
        state.RefreshCountsAsync().GetAwaiter().GetResult();
        // Enable hint in Guessing phase
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

        // Render MainLayout to capture document keydown
        var layout = RenderComponent<MainLayout>();

        // Open palette with Ctrl+K
        layout.Find("div").KeyDown(new KeyboardEventArgs { Key = "k", CtrlKey = true });

        // Find input and type 'hint'
        var input = layout.Find("input");
        input.Input("hint");
        // Press Enter to execute
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // Assert give-hint was posted
        var posts = handler.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        Assert.Contains(posts, r => r.RequestUri!.AbsolutePath.Contains("/v1/message/phase/give-hint/"));
    }

    [Fact]
    public void Palette_respects_disabled_actions_per_phase()
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
        SetupCommon(Services, handler);

        var state = new PerformerUiState(new FactoryFromHandler(handler));
        state.SetSession("dev", new Uri("http://localhost"));
        state.RefreshCountsAsync().GetAwaiter().GetResult();
        // In Lobby phase, Next Song should be disabled
        state.UpdateGameState(new GameStateSnapshot(
            sessionCode: "dev",
            phase: Phase.Lobby,
            songIndex: 0,
            currentSong: null,
            catalog: Array.Empty<SongRef>(),
            choices: Array.Empty<string>(),
            hintIndex: 0,
            tallies: Array.Empty<int>(),
            scores: new Dictionary<string, int>(),
            songStartedAtUtc: null
        ));
        Services.AddSingleton(state);

        var layout = RenderComponent<MainLayout>();

        // Open palette
        layout.Find("div").KeyDown(new KeyboardEventArgs { Key = "k", CtrlKey = true });
        var input = layout.Find("input");
        input.Input("next");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var posts = handler.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        // No next-round should be posted
        Assert.DoesNotContain(posts, r => r.RequestUri!.AbsolutePath.Contains("/v1/message/phase/next-round/"));
    }
}
