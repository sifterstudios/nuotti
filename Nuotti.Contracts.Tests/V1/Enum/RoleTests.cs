using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using System.Text.Json;
using Assert = Xunit.Assert;
namespace Nuotti.Contracts.Tests.V1.Enum;

public class RoleTests
{
    [Fact]
    public Task Role_names_are_stable()
    => Verify(System.Enum.GetNames(typeof(Role)), VerifyDefaults.Settings());

    [Fact]
    public Task Role_json_serialization_is_stable()
    {
        Role[] values = System.Enum.GetValues<Role>();
        Dictionary<string, string> map = values.ToDictionary(v => v.ToString(), v =>
        {
            string json = JsonSerializer.Serialize(v, JsonDefaults.Options);
            return json;
        });

        return Verify(map, VerifyDefaults.Settings());
    }

    [Fact]
    public void Role_roundtrip_deserializes_to_same_value()
    {
        foreach (var v in System.Enum.GetValues<Role>())
        {
            var json = JsonSerializer.Serialize(v, JsonDefaults.Options);
            var back = JsonSerializer.Deserialize<Role>(json, JsonDefaults.Options);
            Assert.Equal(v, back);
        }
    }

    [Fact]
    public Task Role_wrapped_object_json_is_stable()
    {
        var payloads = System.Enum.GetValues<Role>()
            .Select(v => new
            {
                role = v
            })
            .ToArray();

        return Verify(payloads, VerifyDefaults.Settings());
    }
}