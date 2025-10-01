using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Model;

public class HintTests
{
    [Fact]
    public Task Hint_RoundTrip_WithoutLoss()
    {
        var original = new Hint(0, "Instrument Family", null, new SongId("song-abc"));
        var json = JsonSerializer.Serialize(original, JsonDefaults.Options);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}