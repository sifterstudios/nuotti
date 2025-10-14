using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using Nuotti.Performer.Services;
using Nuotti.Performer.Shared;
using Xunit;
namespace Nuotti.Performer.Tests;

public class TroubleshootingToolsTests : MudTestContext
{
    private sealed class FakeEnv : IEnvironmentService
    {
        public bool IsDevelopment { get; set; }
        public string EnvironmentName { get; set; } = "Production";
    }

    private sealed class CapturingJs : IJSRuntime
    {
        public string? LastClipboardText { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "navigator.clipboard.writeText" && args is { Length: 1 } && args[0] is string s)
            {
                LastClipboardText = s;
                return ValueTask.FromResult((TValue)(object)default(bool));
            }
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    [Fact]
    public void Reset_button_disabled_in_production()
    {
        Services.AddSingleton<IEnvironmentService>(new FakeEnv { IsDevelopment = false, EnvironmentName = "Production" });
        Services.AddSingleton(new CommandHistoryService());
        Services.AddSingleton(new PerformerUiState(new DummyFactory()));

        // Render provider after configuring services, before rendering component under test
        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<TroubleshootingTools>();
        var resetBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Reset session"));
        Assert.Contains("disabled", resetBtn.OuterHtml);
    }

    [Fact]
    public void Copy_diagnostics_produces_expected_json()
    {
        var js = new CapturingJs();
        Services.AddSingleton<IJSRuntime>(js);
        Services.AddSingleton<IEnvironmentService>(new FakeEnv { IsDevelopment = true, EnvironmentName = "Development" });
        Services.AddSingleton(new CommandHistoryService());
        var state = new PerformerUiState(new DummyFactory());
        state.SetSession("ABC123", new Uri("http://localhost"));
        // Seed some counts
        typeof(PerformerUiState).GetProperty("ProjectorCount")!.SetValue(state, 2);
        typeof(PerformerUiState).GetProperty("EngineCount")!.SetValue(state, 1);
        typeof(PerformerUiState).GetProperty("AudienceCount")!.SetValue(state, 5);
        Services.AddSingleton(state);

        // Render provider after configuring services, before rendering component under test
        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<TroubleshootingTools>();
        var copyBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Copy diagnostics"));
        copyBtn.Click();

        Assert.False(string.IsNullOrWhiteSpace(js.LastClipboardText));
        var json = js.LastClipboardText!;
        Assert.Contains("\"versions\"", json);
        Assert.Contains("\"session\"", json);
        Assert.Contains("\"roles\"", json);
        Assert.Contains("\"ABC123\"", json);
        Assert.Contains("\"engine\"", json);
    }
}

sealed class DummyFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new HttpClient { BaseAddress = new Uri("http://localhost") };
}
