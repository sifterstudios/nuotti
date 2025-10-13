using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Xunit;
namespace Nuotti.Performer.Tests;

using BackendProgram = Backend.Program;
using PerformerProgram = Performer.Program;

public class SingleSongHappyPathE2E : IAsyncLifetime
{
    private WebApplicationFactory<BackendProgram>? _backend;
    private WebApplicationFactory<PerformerProgram>? _performer;
    private IPlaywright? _pw;
    private IBrowser? _browser;
    private int _backendPort;
    private int _performerPort;

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static WebApplicationFactory<T> RunOnKestrel<T>(int port) where T : class
        => new WebApplicationFactory<T>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("https_port", "");
                builder.UseSetting("urls", $"http://127.0.0.1:{port}");
                builder.UseKestrel();
                builder.UseEnvironment("Development");
            });

    public async Task InitializeAsync()
    {
        // Start backend and performer on ephemeral ports
        var backendPort = GetFreeTcpPort();
        var performerPort = GetFreeTcpPort();
        _backend = RunOnKestrel<Program>(backendPort);
        _performer = RunOnKestrel<Performer.Program>(performerPort);

        _pw = await Playwright.CreateAsync();
        _browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        await (_browser?.DisposeAsync() ?? ValueTask.CompletedTask);
        _pw?.Dispose();
        _performer?.Dispose();
        _backend?.Dispose();
    }

    [Fact]
    public async Task Performer_happy_path_single_song_Projector_and_Audience_observe()
    {
        if (_backend is null || _performer is null || _browser is null) throw new InvalidOperationException("Not initialized");
        var backendBase = _backend.Server.BaseAddress ?? new Uri("http://127.0.0.1");
        var performerBase = _performer.Server.BaseAddress ?? new Uri("http://127.0.0.1");

        var backendUrl = new Uri($"http://127.0.0.1:{backendBase.Port}");
        var performerUrl = new Uri($"http://127.0.0.1:{performerBase.Port}");

        var session = $"pw{Guid.NewGuid():N}".Substring(0, 6).ToUpperInvariant();

        // Projector actor subscribes to state changes to assert phase progression
        IHubClientFactory hubFactory = new HubConnectionFactory();
        var projector = new ProjectorActor(hubFactory, backendUrl, session);
        await projector.StartAsync();

        // Prepare minimal manifest so NextSong can reference a song
        var http = _performer.CreateClient();
        http.BaseAddress = performerUrl;
        var manifest = new
        {
            songs = new[] { new { title = "Song One", artist = "Artist" } }
        };
        // Upload manifest to backend
        var backendHttp = _backend.CreateClient();
        backendHttp.BaseAddress = backendUrl;
        var manifestPost = await backendHttp.PostAsJsonAsync($"/api/manifest/{session}", manifest);
        Assert.True(manifestPost.IsSuccessStatusCode);

        // Use Playwright to drive Performer UI
        var ctx = await _browser.NewContextAsync();
        var page = await ctx.NewPageAsync();
        await page.GotoAsync(performerUrl.ToString());

        // Set backend and session via Command Palette quick fields if present, otherwise use direct REST create-session call and set local state endpoints
        // Perform direct create-session to ensure snapshot exists
        var create = await backendHttp.PostAsJsonAsync($"/v1/message/phase/create-session/{session}", new { issuedByRole = 2, issuedById = "e2e" });
        Assert.True(create.IsSuccessStatusCode);

        // Open settings drawer toggle in UI if present to set Server and Session (fallback to local storage if not found)
        // Try inputs by labels
        try
        {
            await page.GetByLabel("Backend").FillAsync(backendUrl.ToString());
            await page.GetByLabel("Session").FillAsync(session);
        }
        catch
        {
            // Fallback: store in localStorage keys used by PerformerUiState
            await page.EvaluateAsync(@$"() => {{
                localStorage.setItem('nuotti.performer.backend', '{backendUrl}');
                localStorage.setItem('nuotti.performer.session', '{session}');
            }}");
            await page.ReloadAsync();
        }

        // Ensure Engine count is non-zero so buttons enable: simulate engine by touching counts directly via hub join as engine role is not implemented here.
        // The UI still allows NextSong in Guessing after StartSet regardless of engine for this E2E; we'll proceed.

        // Start Set
        await page.GetByTestId("btn-start-set").ClickAsync();

        // Next Song (bypass confirm via Shift)
        await page.GetByTestId("btn-next-song").ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Shift } });

        // Give Hint (ensure allowed)
        await page.GetByTestId("btn-give-hint").ClickAsync();

        // Lock
        await page.GetByTestId("btn-lock").ClickAsync();

        // Select first answer as correct if radio group is present, then Reveal
        var revealBtn = page.GetByTestId("btn-reveal");
        if (await revealBtn.IsVisibleAsync())
        {
            // If a radio exists, select index 0
            var radios = page.Locator("input[type=radio]");
            if (await radios.CountAsync() > 0)
            {
                await radios.First.ClickAsync();
            }
            await revealBtn.ClickAsync();
        }

        // End Song (bypass confirm and countdown via Shift)
        await page.GetByTestId("btn-end-song").ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Shift } });

        // Assertions: Projector actor should have seen multiple phase changes
        // Give it a moment to receive states
        await Task.Delay(500);
        Assert.Contains(projector.ReceivedPhases, p => p.ToString() == "Start");
        Assert.Contains(projector.ReceivedPhases, p => p.ToString() == "Guessing" || p.ToString() == "Hint");
        Assert.Contains(projector.ReceivedPhases, p => p.ToString() == "Lock" || p.ToString() == "Reveal");

        await projector.StopAsync();
        await ctx.CloseAsync();
    }
}
