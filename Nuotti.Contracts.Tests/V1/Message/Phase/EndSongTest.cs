using JetBrains.Annotations;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;

namespace Nuotti.Contracts.Tests.V1.Message.Phase;

[TestSubject(typeof(EndSong))]
public class EndSongTest
{
    static EndSong CreateSample()
        => new EndSong(new SongId("song-123"))
        {
            CommandId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            SessionCode = "SESSION-99",
            IssuedByRole = Role.Performer,
            IssuedById = "user-xyz",
            IssuedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        var sut = CreateSample();

        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"), sut.CommandId);
        Assert.Equal("SESSION-99", sut.SessionCode);
        Assert.Equal(Role.Performer, sut.IssuedByRole);
        Assert.Equal("user-xyz", sut.IssuedById);
        Assert.Equal("song-123", sut.SongId.Value);
    }

    [Fact]
    public void AllowedPhases_ShouldContainExactly_Play()
    {
        var sut = CreateSample();

        Assert.Contains(Nuotti.Contracts.V1.Enum.Phase.Play, sut.AllowedPhases);
        Assert.Single(sut.AllowedPhases);
    }

    [Fact]
    public Task EndSong_Serializes_AsExpected()
    {
        var sut = CreateSample();
        var json = JsonSerializer.Serialize(sut, ContractsJson.DefaultOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}