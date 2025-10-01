using JetBrains.Annotations;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Message.Phase;

[TestSubject(typeof(LockAnswers))]
public class LockAnswersTest
{
    
    static LockAnswers CreateSample()
        => new LockAnswers()
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

        Assert.Equal(Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc"), sut.CommandId);
        Assert.Equal("SESSION-101", sut.SessionCode);
        Assert.Equal(Role.Audience, sut.IssuedByRole);
        Assert.Equal("user-789", sut.IssuedById);
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
        var json = JsonSerializer.Serialize(sut, JsonDefaults.Options);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}