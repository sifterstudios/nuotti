using JetBrains.Annotations;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Message.Phase;

[TestSubject(typeof(NextRound))]
public class NextRoundTest
{

    static NextRound CreateSample()
        => new NextRound(new SongId("song-123"))
        {
            CommandId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc"),
            SessionCode = "SESSION-101",
            IssuedByRole = Role.Audience,
            IssuedById = "user-789",
            IssuedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        var sut = CreateSample();

        Verify(sut, VerifyDefaults.Settings());
    }

    [Fact]
    public void AllowedPhases_ShouldContain_Intermission_And_Guessing()
    {
        var sut = CreateSample();

        // Intermission is the normal path - it is where EndSong leaves a round, and nothing could
        // leave it before. Guessing remains, for abandoning a round and skipping to the next song.
        Assert.Contains(Nuotti.Contracts.V1.Enum.Phase.Intermission, sut.AllowedPhases);
        Assert.Contains(Nuotti.Contracts.V1.Enum.Phase.Guessing, sut.AllowedPhases);
        Assert.Equal(2, sut.AllowedPhases.Count);
    }

    [Fact]
    public Task SubmitAnswer_Serializes_AsExpected()
    {
        var sut = CreateSample();
        var json = JsonSerializer.Serialize(sut, ContractsJson.DefaultOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}
