using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Performer.Pages;
using Xunit;
namespace Nuotti.Performer.Tests;

public class AvailabilityUiTests : TestContext
{
    sealed class FakeManifestService : IManifestService
    {
        private readonly PerformerManifest _manifest;
        public FakeManifestService(PerformerManifest manifest) => _manifest = manifest;
        public Task<PerformerManifest> LoadAsync(string? path = null, CancellationToken ct = default) => Task.FromResult(_manifest);
        public Task SaveAsync(PerformerManifest manifest, string? path = null, CancellationToken ct = default) => Task.CompletedTask;
        public string GetDefaultPath() => "test";
    }

    [Fact]
    public void MissingFiles_AreFlagged()
    {
        // Arrange: create a song with a non-existing file path
        var manifest = new PerformerManifest
        {
            Songs =
            [
                new PerformerManifest.SongEntry { Title = "Foo", File = Path.Combine(Path.GetTempPath(), "nonexistent-file-12345.mp3") }
            ]
        };
        Services.AddSingleton<IManifestService>(new FakeManifestService(manifest));

        // Act
        var cut = RenderComponent<Setlist>();

        // Assert
        Assert.Contains("Missing", cut.Markup);
    }
}
