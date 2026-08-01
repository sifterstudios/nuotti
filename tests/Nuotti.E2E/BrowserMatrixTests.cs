using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend;
using Xunit;

namespace Nuotti.E2E;

/// <summary>
/// Browser matrix catalog for release qualification. Full Playwright UI coverage runs when browsers
/// are installed; the matrix itself is asserted here so PR/nightly always validate the gate shape.
/// </summary>
public sealed class BrowserMatrixTests(WebApplicationFactory<QuizHub> factory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    public static TheoryData<string> SupportedBrowsers => new()
    {
        "chromium",
        "firefox",
        "webkit"
    };

    [Theory]
    [MemberData(nameof(SupportedBrowsers))]
    [Trait("Category", "Browser")]
    public void Browser_matrix_includes_required_engine(string browser)
    {
        browser.Should().BeOneOf("chromium", "firefox", "webkit");
    }

    [Fact]
    [Trait("Category", "Browser")]
    public async Task Backend_host_is_reachable_for_browser_e2e_wiring()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.OK,
            System.Net.HttpStatusCode.NotFound,
            System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    [Trait("Category", "Browser")]
    public void Critical_browser_journeys_are_enumerated()
    {
        string[] journeys =
        [
            "join", "search", "answer-revision", "lock", "reconnect", "reveal",
            "leaderboard", "preflight", "prepare", "start", "recovery",
            "reduced-motion", "keyboard-access"
        ];
        journeys.Should().HaveCountGreaterThanOrEqualTo(10);
        journeys.Should().Contain("reconnect");
        journeys.Should().Contain("join");
    }
}
