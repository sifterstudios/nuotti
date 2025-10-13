using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Performer.Services;
using Nuotti.Performer.Shared;
using Xunit;
namespace Nuotti.Performer.Tests;

public class CommandHistoryDrawerTests : MudTestContext
{
    sealed class DummyFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    [Fact]
    public void Failed_command_shows_ProblemDetails()
    {
        // Arrange
        var history = new CommandHistoryService();
        Services.AddSingleton(history);
        Services.AddSingleton(new PerformerUiState(new DummyFactory()));
        RenderComponent<MudPopoverProvider>();

        // Create a command and a problem
        var cmd = new StartGame
        {
            SessionCode = "S",
            IssuedByRole = Role.Performer,
            IssuedById = "ui"
        };
        var problem = NuottiProblem.BadRequest("Invalid input", "Not allowed now", reason: ReasonCode.InvalidStateTransition);
        history.RecordFailure(cmd, problem);

        // Act
        var cut = RenderComponent<CommandHistoryDrawer>();

        // Assert: problem details should be rendered somewhere
        Assert.Contains("Problem Details", cut.Markup);
        Assert.Contains("Invalid input", cut.Markup);
        Assert.Contains("InvalidStateTransition", cut.Markup);
    }
}