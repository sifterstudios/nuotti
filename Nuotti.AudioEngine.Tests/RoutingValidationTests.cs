using FluentAssertions;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class RoutingValidationTests
{
    [Fact]
    public void Invalid_channel_index_is_rejected()
    {
        // Device with 2 channels
        var routing = new RoutingOptions
        {
            Tracks = new[] { 1, 2 },
            Click = new[] { 3 } // invalid for 2-channel device
        };

        var result = RoutingValidator.ValidateAgainstDeviceChannels(routing, deviceChannelCount: 2);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Valid_routing_passes_validation()
    {
        var routing = new RoutingOptions
        {
            Tracks = new[] { 1, 2 },
            Click = new[] { 1 }
        };
        var result = RoutingValidator.ValidateAgainstDeviceChannels(routing, deviceChannelCount: 2);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
