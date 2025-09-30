using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Message;

public class CommandBaseTests
{
    [Fact]
    public Task CommandBase_RoundTrips_WithoutLoss()
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        var original = new CommandBase(
            CommandId: Guid.NewGuid(),
            SessionCode: "SESS-42",
            IssuedByRole: Role.Audience,
            IssuedById: "user-abc",
            IssuedAtUtc: now
        );

        var json = JsonSerializer.Serialize(original, JsonDefaults.Options);

        return VerifyJson(json, VerifyDefaults.Settings());
    }
}