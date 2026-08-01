using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Nuotti.Performer.Services;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Nuotti.Performer.Tests;

/// <summary>
/// The magic-link sign-in path. Every failure here locks a user out of a hosted Performer, and
/// the Backend deliberately gives no detail beyond a status code, so the mapping from status to
/// behaviour is the whole contract.
/// </summary>
public sealed class WorkspaceSessionSignInTests
{
    [Fact]
    public async Task Redeeming_a_valid_token_stores_the_session_for_the_next_visit()
    {
        var store = new FakeSessionStore();
        var handler = new RouteHandler()
            .Post("/v1/auth/magic-links/redeem", Redeemed("sess-1", "band@example.com", selected: null));
        var session = Create(handler, store);

        var outcome = await session.RedeemAsync("tok-1");

        Assert.Equal(RedeemOutcome.Succeeded, outcome);
        Assert.True(session.IsAuthenticated);
        Assert.Equal("band@example.com", session.Email);
        Assert.Equal("sess-1", store.Token);
    }

    [Fact]
    public async Task Redeeming_sends_the_token_the_backend_expects()
    {
        var handler = new RouteHandler()
            .Post("/v1/auth/magic-links/redeem", Redeemed("sess-1", "band@example.com", selected: null));
        await Create(handler, new FakeSessionStore()).RedeemAsync("tok-abc");

        // PostAsJsonAsync uses JsonSerializerDefaults.Web, so the token goes out camelCased. The
        // Backend binds case-insensitively, but this pins what actually crosses the wire.
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("tok-abc", body.RootElement.GetProperty("token").GetString());
    }

    [Fact]
    public async Task An_expired_or_used_link_is_reported_as_rejected_and_stores_nothing()
    {
        var store = new FakeSessionStore();
        var handler = new RouteHandler().Post("/v1/auth/magic-links/redeem", HttpStatusCode.NotFound);

        var outcome = await Create(handler, store).RedeemAsync("tok-stale");

        Assert.Equal(RedeemOutcome.Rejected, outcome);
        Assert.Null(store.Token);
    }

    [Fact]
    public async Task Redeeming_a_link_does_not_select_a_workspace_on_its_own()
    {
        // Membership and active context are separate decisions in the Backend. Treating a
        // redeemed link as "ready" would send the user to a page whose every call 404s.
        var handler = new RouteHandler()
            .Post("/v1/auth/magic-links/redeem", Redeemed("sess-1", "band@example.com", selected: null));
        var session = Create(handler, new FakeSessionStore());

        await session.RedeemAsync("tok-1");

        Assert.True(session.IsAuthenticated);
        Assert.False(session.IsReady);
    }

    [Fact]
    public async Task A_stored_token_signs_the_user_back_in_without_a_new_link()
    {
        var store = new FakeSessionStore { Token = "sess-kept" };
        var handler = new RouteHandler()
            .Get("/v1/workspaces", Workspaces(("ws-1", "The Satellites", "Owner")))
            .Post("/v1/workspaces/ws-1/select", HttpStatusCode.OK);

        var session = Create(handler, store);
        await session.EnsureReadyAsync();

        Assert.True(session.IsReady);
        Assert.Equal("ws-1", session.WorkspaceId);
        Assert.Equal("The Satellites", session.WorkspaceName);
    }

    [Fact]
    public async Task A_stored_token_the_backend_rejects_is_discarded_rather_than_reused()
    {
        var store = new FakeSessionStore { Token = "sess-revoked" };
        var handler = new RouteHandler().Get("/v1/workspaces", HttpStatusCode.Unauthorized);

        var session = Create(handler, store);
        await session.EnsureReadyAsync();

        Assert.False(session.IsAuthenticated);
        Assert.Null(store.Token);
    }

    [Fact]
    public async Task Several_workspaces_are_left_for_the_user_to_choose_between()
    {
        var store = new FakeSessionStore { Token = "sess-kept" };
        var handler = new RouteHandler().Get("/v1/workspaces",
            Workspaces(("ws-1", "The Satellites", "Owner"), ("ws-2", "Side Project", "Member")));

        var session = Create(handler, store);
        await session.EnsureReadyAsync();

        Assert.True(session.IsAuthenticated);
        Assert.False(session.IsReady);
        Assert.Equal(2, (await session.ListWorkspacesAsync()).Count);
    }

    [Fact]
    public async Task A_stored_token_outranks_the_development_fixture()
    {
        // Otherwise a developer signed in as a real user would silently become the fixture user.
        var store = new FakeSessionStore { Token = "sess-real" };
        var handler = new RouteHandler()
            .Get("/v1/workspaces", Workspaces(("ws-real", "Real Workspace", "Owner")))
            .Post("/v1/workspaces/ws-real/select", HttpStatusCode.OK)
            .Get("/v1/dev/fixture", """
                {"WorkspaceId":"ws-fixture","WorkspaceName":"Fixture","Email":"dev@example.com","SessionToken":"sess-fixture"}
                """);

        var session = Create(handler, store, development: true);
        await session.EnsureReadyAsync();

        Assert.Equal("ws-real", session.WorkspaceId);
        Assert.Equal("sess-real", session.SessionToken);
    }

