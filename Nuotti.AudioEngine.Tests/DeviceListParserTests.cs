using FluentAssertions;
using Nuotti.AudioEngine.AudioDevices;
using System.Text.Json;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class DeviceListParserTests
{
    [Fact]
    public void Deserialize_returns_structured_device_info_from_mocked_backend()
    {
        // Arrange: mocked backend JSON payload
        var json = "{\"defaultDeviceId\":\"dev1\",\"devices\":[{\"id\":\"dev1\",\"name\":\"Speakers (Realtek)\",\"channels\":2},{\"id\":\"dev2\",\"name\":\"HDMI (NVIDIA)\",\"channels\":8}]}";

        // Act
        var result = DeviceListJson.Deserialize(json);

        // Assert
        result.DefaultDeviceId.Should().Be("dev1");
        result.Devices.Should().HaveCount(2);
        result.Devices[0].Id.Should().Be("dev1");
        result.Devices[0].Name.Should().Be("Speakers (Realtek)");
        result.Devices[0].Channels.Should().Be(2);
        result.Devices[1].Id.Should().Be("dev2");
        result.Devices[1].Channels.Should().Be(8);
    }

    [Fact]
    public void Serialize_roundtrips_shape_with_camelCase()
    {
        var model = new DeviceListResult(
            DefaultDeviceId: "default",
            Devices: new []
            {
                new DeviceInfo("default", "System Default", 2)
            }
        );

        var json = DeviceListJson.Serialize(model);

        // Validate camelCase keys exist
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("defaultDeviceId", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("devices", out var devices).Should().BeTrue();
        devices.GetArrayLength().Should().Be(1);
        var d0 = devices[0];
        d0.TryGetProperty("id", out _).Should().BeTrue();
        d0.TryGetProperty("name", out _).Should().BeTrue();
        d0.TryGetProperty("channels", out _).Should().BeTrue();
    }
}
