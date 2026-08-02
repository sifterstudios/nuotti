using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using Nuotti.Performer.Services;
using Nuotti.Performer.Shared;
using Xunit;

namespace Nuotti.Performer.Tests;

public sealed class VenueDevicePairingPanelTests : MudTestContext
{
    [Fact]
    public void Generate_shows_the_issued_code_grouped_for_reading()
    {
        Services.AddSingleton(new ShowAgentPairingClient(new HttpClient(new OkHandler())
        {
            BaseAddress = new Uri("https://backend")
        }));
        Services.AddSingleton(ReadyWorkspace());
        RenderComponent<MudPopoverProvider>();

        var cut = RenderComponent<VenueDevicePairingPanel>(parameters =>
            parameters.Add(p => p.SessionCode, "SHOW1"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Generate pairing code")).Click();
        cut.WaitForAssertion(() => Assert.Contains("1234 5678", cut.Markup));
    }

    [Fact]
    public void Without_a_session_code_the_panel_explains_how_to_continue()
    {
        Services.AddSingleton(new ShowAgentPairingClient(new HttpClient(new OkHandler())
        {
            BaseAddress = new Uri("https://backend")
        }));
        Services.AddSingleton(ReadyWorkspace());
        RenderComponent<MudPopoverProvider>();

        var cut = RenderComponent<VenueDevicePairingPanel>();
        Assert.Contains("Connect a session first", cut.Markup);
    }

    static WorkspaceSession ReadyWorkspace()
    {
        var session = new WorkspaceSession(
            new DummyFactory(),
            new ConfigurationBuilder().Build(),
            new TestEnv(),
            new NullStore());
        typeof(WorkspaceSession).GetProperty(nameof(WorkspaceSession.WorkspaceId))!
            .SetValue(session, "ws_1");
        typeof(WorkspaceSession).GetProperty(nameof(WorkspaceSession.SessionToken))!
            .SetValue(session, "tok");
        typeof(WorkspaceSession).GetProperty(nameof(WorkspaceSession.WorkspaceName))!
            .SetValue(session, "Band");
        return session;
    }

    sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"code":"12345678","expiresAt":"2099-01-01T00:00:00Z"}""",
                        System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    sealed class NullStore : IWorkspaceSessionStore
    {
        public Task<string?> GetTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task SetTokenAsync(string token, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearTokenAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    sealed class TestEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
