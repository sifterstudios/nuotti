using System.Text.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Contracts.Tests.V1.Json;

public class SerializationPolicyTests
{
    private static GameStateSnapshot Sample()
        => new(
            sessionCode: "ABCD",
            phase: Phase.Guessing,
            songIndex: 2,
            currentSong: new SongRef(new SongId("song-42"), "Song Title", "Artist"),
            choices: ["A", "B", "C"],
            hintIndex: 1,
            tallies: [1, 2, 3],
            scores: new Dictionary<string, int> { ["p1"] = 10, ["p2"] = 5 },
            songStartedAtUtc: new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc)
        );

    [Fact]
    public Task Rest_policy_uses_camelCase_property_names()
    {
        var sut = Sample();
        var json = JsonSerializer.Serialize(sut, ContractsJson.RestOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }

    [Fact]
    public Task Hub_policy_uses_PascalCase_property_names()
    {
        var sut = Sample();
        var json = JsonSerializer.Serialize(sut, ContractsJson.HubOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}
