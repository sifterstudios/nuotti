using Nuotti.Contracts.V1.Enum;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assert = Xunit.Assert;

namespace Nuotti.Contracts.Tests.V1.Enum;

public class PhaseTests
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
    public Task Phase_names_are_stable()
        => Verify(System.Enum.GetNames(typeof(Phase)), Snap());

    [Fact]
    public Task Phase_json_serialization_is_stable()
    {
        var values = System.Enum.GetValues<Phase>();
        var map = values.ToDictionary(v => v.ToString(), v =>
        {
            var json = JsonSerializer.Serialize(v, jsonOptions);
            return json;
        });
        return Verify(map, Snap());
    }

    [Fact]
    public void Phase_roundtrip_deserializes_to_same_value()
    {
        foreach (var v in System.Enum.GetValues<Phase>())
        {
            var json = JsonSerializer.Serialize(v, jsonOptions);
            var back = JsonSerializer.Deserialize<Phase>(json, jsonOptions);
            Assert.Equal(v, back);
        }
    }
}