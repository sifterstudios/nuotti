using Nuotti.Contracts.V1.Enum;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assert = Xunit.Assert;
namespace Nuotti.Contracts.Tests.V1.Enum;

public class ReasonCodeTests
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
    public Task ReasonCode_names_are_stable()
        => Verify(System.Enum.GetNames(typeof(ReasonCode)), Snap());

    [Fact]
    public Task ReasonCode_json_serialization_is_stable()
    {
        var values = System.Enum.GetValues<ReasonCode>();
        var map = values.ToDictionary(v => v.ToString(), v =>
        {
            var json = JsonSerializer.Serialize(v, jsonOptions);
            return json;
        });
        return Verify(map, Snap());
    }

    [Fact]
    public void ReasonCode_roundtrip_deserializes_to_same_value()
    {
        foreach (var v in System.Enum.GetValues<ReasonCode>())
        {
            var json = JsonSerializer.Serialize(v, jsonOptions);
            var back = JsonSerializer.Deserialize<ReasonCode>(json, jsonOptions);
            Assert.Equal(v, back);
        }
    }
}