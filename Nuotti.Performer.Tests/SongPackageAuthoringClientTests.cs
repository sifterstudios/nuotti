using System.Net;
using System.Text;
using Nuotti.Performer.Services;
using Xunit;

namespace Nuotti.Performer.Tests;

public sealed class SongPackageAuthoringClientTests
{
    [Fact]
    public async Task Get_unwraps_the_saved_draft_document()
    {
        const string json = """
            {
              "workspaceId":"workspace",
              "catalogEntryId":"song",
              "document":{
                "playback":{"mode":"LiveOnly","backingAssetRevisionId":null,"clickAssetRevisionId":null,"songStartOffsetMs":0,"masterDurationMs":null,"backingDurationMs":null,"clickDurationMs":null,"backingOutputChannels":[],"clickOutputChannels":[]},
                "hints":[{"hintId":"hint-1","type":"Text","text":"A clue","assetRevisionId":null,"performerCue":null}],
                "lyrics":null
              },
              "updatedBy":"member",
              "updatedAt":"2026-07-31T12:00:00Z"
            }
            """;
        using var http = new HttpClient(new JsonHandler(json)) { BaseAddress = new Uri("https://backend") };
        var client = new SongPackageAuthoringClient(http);

        var document = await client.GetAsync("workspace", "song", "token");

        Assert.NotNull(document);
        Assert.Equal(AuthoringPlaybackMode.LiveOnly, document.Playback.Mode);
        Assert.Equal("A clue", Assert.Single(document.Hints).Text);
    }

    sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
