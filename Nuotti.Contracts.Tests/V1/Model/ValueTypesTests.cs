using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
using Assert = Xunit.Assert;
namespace Nuotti.Contracts.Tests.V1.Model;

public class ValueTypesTests
{
    [Xunit.Theory]
    [MemberData(nameof(Samples))]
    public void Round_trip_serializes_and_deserializes_to_equal_object<T>(T original)
    {
        var json = JsonSerializer.Serialize(original!, JsonDefaults.Options);
        var back = JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> Samples()
    {
        yield return [new SongId("song-123")];
        yield return [new SongRef(new SongId("song-abc"), "Song Title", "Artist Name")];
        yield return [new SongRef(new SongId("song-xyz"), "Untitled", null)];
        yield return [new Choice("A", "Piano")];
        yield return [new Choice("B", "Guitar")];
        yield return [new Hint(0, "Instrument family", null, new SongId("song1"))];
        yield return [new Hint(1, null, "Play lead melody without rhythm", new SongId("song2"))];
        yield return [new Tally("A", 0)];
        yield return [new Tally("B", 42)];
    }

    [Fact]
    public Task Json_shape_is_stable()
    {
        var samples = new Dictionary<string, string>
        {
            ["SongId"] = JsonSerializer.Serialize(new SongId("song-123"), JsonDefaults.Options),
            ["SongRef"] = JsonSerializer.Serialize(new SongRef(new SongId("id-1"), "Song", "Artist"), JsonDefaults.Options),
            ["SongRef_nullArtist"] = JsonSerializer.Serialize(new SongRef(new SongId("id-2"), "Song", null), JsonDefaults.Options),
            ["Choice"] = JsonSerializer.Serialize(new Choice("A", "Text"), JsonDefaults.Options),
            ["Hint"] = JsonSerializer.Serialize(new Hint(0, null, "Text", new SongId("song3")), JsonDefaults.Options),
            ["Tally"] = JsonSerializer.Serialize(new Tally("A", 3), JsonDefaults.Options),
        };
        return Verify(samples, VerifyDefaults.Settings());
    }
}