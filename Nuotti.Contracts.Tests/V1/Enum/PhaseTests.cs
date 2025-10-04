using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using System.Text.Json;
using Assert = Xunit.Assert;

namespace Nuotti.Contracts.Tests.V1.Enum;

public class PhaseTests
{
    [Fact]
    public Task Phase_names_are_stable()
        => Verify(System.Enum.GetNames(typeof(Phase)), VerifyDefaults.Settings());

    [Fact]
    public Task Phase_json_serialization_is_stable()
    {
        var values = System.Enum.GetValues<Phase>();
        var map = values.ToDictionary(v => v.ToString(), v =>
        {
            var json = JsonSerializer.Serialize(v, ContractsJson.DefaultOptions);
            return json;
        });
        return Verify(map, VerifyDefaults.Settings());
    }

    [Fact]
    public void Phase_roundtrip_deserializes_to_same_value()
    {
        foreach (var v in System.Enum.GetValues<Phase>())
        {
            var json = JsonSerializer.Serialize(v, ContractsJson.DefaultOptions);
            var back = JsonSerializer.Deserialize<Phase>(json, ContractsJson.DefaultOptions);
            Assert.Equal(v, back);
        }
    }
}