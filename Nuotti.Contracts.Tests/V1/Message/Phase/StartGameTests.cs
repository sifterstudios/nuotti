using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using System.Text.Json;

namespace Nuotti.Contracts.Tests.V1.Message.Phase;

public class StartGameTests
{
    static StartGame CreateSample()
        => new StartGame
        {
            CommandId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            SessionCode = "SESS-42",
            IssuedByRole = Role.Audience,
            IssuedById = "user-abc",
            IssuedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

    [Fact]
    public void AllowedPhases_ShouldContainExactly_Lobby_And_Finished()
    {
        var sut = CreateSample();

        Assert.Contains(Nuotti.Contracts.V1.Enum.Phase.Lobby, sut.AllowedPhases);
        Assert.Contains(Nuotti.Contracts.V1.Enum.Phase.Finished, sut.AllowedPhases);
        Assert.Equal(2, sut.AllowedPhases.Count);
    }

    [Fact]
    public Task StartGame_Serializes_AsExpected()
    {
        var sut = CreateSample();
        var json = JsonSerializer.Serialize(sut, JsonDefaults.Options);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}