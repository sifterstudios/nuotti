using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Model;

public class TallyTests
{
    [Fact]
    public Task Tally_RoundTrip_WithoutLoss()
    {
        var original = new Tally("B", 42);
        var json = JsonSerializer.Serialize(original, ContractsJson.DefaultOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}