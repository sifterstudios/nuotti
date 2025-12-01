using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Nuotti.Backend.Tests;

/// <summary>
/// Tests for dynamic log level switch (J6).
/// Verifies that log level can be changed at runtime via DEV endpoint.
/// </summary>
public class DynamicLogLevelTests : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory;

    public DynamicLogLevelTests(WebApplicationFactory<QuizHub> factory)
    {
        // Ensure we're in Development mode for DEV endpoints
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });
    }

    [Fact]
    public async Task LogLevel_ChangeToDebug_Returns200()
    {
        var client = _factory.CreateClient();
        var payload = new { level = "Debug" };

        var resp = await client.PostAsJsonAsync("/dev/log-level", payload);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Debug", result.GetProperty("level").GetString());
    }

    [Fact]
    public async Task LogLevel_InvalidLevel_Returns400()
    {
        var client = _factory.CreateClient();
        var payload = new { level = "InvalidLevel" };

        var resp = await client.PostAsJsonAsync("/dev/log-level", payload);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task LogLevel_MissingLevel_Returns400()
    {
        var client = _factory.CreateClient();
        var payload = new { };

        var resp = await client.PostAsJsonAsync("/dev/log-level", payload);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task LogLevel_Endpoint_OnlyAvailableInDevelopment()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
        });

        var client = factory.CreateClient();
        var payload = new { level = "Debug" };

        var resp = await client.PostAsJsonAsync("/dev/log-level", payload);

        // In Production, DEV endpoints are not mapped
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

