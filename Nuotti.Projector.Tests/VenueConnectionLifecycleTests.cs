using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Projector.Services;
using Xunit;

namespace Nuotti.Projector.Tests;

/// <summary>
/// Connection lifecycle is the Venue Session state machine; MainWindow only binds UI.
/// These tests cover token-gate and bounded resync without an Avalonia window.
/// </summary>
public sealed class VenueConnectionLifecycleTests : IAsyncDisposable
{
    readonly string _credentialPath = Path.Combine(Path.GetTempPath(), $"nuotti-lifecycle-{Guid.NewGuid():N}.json");
    HubConnection? _hub;

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            try { await _hub.DisposeAsync(); } catch { /* test teardown */ }
        }
        if (File.Exists(_credentialPath)) File.Delete(_credentialPath);
    }

    [Fact]
    public async Task Start_holds_when_lease_refresh_fails_without_wiping_pairing()
    {
        var store = new VenueCredentialStore(_credentialPath);
        store.Save(new VenueDeviceCredential("agent-1", "cred-1", "ws-1", "SHOW42"));
        var handler = new StubHandler().Post("/v1/show-agent/token", null, HttpStatusCode.BadGateway);
        var pairing = new VenueDevicePairingClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.nuotti.test") }, store);
        _hub = new HubConnectionBuilder().WithUrl("http://127.0.0.1:9/hub").Build();

        string? status = null;
        var logs = new List<string>();
        var lifecycle = Build(pairing, logs, s => status = s, session: "SHOW42");

        Assert.False(await lifecycle.StartAsync());
        Assert.NotNull(store.Load());
        Assert.Contains(logs, l => l.Contains("no access token", StringComparison.Ordinal));
        Assert.Equal("Waiting for API — lease refresh failed", status);
    }

    [Fact]
    public async Task Resync_gives_up_after_max_attempts_instead_of_recursing_forever()
    {
        var store = new VenueCredentialStore(_credentialPath);
        store.Save(new VenueDeviceCredential("agent-1", "cred-1", "ws-1", "SHOW42"));
        // Lease succeeds so we reach hub StartAsync, which fails against a dead endpoint.
        var handler = new StubHandler()
            .Post("/v1/show-agent/token", new { accessToken = "tok", expiresAt = DateTimeOffset.UtcNow.AddMinutes(10), workspaceId = "ws-1", sessionCode = "SHOW42" });
        var pairing = new VenueDevicePairingClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.nuotti.test") }, store);
        _hub = new HubConnectionBuilder().WithUrl("http://127.0.0.1:9/hub").Build();

        string? failed = null;
        var logs = new List<string>();
        var lifecycle = Build(pairing, logs, _ => { }, session: "SHOW42",
            onFailed: d => failed = d, maxAttempts: 2);

        Assert.False(await lifecycle.ResyncAsync());
        Assert.NotNull(failed);
        Assert.Contains("Gave up after 2", failed, StringComparison.Ordinal);
        Assert.Equal(2, logs.Count(l => l.Contains("reconnect error", StringComparison.Ordinal)));
    }

    VenueConnectionLifecycle Build(
        VenueDevicePairingClient pairing,
        List<string> logs,
        Action<string> onStatus,
        string session,
        Action<string>? onFailed = null,
        int maxAttempts = 5)
    {
        var sessionCode = session;
        return new VenueConnectionLifecycle(
            _hub!,
            pairing,
            new ReconnectService("https://api.nuotti.test", () => pairing.GetAccessTokenAsync()),
            "https://api.nuotti.test",
            () => sessionCode,
            code => sessionCode = code,
            new VenueConnectionCallbacks
            {
                Log = logs.Add,
                NeedPairing = _ => { },
                ConnectionStatus = onStatus,
                SessionCodeChanged = _ => { },
                HubStarted = _ => { },
                EngineStarted = _ => { },
                ResyncProgress = (_, _) => { },
                ResyncSucceeded = _ => { },
                ResyncFailed = onFailed ?? (_ => { })
            },
            maxResyncAttempts: maxAttempts);
    }

    sealed class StubHandler : HttpMessageHandler
    {
        readonly Dictionary<string, (object? Body, HttpStatusCode Status)> _responses = new();

        public StubHandler Post(string path, object? body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _responses[path] = (body, status);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (!_responses.TryGetValue(path, out var configured))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var response = new HttpResponseMessage(configured.Status);
            if (configured.Body is not null)
                response.Content = new StringContent(JsonSerializer.Serialize(configured.Body), Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }
}
