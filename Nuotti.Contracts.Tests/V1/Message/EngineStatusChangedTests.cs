using FluentAssertions;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Message;

public class EngineStatusChangedTests
{
    [Fact]
    public void EngineStatusChanged_JSON_includes_latencyMs_numeric()
    {
        var evt = new EngineStatusChanged(EngineStatus.Playing, 12.34);
        var json = JsonSerializer.Serialize(evt, ContractsJson.DefaultOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("status", out var statusProp).Should().BeTrue();
        root.TryGetProperty("latencyMs", out var latencyProp).Should().BeTrue();
        latencyProp.ValueKind.Should().Be(JsonValueKind.Number);
        latencyProp.GetDouble().Should().BeApproximately(12.34, 0.001);
    }
}
