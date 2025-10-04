using JetBrains.Annotations;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;

namespace Nuotti.Contracts.Tests.V1.Message.Phase;

[TestSubject(typeof(RevealAnswer))]
public class RevealAnswerTest
{
    static RevealAnswer CreateSample()
        => new RevealAnswer(new SongRef(new SongId("song-123"), "Sample Title", "Sample Artist"))
        {
            CommandId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            SessionCode = "SESSION-123",
            IssuedByRole = Role.Performer,
            IssuedById = "performer-123",
            IssuedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
        };

    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        var sut = CreateSample();

        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"), sut.CommandId);
        Assert.Equal("SESSION-123", sut.SessionCode);
        Assert.Equal(Role.Performer, sut.IssuedByRole);
        Assert.Equal("performer-123", sut.IssuedById);

        Assert.Equal("song-123", sut.SongRef.Id.Value);
        Assert.Equal("Sample Title", sut.SongRef.Title);
        Assert.Equal("Sample Artist", sut.SongRef.Artist);
    }

    [Fact]
    public void AllowedPhases_ShouldContainExactly_Lock()
    {
        var sut = CreateSample();

        Assert.Contains(Nuotti.Contracts.V1.Enum.Phase.Lock, sut.AllowedPhases);
        Assert.Single(sut.AllowedPhases);
    }

    [Fact]
    public Task RevealAnswer_Serializes_AsExpected()
    {
        var sut = CreateSample();
        var json = JsonSerializer.Serialize(sut, ContractsJson.DefaultOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}