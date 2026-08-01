using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Nuotti.Backend.Workspaces;
namespace Nuotti.Backend.Tests;

public class DevEndpointsTests : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory;

    public DevEndpointsTests(WebApplicationFactory<QuizHub> factory)
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
    public async Task Fixture_Returns_Stable_Dev_Workspace()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/v1/dev/fixture");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var fixture = await resp.Content.ReadFromJsonAsync<DevelopmentWorkspaceFixture>();
        Assert.NotNull(fixture);
        Assert.Equal(DevelopmentWorkspaceDefaults.WorkspaceId, fixture.WorkspaceId);
        Assert.Equal(DevelopmentWorkspaceDefaults.SessionToken, fixture.SessionToken);

        using var auth = new HttpRequestMessage(HttpMethod.Get, $"/v1/workspaces/{fixture.WorkspaceId}/catalog");
        auth.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", fixture.SessionToken);
        var catalog = await client.SendAsync(auth);
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
    }

    [Fact]
    public async Task Endpoints_NotMapped_InProduction()
    {
        var prodFactory = _factory.WithWebHostBuilder(builder => builder.UseSetting("environment", "Production"));
        var client = prodFactory.CreateClient();
        var resetResp = await client.PostAsync("/dev/reset/ABC123", null);
        var fakeResp = await client.PostAsJsonAsync("/dev/fake/ABC123", new { type = "AnswerSubmitted", payload = new { AudienceId = "x", ChoiceIndex = 1 } });
        var fixtureResp = await client.GetAsync("/v1/dev/fixture");
        Assert.Equal(HttpStatusCode.NotFound, resetResp.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fakeResp.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fixtureResp.StatusCode);
    }
}
