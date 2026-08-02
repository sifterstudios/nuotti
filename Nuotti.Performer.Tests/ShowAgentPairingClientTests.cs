using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Nuotti.Performer.Services;
using Xunit;

namespace Nuotti.Performer.Tests;

public sealed class ShowAgentPairingClientTests
{
    [Fact]
    public async Task Issue_posts_to_the_session_pairings_route_with_the_bearer_token()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK,
            """{"code":"12345678","expiresAt":"2026-08-02T16:00:00Z"}""");
        var client = Create(handler);

        var issued = await client.IssuePairingCodeAsync("ws_1", "SHOW1", "sess-token");

        Assert.Equal("12345678", issued.Code);
        Assert.Equal(new DateTimeOffset(2026, 8, 2, 16, 0, 0, TimeSpan.Zero), issued.ExpiresAt);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/v1/workspaces/ws_1/sessions/SHOW1/show-agent/pairings", handler.Path);
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("sess-token", handler.Authorization?.Parameter);
    }

    [Fact]
    public async Task List_deserializes_every_paired_device()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK,
            """
            [
              {"agentId":"a1","name":"Stage projector","workspaceId":"ws_1","sessionCode":"SHOW1","state":"Ready","detail":null,"lastSeenAt":"2026-08-02T15:00:00Z","revoked":false},
              {"agentId":"a2","name":"Show agent","workspaceId":"ws_1","sessionCode":"SHOW1","state":"Offline","detail":null,"lastSeenAt":null,"revoked":false}
            ]
            """);
        var client = Create(handler);

        var statuses = await client.ListStatusesAsync("ws_1", "SHOW1", "sess-token");

        Assert.Equal(2, statuses.Count);
        Assert.Equal("Stage projector", statuses[0].Name);
        Assert.Equal(ShowAgentDeviceState.Ready, statuses[0].State);
        Assert.Equal("Show agent", statuses[1].Name);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/v1/workspaces/ws_1/sessions/SHOW1/show-agent", handler.Path);
    }

    [Fact]
    public async Task Revoke_sends_delete_and_returns_true_on_no_content()
    {
        var handler = new CapturingHandler(HttpStatusCode.NoContent, "");
        var client = Create(handler);

        Assert.True(await client.RevokeAsync("ws_1", "SHOW1", "sess-token"));
        Assert.Equal(HttpMethod.Delete, handler.Method);
        Assert.Equal("/v1/workspaces/ws_1/sessions/SHOW1/show-agent", handler.Path);
    }

    [Fact]
    public async Task Issue_surfaces_entitlement_detail_from_a_403()
    {
        var handler = new CapturingHandler(HttpStatusCode.Forbidden,
            """{"title":"Not entitled","detail":"Show Agent pairing requires an active entitlement."}""");
        var client = Create(handler);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.IssuePairingCodeAsync("ws_1", "SHOW1", "sess-token"));

        Assert.Equal("Show Agent pairing requires an active entitlement.", error.Message);
    }

    static ShowAgentPairingClient Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://backend") });

    sealed class CapturingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Method = request.Method;
            Path = request.RequestUri?.PathAndQuery;
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
