using FluentAssertions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class HubWireNamesTests
{
    [Theory]
    [InlineData(typeof(GameStateSnapshot), "GameStateChanged")]
    [InlineData(typeof(AnswerSubmitted), "AnswerSubmitted")]
    [InlineData(typeof(QuestionPushed), "QuestionPushed")]
    [InlineData(typeof(PlayTrack), "PlayTrack")]
    [InlineData(typeof(StopTrack), "Stop")]
    public void Maps_each_payload_type_to_the_name_the_backend_actually_sends(Type payload, string expected)
    {
        HubWireNames.ByPayloadType[payload].Should().Be(expected);
    }

    [Fact]
    public void StopTrack_is_not_named_after_its_type()
    {
        // Guarding the specific trap: HubBroadcastSubscriber sends StopTrack as "Stop".
        // Deriving the method name from typeof(T).Name would produce a subscription that
        // silently never fires.
        HubWireNames.ByPayloadType[typeof(StopTrack)].Should().NotBe(nameof(StopTrack));
    }

    [Fact]
    public void An_unmapped_payload_type_is_rejected_rather_than_guessed()
    {
        var act = () => HubWireNames.For<HubWireNamesTests>();

        act.Should().Throw<NotSupportedException>();
    }
}
