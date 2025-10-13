using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Nuotti.Performer.Services;
using Nuotti.Performer.Shared;
using System.Collections.Concurrent;
using System.Net;
using Xunit;
namespace Nuotti.Performer.Tests;

public class TourTests : MudTestContext
{
    private sealed class FakeJSRuntime : IJSRuntime
    {
        private readonly ConcurrentDictionary<string, string> _store = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args ?? Array.Empty<object?>());
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "localStorage.getItem")
            {
                var key = args is { Length: > 0 } ? args![0]?.ToString() ?? string.Empty : string.Empty;
                _store.TryGetValue(key, out var val);
                return new ValueTask<TValue>((TValue)(object?)val!);
            }
            if (identifier == "localStorage.setItem")
            {
                var key = args is { Length: > 0 } ? args![0]?.ToString() ?? string.Empty : string.Empty;
                var val = args is { Length: > 1 } ? args![1]?.ToString() ?? string.Empty : string.Empty;
                _store[key] = val;
                return new ValueTask<TValue>((TValue)(object?)default!);
            }
            throw new NotSupportedException(identifier);
        }
    }

    [Fact]
    public async Task TourService_persists_seen_state()
    {
        var js = new FakeJSRuntime();
        var svc = new TourService(js);
        var seen1 = await svc.GetSeenAsync();
        Assert.False(seen1);
        await svc.SetSeenAsync(true);
        var seen2 = await svc.GetSeenAsync();
        Assert.True(seen2);
    }

    [Fact]
    public void Help_menu_opens_tour_dialog()
    {
        // Arrange: Use a fake tour service that reports seen already
        Services.AddScoped<ITourService>(_ => new TourService(new FakeJSRuntime()));
        Services.AddSingleton(new PerformerUiState(new HttpClientFactory()));
        Services.AddSingleton<CommandHistoryService>();
        Services.AddSingleton<KeyboardShortcutsService>();
        Services.AddSingleton<OfflineCommandQueue>();
        Services.AddScoped<PerformerCommands>();

        // Render layout
        var cut = RenderComponent<MainLayout>();

        // Act: click Show Tour in Help group
        var link = cut.Find("a.mud-nav-link:contains('Show Tour')");
        link.Click();

        // Assert: dialog content contains welcome text
        Assert.Contains("Welcome to Nuotti Performer", cut.Markup);
    }

    private sealed class HttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient(new FakeHandler());

        private sealed class FakeHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }
}
