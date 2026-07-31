using Nuotti.Backend.Eventing;
using Nuotti.Backend.Persistence;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.Backend.Tests;

public sealed class WorkspacePublicationTests
{
    [Fact]
    public async Task Answer_and_relay_messages_keep_their_workspace_routing_envelope()
    {
        var bus = new InMemoryEventBus();
        var routed = new List<SessionMessagePublisher.WorkspacePublication>();
        using var subscription = bus.Subscribe<SessionMessagePublisher.WorkspacePublication>((message, _) =>
        {
            routed.Add(message);
            return Task.CompletedTask;
        });

        var answer = new AnswerSubmitted("audience-1", 2)
        {
            AudienceId = "audience-1",
            ChoiceIndex = 2,
            SessionCode = "SHOW42",
            CorrelationId = Guid.NewGuid(),
            CausedByCommandId = Guid.NewGuid()
        };
        var play = new PlayTrack("track.wav")
        {
            SessionCode = "SHOW42",
            IssuedByRole = Role.Performer,
            IssuedById = "performer-1"
        };

        await SessionMessagePublisher.PublishAsync(bus, answer, workspaceId: "ws_band");
        await SessionMessagePublisher.PublishAsync(bus, play, workspaceId: "ws_band");

        Assert.Collection(routed,
            message =>
            {
                Assert.Equal(("ws_band", "SHOW42"), (message.WorkspaceId, message.SessionCode));
                Assert.IsType<AnswerSubmitted>(message.Payload);
            },
            message =>
            {
                Assert.Equal(("ws_band", "SHOW42"), (message.WorkspaceId, message.SessionCode));
                Assert.IsType<PlayTrack>(message.Payload);
            });
    }
}
