using Nuotti.Projector.Services;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Nuotti.Projector.Tests;

/// <summary>
/// The venue rig's half of pairing: redeem a code once, then keep a live lease without asking again.
/// </summary>
public sealed class VenueDevicePairingClientTests : IDisposable
{
    readonly string _credentialPath = Path.Combine(Path.GetTempPath(), $"nuotti-pairing-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_credentialPath)) File.Delete(_credentialPath);
    }

    [Fact]
    public async Task Pairing_learns_which_session_it_joined_and_remembers_it()
    {
        // The venue machine is never configured with a session code: it is told one by the band,
        // once, in the form of an eight-digit code.
        var handler = new StubHandler()
            .Post("/v1/show-agent/pair", new { agentId = "agent-1", credential = "cred-1", accessToken = "tok-1", accessTokenExpiresAt = In(10) })
            .Post("/v1/show-agent/token", new { accessToken = "tok-1", expiresAt = In(10), workspaceId = "ws-1", sessionCode = "SHOW42" });
        var client = Build(handler);

        var paired = await client.PairAsync("12345678", "Stage projector");

        Assert.NotNull(paired);
        Assert.Equal("SHOW42", paired!.SessionCode);
        Assert.Equal("ws-1", paired.WorkspaceId);
        Assert.Equal("SHOW42", new VenueCredentialStore(_credentialPath).Load()?.SessionCode);
    }

    [Fact]
    public async Task A_cached_lease_is_reused_rather_than_re_exchanged()
    {
        // Every hub reconnect asks for a token. Exchanging on each one would put a request on the
        // wire for every flap of a venue's wifi.
        var handler = new StubHandler()
            .Post("/v1/show-agent/pair", new { agentId = "agent-1", credential = "cred-1", accessToken = "tok-1", accessTokenExpiresAt = In(10) })
            .Post("/v1/show-agent/token", new { accessToken = "tok-1", expiresAt = In(10), workspaceId = "ws-1", sessionCode = "SHOW42" });
        var client = Build(handler);
        await client.PairAsync("12345678", "Stage projector");
        var exchangesAfterPairing = handler.Count("/v1/show-agent/token");

        Assert.Equal("tok-1", await client.GetAccessTokenAsync());
        Assert.Equal("tok-1", await client.GetAccessTokenAsync());

        Assert.Equal(exchangesAfterPairing, handler.Count("/v1/show-agent/token"));
    }

    [Fact]
    public async Task An_expiring_lease_is_refreshed()
    {
        var handler = new StubHandler()
            .Post("/v1/show-agent/token", new { accessToken = "tok-2", expiresAt = In(10), workspaceId = "ws-1", sessionCode = "SHOW42" });
        new VenueCredentialStore(_credentialPath).Save(new VenueDeviceCredential("agent-1", "cred-1", "ws-1", "SHOW42"));
        var client = Build(handler);

        Assert.Equal("tok-2", await client.GetAccessTokenAsync());
        Assert.Equal(1, handler.Count("/v1/show-agent/token"));
    }

    [Fact]
    public async Task A_revoked_device_forgets_its_credential_instead_of_retrying_forever()
    {
        // When the band revokes this projector, the stored credential is dead. Keeping it would
        // mean a machine in a venue hammering a 401 for the rest of the night.
        var handler = new StubHandler().Post("/v1/show-agent/token", null, HttpStatusCode.Unauthorized);
        var store = new VenueCredentialStore(_credentialPath);
        store.Save(new VenueDeviceCredential("agent-1", "cred-1", "ws-1", "SHOW42"));
        var client = Build(handler);

        Assert.Null(await client.GetAccessTokenAsync());
        Assert.Null(store.Load());
    }

    [Fact]
    public async Task An_unpaired_projector_asks_for_nothing_and_reports_no_token()
    {
        var handler = new StubHandler();
        var client = Build(handler);

        Assert.Null(await client.GetAccessTokenAsync());
        Assert.Equal(0, handler.Count("/v1/show-agent/token"));
    }

    VenueDevicePairingClient Build(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.nuotti.test") },
        new VenueCredentialStore(_credentialPath));

    static string In(int minutes) => DateTimeOffset.UtcNow.AddMinutes(minutes).ToString("O");

    sealed class StubHandler : HttpMessageHandler
    {
        readonly Dictionary<string, (object? Body, HttpStatusCode Status)> _responses = new();
        readonly Dictionary<string, int> _calls = new();

        public StubHandler Post(string path, object? body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _responses[path] = (body, status);
            return this;
        }

        public int Count(string path) => _calls.TryGetValue(path, out var count) ? count : 0;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            _calls[path] = Count(path) + 1;
            if (!_responses.TryGetValue(path, out var configured))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var response = new HttpResponseMessage(configured.Status);
            if (configured.Body is not null)
                response.Content = new StringContent(JsonSerializer.Serialize(configured.Body), Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }
}
