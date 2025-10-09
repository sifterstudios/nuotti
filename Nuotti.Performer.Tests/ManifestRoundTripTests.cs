using Xunit;
namespace Nuotti.Performer.Tests;

public class ManifestRoundTripTests
{
    [Fact]
    public async Task SerializeDeserialize_RoundTrip_ShouldPreserveData()
    {
        var service = new ManifestService();
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-performer-manifest.json");
        try
        {
            var original = new PerformerManifest
            {
                Songs =
                [
                    new PerformerManifest.SongEntry
                    {
                        Title = "Song A",
                        Artist = "Artist X",
                        Bpm = 120,
                        File = "http://example.com/a.mp3",
                        Hints = new() { "hint1", "hint2" }
                    },
                    new PerformerManifest.SongEntry
                    {
                        Title = "Song B",
                        Artist = "Artist Y",
                        Bpm = null,
                        File = Path.GetTempFileName(),
                        Hints = new()
                    }
                ]
            };

            await service.SaveAsync(original, tmp);
            var loaded = await service.LoadAsync(tmp);

            Assert.Equal(original.Songs.Count, loaded.Songs.Count);
            for (int i = 0; i < original.Songs.Count; i++)
            {
                Assert.Equal(original.Songs[i].Title, loaded.Songs[i].Title);
                Assert.Equal(original.Songs[i].Artist, loaded.Songs[i].Artist);
                Assert.Equal(original.Songs[i].Bpm, loaded.Songs[i].Bpm);
                Assert.Equal(original.Songs[i].File, loaded.Songs[i].File);
                Assert.Equal(original.Songs[i].Hints, loaded.Songs[i].Hints);
            }
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
