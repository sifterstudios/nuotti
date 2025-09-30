using Nuotti.Contracts.V1.Enum;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assert = Xunit.Assert;
namespace Nuotti.Contracts.Tests.V1.Enum;

public class RoleTests
{

    static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        Converters =
        {
            new JsonStringEnumConverter()
        },
        WriteIndented = true
    };

    static VerifySettings Snap()
    {
        var snap = new VerifySettings();
        snap.UseDirectory("Snapshots");
        return snap;
    }

    [Fact]
    public Task Role_names_are_stable()
        => Verify(System.Enum.GetNames(typeof(Role)), Snap());

    [Fact]
    public Task Role_json_serialization_is_stable()
    {
        Role[] values = System.Enum.GetValues<Role>();
        Dictionary<string, string> map = values.ToDictionary(v => v.ToString(), v =>
        {
            string json = JsonSerializer.Serialize(v, jsonOptions);
            return json;
        });

        return Verify(map, Snap());
    }

    [Fact]
    public void Role_roundtrip_deserializes_to_same_value()
    {
        foreach (var v in System.Enum.GetValues<Role>())
        {
            var json = JsonSerializer.Serialize(v, jsonOptions);
            var back = JsonSerializer.Deserialize<Role>(json, jsonOptions);
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

        return Verify(payloads, Snap());
    }
}