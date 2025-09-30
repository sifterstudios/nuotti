using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Model;

public class SongRefTests
{
    [Fact]
    public Task SongRef_RoundTrip_WithoutLoss()
    {
        var original = new SongRef(new SongId("song-abc"), "Song Title", "Artist Name");
        var json = JsonSerializer.Serialize(original, JsonDefaults.Options);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}