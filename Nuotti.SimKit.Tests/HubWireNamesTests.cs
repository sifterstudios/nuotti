using FluentAssertions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
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
        HubWireContract.SessionBroadcasts[payload].Should().Be(expected);
        HubWireNames.ByPayloadType[payload].Should().Be(expected);
    }

    [Fact]
    public void StopTrack_is_not_named_after_its_type_on_the_session_wire()
    {
        HubWireContract.SessionBroadcasts[typeof(StopTrack)].Should().Be("Stop");
        HubWireContract.ShowAgentCommands[typeof(StopTrack)].Should().Be("StopTrack");
    }

    [Fact]
    public void PreparePlayback_is_Show_Agent_only()
    {
        HubWireContract.ShowAgentCommands[typeof(PreparePlayback)].Should().Be("Prepare");
        HubWireContract.SessionBroadcasts.ContainsKey(typeof(PreparePlayback)).Should().BeFalse();
    }

    [Fact]
    public void An_unmapped_payload_type_is_rejected_rather_than_guessed()
    {
        var act = () => HubWireContract.SessionMethodFor<HubWireNamesTests>();

        act.Should().Throw<NotSupportedException>();
    }
}
