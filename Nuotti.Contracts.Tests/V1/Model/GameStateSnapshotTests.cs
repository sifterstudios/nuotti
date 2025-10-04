using System.Text.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Contracts.Tests.V1.Model;

public class GameStateSnapshotTests
{
    [Fact]
    public Task GameStateSnapshot_RoundTrip_Snapshot()
    {
        var sut = new GameStateSnapshot(
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

        var json = JsonSerializer.Serialize(sut, JsonDefaults.Options);
        return VerifyJson(json, VerifyDefaults.Settings());
    }

    [Fact]
    public void Missing_optional_fields_deserialize_with_defaults()
    {
        // Arrange: omit CurrentSong, Choices, Tallies, Scores, SongStartedAtUtc
        var json = "{" +
                   "\"sessionCode\":\"CODE\"," +
                   "\"phase\":\"start\"," +
                   "\"songIndex\":0," +
                   "\"hintIndex\":0" +
                   "}";

        // Act
        var back = JsonSerializer.Deserialize<GameStateSnapshot>(json, JsonDefaults.Options)!;

        // Assert
        Assert.NotNull(back);
        Assert.Equal("CODE", back.SessionCode);
        Assert.Equal(Phase.Start, back.Phase);
        Assert.Equal(0, back.SongIndex);
        Assert.Null(back.CurrentSong);
        Assert.NotNull(back.Choices);
        Assert.Empty(back.Choices);
        Assert.NotNull(back.Tallies);
        Assert.Empty(back.Tallies);
        Assert.NotNull(back.Scores);
        Assert.Empty(back.Scores);
        Assert.Null(back.SongStartedAtUtc);
    }
}