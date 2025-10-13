using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend;
using Xunit;
namespace Nuotti.Performer.Tests;

public class SessionSelectionTests(WebApplicationFactory<QuizHub> factory) : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = factory.WithWebHostBuilder(_ => { /* default dev env */ });

    [Fact]
    public async Task CreateNewSession_ReturnsCode_And_SwitchesToControlView()
    {
        var svc = new SessionSelectionService();
        using var client = _factory.CreateClient();
        var code = await svc.CreateNewSessionAsync(client);
        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.Equal(UiState.Control, svc.State);
    }
}
