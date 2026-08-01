using Nuotti.Backend.Participants;

namespace Nuotti.Backend.Tests;

public sealed class ParticipantIdentityStoreTests
{
    readonly InMemoryParticipantIdentityStore _store = new();

    [Fact]
    public void Same_device_reconnects_to_same_Participant_within_Session()
    {
        var first = _store.JoinOrRestore("SESS01", "device-secret-a", "Alex");
        var second = _store.JoinOrRestore("SESS01", "device-secret-a", "Alex");

        Assert.Equal(first.ParticipantId, second.ParticipantId);
        Assert.Equal("SESS01", second.SessionCode);
        Assert.Equal("Alex", second.DisplayName);
    }

    [Fact]
    public void Same_device_in_different_Session_gets_new_Participant()
    {
        var first = _store.JoinOrRestore("SESS01", "device-secret-a", "Alex");
        var second = _store.JoinOrRestore("SESS02", "device-secret-a", "Alex");

        Assert.NotEqual(first.ParticipantId, second.ParticipantId);
        Assert.Equal("SESS02", second.SessionCode);
    }

    [Fact]
    public void TryGet_rejects_Participant_bound_to_another_Session()
    {
        var bound = _store.JoinOrRestore("SESS01", "device-secret-a", "Alex");

        Assert.False(_store.TryGet("SESS02", bound.ParticipantId, out _));
        Assert.True(_store.TryGet("SESS01", bound.ParticipantId, out var found));
        Assert.Equal(bound.ParticipantId, found!.ParticipantId);
    }

    [Fact]
    public void Performer_can_moderate_display_name()
    {
        var participant = _store.JoinOrRestore("SESS01", "device-secret-a", "BadWord");
        Assert.True(_store.TryModerateName("SESS01", participant.ParticipantId, "CleanName", out var moderated));
        Assert.Equal("CleanName", moderated!.DisplayName);
        Assert.True(moderated.NameIsModerated);
    }

    [Fact]
    public void Join_rejects_empty_or_profane_names()
    {
        Assert.Throws<ArgumentException>(() => _store.JoinOrRestore("SESS01", "device-secret-a", "x"));
        Assert.Throws<ArgumentException>(() => _store.JoinOrRestore("SESS01", "device-secret-a", "damn idiot"));
    }
}
