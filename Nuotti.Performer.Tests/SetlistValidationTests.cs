using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Nuotti.Performer.Pages;
using Xunit;
namespace Nuotti.Performer.Tests;

public class SetlistValidationTests : MudTestContext
{
    sealed class DummyFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }
    sealed class FakeManifestService : IManifestService
    {
        public int SaveCalls;
        public Task SaveAsync(PerformerManifest manifest, string? path = null, CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
        public Task<PerformerManifest> LoadAsync(string? path = null, CancellationToken ct = default)
        {
            return Task.FromResult(new PerformerManifest());
        }
        public string GetDefaultPath() => "test";
    }

    [Fact(Skip = "Setlist now loads from the Backend workspace API and no longer uses IManifestService, so it no longer validates titles or blocks a manifest save. Rewrite against the new page rather than deleting: the rewritten Setlist currently has no coverage at all.")]
    public void EmptyTitle_ShouldShowError_AndBlockSave()
    {
        var fake = new FakeManifestService();
        Services.AddSingleton<IManifestService>(fake);
        Services.AddSingleton(new PerformerUiState(new DummyFactory()));

        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<Setlist>();

        // Click Add Row
        cut.Find("button.btn.btn-secondary").Click();

        // Attempt to Save
        cut.Find("button.btn.btn-primary").Click();

        // Expect validation banner and that Save wasn't called
        Assert.Contains("Fix validation errors before saving.", cut.Markup);
        Assert.Equal(0, fake.SaveCalls);
    }
}
