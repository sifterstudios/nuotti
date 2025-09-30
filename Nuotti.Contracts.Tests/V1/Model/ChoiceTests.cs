using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Model;

public class ChoiceTests
{
    [Fact]
    public Task Choice_RoundTrip_WithoutLoss()
    {
        var original = new Choice("A", "Piano");
        var json = JsonSerializer.Serialize(original, JsonDefaults.Options);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}