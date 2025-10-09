using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using Nuotti.Performer.Pages;
using System.Net;
using System.Net.Http.Json;
using Xunit;
namespace Nuotti.Performer.Tests;

public class SetlistPlayStopTests : MudTestContext
{
    sealed class FakeManifestService : IManifestService
    {
        private readonly PerformerManifest _manifest;
        public FakeManifestService(PerformerManifest manifest) => _manifest = manifest;
        public Task<PerformerManifest> LoadAsync(string? path = null, CancellationToken ct = default) => Task.FromResult(_manifest);
        public Task SaveAsync(PerformerManifest manifest, string? path = null, CancellationToken ct = default) => Task.CompletedTask;
        public string GetDefaultPath() => "test";
    }

    sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? OnSendAsync;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (OnSendAsync is not null) return OnSendAsync(request, cancellationToken);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }

    [Fact]
    public async Task Play_sends_correct_fileUrl_to_backend_and_Stop_is_enabled_when_playing()
    {
        // Arrange a real temp file so availability check passes
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "test");
        var manifest = new PerformerManifest
        {
            Songs =
            [
                new PerformerManifest.SongEntry { Title = "Song A", Artist = "X", File = tmp }
            ]
        };
        Services.AddSingleton<IManifestService>(new FakeManifestService(manifest));

        var handler = new CapturingHandler
        {
            OnSendAsync = (req, ct) =>
            {
                // Return counts JSON for GET counts
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

        // Provide UI state with Engine connected and Playing
        var state = new PerformerUiState(new FactoryFromInstance(httpClient));
        state.SetSession("dev", new Uri("http://localhost"));
        // simulate that engine is present
        await state.RefreshCountsAsync(); // handler not used here; counts call not made by Setlist
        // Directly set via reflection is overkill; stop button derives from Phase too. We'll set Phase=Play to enable Stop.
        Services.AddSingleton(state);

        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<Setlist>();

        // Enter session code
        var input = cut.Find("input[placeholder='Enter session code']");
        input.Change("dev");

        // Act: click Play for first row
        var playBtn = cut.Find("button.btn.btn-sm.btn-success");
        playBtn.Click();

        // Assert HTTP call
        Assert.NotNull(handler.LastRequest);
        Assert.Contains("/api/play/dev", handler.LastRequest!.RequestUri!.ToString());
        var body = await handler.LastRequest!.Content!.ReadFromJsonAsync<PlayTrack>();
        Assert.NotNull(body);
        Assert.Equal(tmp, body!.FileUrl);

        // Simulate UI phase change to playing and rerender Stop state check
        state.UpdateGameState(new GameStateSnapshot("dev", Phase.Play, 0, null, [], [], 0, [], null, null));
        cut.Render();

        var stopBtn = cut.Find("button.btn.btn-sm.btn-outline-danger");
        Assert.False(stopBtn.HasAttribute("disabled"));
    }

    [Fact]
    public void Play_disabled_if_file_missing_or_engine_missing()
    {
        var manifest = new PerformerManifest
        {
            Songs = [ new PerformerManifest.SongEntry { Title = "Missing", File = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".mp3") } ]
        };
        Services.AddSingleton<IManifestService>(new FakeManifestService(manifest));

        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton<HttpClient>(httpClient);

        var state = new PerformerUiState(new FactoryFromInstance(httpClient));
        state.SetSession("dev", new Uri("http://localhost"));
        Services.AddSingleton(state);

        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<Setlist>();
        // Set session code but no engine connected
        var input = cut.Find("input[placeholder='Enter session code']");
        input.Change("dev");

        var playBtn = cut.Find("button.btn.btn-sm.btn-success");
        Assert.True(playBtn.HasAttribute("disabled"));
    }

    // Helper factory to satisfy PerformerUiState dependency when constructing
    sealed class FactoryFromInstance : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FactoryFromInstance(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }
}
