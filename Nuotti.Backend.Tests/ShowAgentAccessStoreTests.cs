using Nuotti.Backend.ShowAgents;

namespace Nuotti.Backend.Tests;

public sealed class ShowAgentAccessStoreTests
{
    [Fact]
    public async Task Distributed_prefix_rotation_hits_global_pairing_budget()
    {
        var store = new InMemoryShowAgentAccessStore();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var code = $"{attempt % 100:D2}{attempt:D6}";
            Assert.Null(await store.PairAsync(code, "Guesser"));
        }

        await Assert.ThrowsAsync<ShowAgentPairingThrottledException>(
            () => store.PairAsync("99123456", "Guesser"));
    }

    [Fact]
    public async Task Offline_playback_commands_compact_to_latest_desired_state()
    {
        var store = new InMemoryShowAgentAccessStore();
        await store.AppendCommandAsync("ws", "SHOW", "PlayTrack", new { fileUrl = "old.wav" });
        await store.AppendCommandAsync("ws", "SHOW", "StopTrack", new { });
        var code = await store.IssuePairingCodeAsync("ws", "SHOW", "owner");
        var paired = (await store.PairAsync(code.Code, "Agent"))!;
        await store.AppendCommandAsync("ws", "SHOW", "PlayTrack", new { fileUrl = "current.wav" });
        var lease = (await store.AuthenticateAsync(paired.AccessToken))!;

        var commands = await store.ReadCommandsAsync(lease, 0);

        var command = Assert.Single(commands!);
        Assert.Equal(3, command.Sequence);
        Assert.Equal("PlayTrack", command.MessageType);
    }
}
