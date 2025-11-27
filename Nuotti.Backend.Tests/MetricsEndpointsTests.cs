using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace Nuotti.Backend.Tests;

/// <summary>
/// Tests for metrics endpoints (J4).
/// Verifies that /metrics returns valid JSON with expected keys.
/// </summary>
public class MetricsEndpointsTests : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory;

    public MetricsEndpointsTests(WebApplicationFactory<QuizHub> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Metrics_ReturnsValidJson_WithExpectedKeys()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/metrics");
        
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
        
        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        // Verify expected keys exist
        Assert.True(root.TryGetProperty("uptimeSeconds", out _));
        Assert.True(root.TryGetProperty("answersPerMinute", out _));
        Assert.True(root.TryGetProperty("commandApplyLatencyP50Ms", out _));
        Assert.True(root.TryGetProperty("commandApplyLatencyP95Ms", out _));
        Assert.True(root.TryGetProperty("totalAnswersSubmitted", out _));
        Assert.True(root.TryGetProperty("activeConnections", out _));
        
        // Verify types
        Assert.Equal(JsonValueKind.Number, root.GetProperty("uptimeSeconds").ValueKind);
        Assert.Equal(JsonValueKind.Number, root.GetProperty("answersPerMinute").ValueKind);
        Assert.Equal(JsonValueKind.Number, root.GetProperty("commandApplyLatencyP50Ms").ValueKind);
        Assert.Equal(JsonValueKind.Number, root.GetProperty("commandApplyLatencyP95Ms").ValueKind);
        Assert.Equal(JsonValueKind.Number, root.GetProperty("totalAnswersSubmitted").ValueKind);
        Assert.Equal(JsonValueKind.Object, root.GetProperty("activeConnections").ValueKind);
    }
}

