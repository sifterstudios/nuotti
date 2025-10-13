using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Nuotti.Performer.Pages;
using Xunit;
namespace Nuotti.Performer.Tests;

public class HintEditorTests : MudTestContext
{
    sealed class DummyFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    sealed class CapturingManifestService : IManifestService
    {
        public PerformerManifest? Saved;
        public Task SaveAsync(PerformerManifest manifest, string? path = null, CancellationToken ct = default)
        {
            Saved = manifest;
            return Task.CompletedTask;
        }
        public Task<PerformerManifest> LoadAsync(string? path = null, CancellationToken ct = default)
        {
            return Task.FromResult(new PerformerManifest
            {
                Songs =
                [
                    new PerformerManifest.SongEntry
                    {
                        Title = "Song A",
                        Artist = "Artist A",
                        File = "http://example.com/a.mp3",
                        Hints = ["h1", "h2", "h3"]
                    }
                ]
            });
        }
        public string GetDefaultPath() => "test";
    }

    [Fact]
    public void Reordering_Hints_Persists_In_Saved_Manifest()
    {
        var manifest = new CapturingManifestService();
        Services.AddSingleton<IManifestService>(manifest);
        Services.AddSingleton(new PerformerUiState(new DummyFactory()));

        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<Setlist>();

        // Wait until hints are rendered
        cut.WaitForAssertion(() => Assert.True(cut.FindAll("button[title=\"Down\"]").Count > 0));

        // Move first hint down (h1 below h2): click first "Down" button
        cut.FindAll("button[title=\"Down\"]")[0].Click();

        // Save
        cut.Find("button.btn.btn-primary").Click();

        Assert.NotNull(manifest.Saved);
        var savedSong = Assert.Single(manifest.Saved!.Songs);
        Assert.Equal(new[] { "h2", "h1", "h3" }, savedSong.Hints);
    }
}
