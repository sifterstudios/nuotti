using System.Text.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.Contracts.Tests.V1.Message;

public sealed class PlaybackIdentityTests
{
    [Fact]
    public void Play_and_stop_can_target_one_playback_instance_and_control_generation()
    {
        var play = new PlayTrack("https://assets.invalid/backing.wav")
        {
            SessionCode = "SHOW1234",
            IssuedByRole = Role.Performer,
            IssuedById = "performer-1",
            PlaybackInstanceId = "playback-1",
            ControlGeneration = new ControlGeneration(8)
        };
        var stop = new StopTrack
        {
            SessionCode = "SHOW1234",
            IssuedByRole = Role.Performer,
            IssuedById = "performer-1",
            PlaybackInstanceId = play.PlaybackInstanceId,
            ControlGeneration = play.ControlGeneration
        };

        Assert.Equal("playback-1", stop.PlaybackInstanceId);
        Assert.Equal(8, stop.ControlGeneration!.Value.Value);

        var json = JsonSerializer.Serialize(play, ContractsJson.RestOptions);
        Assert.Contains("\"controlGeneration\":\"8\"", json);
    }

    [Fact]
    public void Legacy_playback_payloads_remain_valid_when_identity_is_absent()
    {
        var play = new PlayTrack("https://assets.invalid/backing.wav")
        {
            SessionCode = "SHOW1234",
            IssuedByRole = Role.Performer,
            IssuedById = "performer-1"
        };

        var json = JsonSerializer.Serialize(play, ContractsJson.RestOptions);

        Assert.Null(play.PlaybackInstanceId);
        Assert.Null(play.ControlGeneration);
        Assert.Contains("\"fileUrl\"", json);
        Assert.DoesNotContain("playbackInstanceId", json);
        Assert.DoesNotContain("controlGeneration", json);
    }
}
