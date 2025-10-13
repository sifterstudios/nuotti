using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Contracts.V1.Model;
using System.Net;
using System.Net.Http.Json;
using Xunit;
namespace Nuotti.Backend.Tests;

public class ManifestEndpointsTests(WebApplicationFactory<QuizHub> factory) : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Manifest_Invalid_EmptySongs_Returns422()
    {
        var client = _factory.CreateClient();
        var session = "manifest-session-1";
        var manifest = new SetlistManifest { Songs = new() }; // invalid: no songs

        var resp = await client.PostAsJsonAsync($"/api/manifest/{session}", manifest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Manifest_Invalid_BadFileUrl_Returns422()
    {
        var client = _factory.CreateClient();
        var session = "manifest-session-2";
        var manifest = new SetlistManifest
        {
            Songs =
            [
                new SetlistManifest.SongEntry
                {
                    Title = "Song A",
                    Artist = "Artist A",
                    File = "ftp://not-allowed",
                    Hints = ["h1"]
                }
            ]
        };

        var resp = await client.PostAsJsonAsync($"/api/manifest/{session}", manifest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }
}
