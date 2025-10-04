using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Model;

public class SongIdTests
{
    [Fact]
    public Task SongId_RoundTrip_WithoutLoss()
    {
        var original = new SongId("song-123");
        var json = JsonSerializer.Serialize(original, ContractsJson.DefaultOptions);

        return VerifyJson(json, VerifyDefaults.Settings());
    }
}