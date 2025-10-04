using JetBrains.Annotations;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using System.Text.Json;

namespace Nuotti.Contracts.Tests.V1.Message.Phase;

[TestSubject(typeof(CreateSession))]
public class CreateSessionTest
{
    static CreateSession CreateSample()
        => new CreateSession("SESSION-123")
        {
            CommandId = Guid.Parse("abcde123-4567-890a-bcde-1234567890ab"),
            SessionCode = "CODE-456",
            IssuedByRole = Role.Performer,
            IssuedById = "user-456",
            IssuedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        // Arrange
        var sessionId = "SESSION-123";

        // Act
        var sut = CreateSample();

        // Assert
        Assert.Equal(sessionId, sut.SessionId);
        Assert.Equal(Guid.Parse("abcde123-4567-890a-bcde-1234567890ab"), sut.CommandId);
        Assert.Equal("CODE-456", sut.SessionCode);
        Assert.Equal(Role.Performer, sut.IssuedByRole);
        Assert.Equal("user-456", sut.IssuedById);
    }

    [Fact]
    public void AllowedPhases_ShouldContainExactly_Idle()
    {
        // Arrange
        var sut = CreateSample();

        // Act & Assert
        Assert.Contains(Nuotti.Contracts.V1.Enum.Phase.Idle, sut.AllowedPhases);
        Assert.Single(sut.AllowedPhases);
    }

    [Fact]
    public Task CreateSession_Serializes_AsExpected()
    {
        // Arrange
        var sut = CreateSample();

        // Act
        var json = JsonSerializer.Serialize(sut, ContractsJson.DefaultOptions);

        // Assert
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}