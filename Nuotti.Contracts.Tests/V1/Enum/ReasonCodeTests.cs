using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using System.Text.Json;
using Assert = Xunit.Assert;
namespace Nuotti.Contracts.Tests.V1.Enum;

public class ReasonCodeTests
{
    [Fact]
    public Task ReasonCode_names_are_stable()
        => Verify(System.Enum.GetNames(typeof(ReasonCode)), VerifyDefaults.Settings());

    [Fact]
    public Task ReasonCode_json_serialization_is_stable()
    {
        var values = System.Enum.GetValues<ReasonCode>();
        var map = values.ToDictionary(v => v.ToString(), v =>
        {
            var json = JsonSerializer.Serialize(v, JsonDefaults.Options);
            return json;
        });
        return Verify(map, VerifyDefaults.Settings());
    }

    [Fact]
    public void ReasonCode_roundtrip_deserializes_to_same_value()
    {
        foreach (var v in System.Enum.GetValues<ReasonCode>())
        {
            var json = JsonSerializer.Serialize(v, JsonDefaults.Options);
            var back = JsonSerializer.Deserialize<ReasonCode>(json, JsonDefaults.Options);
            Assert.Equal(v, back);
        }
    }
}