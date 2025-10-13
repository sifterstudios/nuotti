using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Performer.Services;
using Nuotti.Performer.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace Nuotti.Performer.Tests;

public class OfflineQueueIntegrationTests : MudTestContext
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
    public async Task Integration_offline_queue_flushes_on_reconnect_in_order_and_preserves_CommandId()
    {
        var handler = new CapturingHandler();
        bool drop = true;
        handler.OnSendAsync = (req, ct) =>
        {
            // counts always OK
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/api/sessions/") && req.RequestUri!.AbsolutePath.EndsWith("/counts"))
            {
                var json = new StringContent("{\"performer\":1,\"projector\":1,\"engine\":1,\"audiences\":0}");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = json });
            }
            if (drop && req.Method == HttpMethod.Post)
            {
                throw new HttpRequestException("Simulated drop");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        };

        var httpClient = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton<HttpClient>(httpClient);
        Services.AddSingleton<IHttpClientFactory>(new FactoryFromHandler(handler));

        var state = new PerformerUiState(new FactoryFromHandler(handler));
        state.SetSession("dev", new Uri("http://localhost"));
        await state.RefreshCountsAsync();
        state.UpdateGameState(new GameStateSnapshot(
            sessionCode: "dev",
            phase: Phase.Guessing,
            songIndex: 1,
            currentSong: new SongRef(new SongId("song-1"), "Title", "Artist"),
            catalog: Array.Empty<SongRef>(),
            choices: new[] { "A", "B", "C", "D" },
            hintIndex: 0,
            tallies: Array.Empty<int>(),
            scores: new Dictionary<string, int>(),
            songStartedAtUtc: null
        ));
        Services.AddSingleton(state);
        var history = new CommandHistoryService();
        Services.AddSingleton(history);
        var queue = new OfflineCommandQueue();
        Services.AddSingleton(queue);
        Services.AddScoped<PerformerCommands>();

        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<ControlPanel>();

        // Act: issue two commands during drop
        var commands = Services.GetRequiredService<PerformerCommands>();
        await commands.NextSongAsync(new SongId("song-1"));
        await commands.LockAnswersAsync();

        // There should be two POST attempts captured that failed
        var prePosts = handler.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        Assert.Equal(2, prePosts.Count);

        // Capture commandIds of both payloads
        string GetCommandId(HttpRequestMessage m)
        {
            var text = m.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.GetProperty("commandId").GetString()!;
        }
        var preIds = prePosts.Select(GetCommandId).ToList();
        Assert.Equal(2, preIds.Distinct().Count());

        // Simulate reconnect
        drop = false;
        await state.RefreshCountsAsync(); // marks Connected=true

        // Manually flush (in app this is triggered by MainLayout when Connected becomes true)
        await queue.FlushAsync(async (route, cmd) =>
        {
            var url = $"/v1/message/phase/{route}/{Uri.EscapeDataString(state.SessionCode!)}";
            return await httpClient.PostAsJsonAsync(url, cmd, ContractsJson.RestOptions);
        }, null, history);

        var allPosts = handler.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        Assert.Equal(4, allPosts.Count);
        var postFlush = allPosts.Skip(2).ToList();
        var postIds = postFlush.Select(GetCommandId).ToList();

        // Assert flush order and preserved ids
        Assert.Equal(preIds[0], postIds[0]);
        Assert.Equal(preIds[1], postIds[1]);
    }
}