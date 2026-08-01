using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;
using Nuotti.Performer.Services;
namespace Nuotti.Performer.Tests;

/// <summary>
/// Shared bUnit TestContext that registers MudBlazor services required by components
/// (e.g., ISnackbar, IKeyInterceptorService, IDialogService, IResizeObserverFactory).
/// </summary>
public class MudTestContext : TestContext
{
    /// <summary>The workspace every page under test is signed in to.</summary>
    public const string TestWorkspaceId = "ws-test";
    public const string TestSessionToken = "sess-test";

    public MudTestContext()
    {
        // Register MudBlazor service singletons used by components under test
        Services.AddMudServices();

        // Pages that author songs and setlists inject WorkspaceSession and bail out unless it
        // is ready, so without this they render an error instead of the UI under test. The
        // Dev:* keys are WorkspaceSession's own static-configuration path, which needs no HTTP
        // call - the alternatives would need a stubbed Backend just to reach the first assert.
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dev:WorkspaceId"] = TestWorkspaceId,
                ["Dev:SessionToken"] = TestSessionToken,
                ["Dev:WorkspaceName"] = "Test Workspace"
            })
            .Build());
        // Production, so EnsureReadyAsync does not try to fetch the Development fixture.
        Services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        Services.AddSingleton<IWorkspaceSessionStore, NullWorkspaceSessionStore>();
        Services.AddSingleton<IHttpClientFactory, TestHttpClientFactory>();
        Services.AddScoped<WorkspaceSession>();
        // Typed client, registered in Program.cs via AddHttpClient<T>. Tests that assert on its
        // traffic register their own handler over the top of this one.
        Services.AddScoped(sp =>
            new SongPackageAuthoringClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SongPackageAuthoringClient))));

        // Loosen JS interop to avoid failing on MudBlazor's internal JS calls during unit tests
        JSInterop.Mode = JSRuntimeMode.Loose;
        // Common MudBlazor JS calls we can safely ignore in tests
        JSInterop.SetupVoid("mudElementRef.addOnBlurEvent", _ => true);
        JSInterop.SetupVoid("mudPopover.initialize", _ => true);
    }

    /// <summary>No stored token, so the session falls through to the Dev:* configuration above.</summary>
    sealed class NullWorkspaceSessionStore : IWorkspaceSessionStore
    {
        public Task<string?> GetTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task SetTokenAsync(string token, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearTokenAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new() { BaseAddress = new Uri("http://backend.test") };
    }

    sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Nuotti.Performer.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
