using FluentAssertions;
using Nuotti.AudioEngine.Playback;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class PlayCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_InvokesPlayerPlay()
    {
        var player = new NoopPlayer();
        var handler = new PlayCommandHandler(player);
        bool started = false;
        player.Started += (_, __) => started = true;

        var cmd = new PlayTrack("http://example.com/test.mp3")
                {
                    SessionCode = "test",
                    IssuedByRole = Role.Performer,
                    IssuedById = "tester"
                };
                await handler.HandleAsync(cmd);

        started.Should().BeTrue("player should signal start");
        player.IsPlaying.Should().BeTrue();
    }
}