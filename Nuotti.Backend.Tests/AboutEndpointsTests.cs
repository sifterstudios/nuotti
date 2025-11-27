using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace Nuotti.Backend.Tests;

/// <summary>
/// Tests for about endpoints (J5).
/// Verifies that /about returns valid JSON with expected fields.
/// </summary>
public class AboutEndpointsTests : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory;

    public AboutEndpointsTests(WebApplicationFactory<QuizHub> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task About_ReturnsValidJson_WithExpectedFields()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/about");
        
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
        
        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        // Verify expected fields exist and are non-empty
        Assert.True(root.TryGetProperty("service", out var service));
        Assert.True(root.TryGetProperty("version", out var version));
        Assert.True(root.TryGetProperty("runtime", out var runtime));
        
        Assert.Equal("Nuotti.Backend", service.GetString());
        Assert.False(string.IsNullOrWhiteSpace(version.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(runtime.GetString()));
        
        // GitCommit and BuildTime are optional but should be present if available
        root.TryGetProperty("gitCommit", out _);
        root.TryGetProperty("buildTime", out _);
    }
}