    [Fact]
    public async Task Workspace_calls_carry_the_session_as_a_bearer_token()
    {
        var handler = new RouteHandler().Get("/v1/workspaces", Workspaces());
        var session = Create(handler, new FakeSessionStore { Token = "sess-bearer" });

        await session.EnsureReadyAsync();

        Assert.Equal("Bearer", handler.LastAuthScheme);
        Assert.Equal("sess-bearer", handler.LastAuthParameter);
    }

    [Fact]
    public async Task A_backend_without_email_delivery_says_so_instead_of_pretending_to_send()
    {
        var handler = new RouteHandler().Post("/v1/auth/magic-links", HttpStatusCode.ServiceUnavailable);
        var session = Create(handler, new FakeSessionStore());

        var requested = await session.RequestSignInAsync("band@example.com");

        Assert.False(requested);
        Assert.Contains("not configured", session.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Requesting_a_link_succeeds_on_the_accepted_status_the_backend_returns()
    {
        var handler = new RouteHandler().Post("/v1/auth/magic-links", HttpStatusCode.Accepted);

        Assert.True(await Create(handler, new FakeSessionStore()).RequestSignInAsync("band@example.com"));
    }

    [Fact]
    public async Task Signing_out_clears_the_stored_token()
    {
        var store = new FakeSessionStore { Token = "sess-kept" };
        var handler = new RouteHandler()
            .Get("/v1/workspaces", Workspaces(("ws-1", "The Satellites", "Owner")))
            .Post("/v1/workspaces/ws-1/select", HttpStatusCode.OK);
        var session = Create(handler, store);
        await session.EnsureReadyAsync();

        await session.SignOutAsync();

        Assert.False(session.IsAuthenticated);
        Assert.Null(store.Token);
    }

    static string Redeemed(string sessionToken, string email, string? selected) =>
        JsonSerializer.Serialize(new
        {
            SessionToken = sessionToken,
            Principal = new
            {
                UserId = "user-1",
                Email = email,
                SelectedWorkspaceId = selected,
                SessionId = "sid-1"
            }
        });

    static string Workspaces(params (string Id, string Name, string Role)[] workspaces) =>
        JsonSerializer.Serialize(workspaces.Select(w => new { WorkspaceId = w.Id, Name = w.Name, Role = w.Role }));

    static WorkspaceSession Create(RouteHandler handler, IWorkspaceSessionStore store, bool development = false) =>
        new(new StubHttpClientFactory(handler),
            new ConfigurationBuilder().Build(),
            new StubHostEnvironment(development ? Environments.Development : Environments.Production),
            store);

    sealed class FakeSessionStore : IWorkspaceSessionStore
    {
        public string? Token { get; set; }
        public Task<string?> GetTokenAsync(CancellationToken ct = default) => Task.FromResult(Token);
        public Task SetTokenAsync(string token, CancellationToken ct = default) { Token = token; return Task.CompletedTask; }
        public Task ClearTokenAsync(CancellationToken ct = default) { Token = null; return Task.CompletedTask; }
    }

    sealed class RouteHandler : HttpMessageHandler
    {
        readonly Dictionary<string, (HttpStatusCode Status, string Body)> _routes = new();

        public string? LastBody { get; private set; }
        public string? LastAuthScheme { get; private set; }
        public string? LastAuthParameter { get; private set; }

        public RouteHandler Get(string path, string body) => Add("GET", path, HttpStatusCode.OK, body);
        public RouteHandler Get(string path, HttpStatusCode status) => Add("GET", path, status, "");
        public RouteHandler Post(string path, string body) => Add("POST", path, HttpStatusCode.OK, body);
        public RouteHandler Post(string path, HttpStatusCode status) => Add("POST", path, status, "");

        RouteHandler Add(string method, string path, HttpStatusCode status, string body)
        {
            _routes[$"{method} {path}"] = (status, body);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null) LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            LastAuthScheme = request.Headers.Authorization?.Scheme;
            LastAuthParameter = request.Headers.Authorization?.Parameter;

            var key = $"{request.Method.Method} {request.RequestUri!.AbsolutePath}";
            if (!_routes.TryGetValue(key, out var route))
                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("") };

            return new HttpResponseMessage(route.Status)
            {
                Content = new StringContent(route.Body, Encoding.UTF8, "application/json")
            };
        }
    }

    sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("http://backend.test") };
    }

    sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Nuotti.Performer.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
