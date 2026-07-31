using System.Net;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nuotti.AudioEngine.Tests;

public sealed class ShowAgentCloudClientTests
{
    [Fact]
    public async Task Pairing_saves_credential_and_all_runtime_traffic_is_outbound_http()
    {
        var store = new MemoryCredentialStore();
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, """{"agentId":"a1","credential":"secret","accessToken":"initial","accessTokenExpiresAt":"2020-01-01T00:00:00Z"}"""),
            Json(HttpStatusCode.OK, """{"accessToken":"lease","expiresAt":"2099-01-01T00:00:00Z","workspaceId":"ws1","sessionCode":"SHOW1"}"""),
            Json(HttpStatusCode.OK, """[{"sequence":1,"messageType":"StopTrack","payload":{}}]"""),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new ShowAgentCloudClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test") }, store);

        await client.PairAsync("12345678", "Stage laptop");
        var commands = await client.PollAsync(0);
        var reported = await client.ReportStatusAsync("Ready", null);

        Assert.Equal("secret", store.Value);
        Assert.Single(commands!);
        Assert.True(reported);
        Assert.Equal([HttpMethod.Post, HttpMethod.Post, HttpMethod.Get, HttpMethod.Put],
            handler.Requests.Select(request => request.Method).ToArray());
        Assert.All(handler.Requests, request => Assert.Equal("https", request.RequestUri!.Scheme));
    }

    [Fact]
    public async Task Revoked_credential_is_deleted_and_cannot_refresh()
    {
        var store = new MemoryCredentialStore { Value = "revoked" };
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = new ShowAgentCloudClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test") }, store);

        Assert.Null(await client.EnsureLeaseAsync());
        Assert.Null(store.Value);
    }

    [Fact]
    public void Playback_payload_uses_web_json_names()
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            """{"fileUrl":"track.wav","assetRevisionId":"rev-1","sessionCode":"SHOW1","issuedByRole":"Performer","issuedById":"performer-1"}""");
        var command = ShowAgentCloudClient.DeserializePayload<Nuotti.Contracts.V1.Message.PlayTrack>(document.RootElement);
        Assert.Equal("track.wav", command!.FileUrl);
        Assert.Equal("rev-1", command.AssetRevisionId);
    }

    static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    sealed class MemoryCredentialStore : IShowAgentCredentialStore
    {
        public string? Value { get; set; }
        public string? Load() => Value;
        public void Save(string credential) => Value = credential;
        public long LoadCursor(string workspaceId, string sessionCode) => 0;
        public void SaveCursor(string workspaceId, string sessionCode, long sequence) { }
        public void Delete() => Value = null;
    }

    sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new HttpRequestMessage(request.Method, request.RequestUri));
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
