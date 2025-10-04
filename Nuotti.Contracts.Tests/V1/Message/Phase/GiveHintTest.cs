using JetBrains.Annotations;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Message.Phase;

[TestSubject(typeof(GiveHint))]
public class GiveHintTest
{

    static GiveHint CreateSample()
        => new GiveHint(new Hint(0, null, null, new SongId("song-123")))
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
    public void AllowedPhases_ShouldContainExactly_Guessing()
    {
        var sut = CreateSample();

        Assert.Contains(Nuotti.Contracts.V1.Enum.Phase.Guessing, sut.AllowedPhases);
        Assert.Single(sut.AllowedPhases);
    }

    [Fact]
    public Task SubmitAnswer_Serializes_AsExpected()
    {
        var sut = CreateSample();
        var json = JsonSerializer.Serialize(sut, ContractsJson.DefaultOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}