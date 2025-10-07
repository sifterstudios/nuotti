using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Nuotti.Backend.Tests;

public class DevEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    readonly WebApplicationFactory<Program> _factory;

    public DevEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { }); // default env (Development in tests)
    }

    [Fact]
    public async Task Reset_IsAvailable_InDevelopment()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/dev/reset/ABC123", content: null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task Fake_Accepts_KnownEvent()
    {
        var client = _factory.CreateClient();
        var payload = new
        {
            type = "AnswerSubmitted",
            payload = new { AudienceId = "aud-1", ChoiceIndex = 2 }
        };
        var resp = await client.PostAsJsonAsync("/dev/fake/ABC123", payload);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task Fake_Rejects_UnknownType()
    {
        var client = _factory.CreateClient();
        var payload = new { type = "NotAnEvent", payload = new { x = 1 } };
        var resp = await client.PostAsJsonAsync("/dev/fake/ABC123", payload);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Endpoints_NotMapped_InProduction()
    {
        var prodFactory = _factory.WithWebHostBuilder(builder => builder.UseSetting("environment", "Production"));
        var client = prodFactory.CreateClient();
        var resetResp = await client.PostAsync("/dev/reset/ABC123", null);
        var fakeResp = await client.PostAsJsonAsync("/dev/fake/ABC123", new { type = "AnswerSubmitted", payload = new { AudienceId = "x", ChoiceIndex = 1 } });
        Assert.Equal(HttpStatusCode.NotFound, resetResp.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fakeResp.StatusCode);
    }
}
