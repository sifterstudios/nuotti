using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Message;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Message;

public class EventBaseTests
{
    [Fact]
    public Task EventBase_RoundTrips_WithoutLoss()
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        var cmdId = Guid.NewGuid();
        var original = new EventBase(
            EventId: Guid.NewGuid(),
            CorrelationId: cmdId,
            CausedByCommandId: cmdId,
            SessionCode: "SESS-42",
            EmittedAtUtc: now
        );

        var json = JsonSerializer.Serialize(original, JsonDefaults.Options);

        return VerifyJson(json, VerifyDefaults.Settings());
    }
}