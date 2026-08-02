using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;
using Nuotti.Backend.Participants;
using Nuotti.Backend.Realtime;
using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Backend.Tests;

/// <summary>
/// Realtime authorization. QuizHub previously took a role as a string argument and believed it,
/// which is the reason it could only ever be mapped in Development. These tests pin the rule that
/// replaced that: capabilities come from a credential, and an unrecognised credential gets nothing.
/// </summary>
public sealed class ConnectionPrincipalResolverTests
{
    static (ConnectionPrincipalResolver Resolver, InMemoryWorkspaceAccessStore Workspaces,
            InMemoryShowAgentAccessStore Devices, InMemoryAudienceJoinStore Audience) Create()
    {
        var options = Options.Create(new NuottiOptions());
        var workspaces = new InMemoryWorkspaceAccessStore();
        var devices = new InMemoryShowAgentAccessStore();
        var audience = new InMemoryAudienceJoinStore(new InMemoryParticipantIdentityStore());
        return (new ConnectionPrincipalResolver(workspaces, devices, audience), workspaces, devices, audience);
    }

    static async Task<(string SessionToken, string WorkspaceId, string UserId)> SignedInOwnerAsync(
        InMemoryWorkspaceAccessStore workspaces, string email = "owner@example.test")
    {
        var link = await workspaces.IssueSignInAsync(email);
        var redeemed = await workspaces.RedeemAsync(link.Token);
        var access = await workspaces.CreateWorkspaceAsync(redeemed!.Principal, "The Satellites");
        await workspaces.SelectAsync(redeemed.Principal, access.WorkspaceId);
        return (redeemed.SessionToken, access.WorkspaceId, redeemed.Principal.UserId);
    }

    [Fact]
    public async Task An_unknown_token_resolves_to_nothing()
    {
        var (resolver, _, _, _) = Create();

        var principal = await resolver.ResolveAsync(new RealtimeConnectionRequest("not-a-token", "SESS", null, null));

        Assert.Null(principal);
    }

    [Fact]
    public async Task A_connection_with_no_token_resolves_to_nothing()
    {
        // There is deliberately no anonymous fallback: this is what QuizHub.Join used to allow.
        var (resolver, _, _, _) = Create();

        Assert.Null(await resolver.ResolveAsync(new RealtimeConnectionRequest(null, "SESS", null, null)));
        Assert.Null(await resolver.ResolveAsync(new RealtimeConnectionRequest("", "SESS", null, null)));
    }

    [Fact]
    public async Task An_audience_token_can_answer_but_cannot_drive_the_game()
    {
        var (resolver, _, _, audience) = Create();
        var ticket = await audience.JoinAsync("SESS", "device-secret-0123456789");

        var principal = await resolver.ResolveAsync(new RealtimeConnectionRequest(ticket.Token, "SESS", null, null));

        Assert.NotNull(principal);
        Assert.Equal(PrincipalKind.AudienceParticipant, principal!.Kind);
        Assert.True(principal.Can(Capability.SubmitAnswer));
        Assert.True(principal.Can(Capability.Subscribe));
        Assert.False(principal.Can(Capability.IssueGameCommand));
        Assert.False(principal.Can(Capability.ReportDeviceStatus));
    }

    [Fact]
    public async Task An_audience_token_is_useless_against_a_different_session()
    {
        // Otherwise one join code would be a skeleton key for every concurrent show.
        var (resolver, _, _, audience) = Create();
        var ticket = await audience.JoinAsync("SESS", "device-secret-0123456789");

        Assert.Null(await resolver.ResolveAsync(new RealtimeConnectionRequest(ticket.Token, "OTHER", null, null)));
    }

    [Fact]
    public async Task The_same_device_secret_comes_back_as_the_same_participant()
    {
        // A phone that drops off wifi mid-song must not return as a stranger with no score.
        var (_, _, _, audience) = Create();

        var first = await audience.JoinAsync("SESS", "device-secret-0123456789");
        var second = await audience.JoinAsync("SESS", "device-secret-0123456789");

        Assert.Equal(first.ParticipantId, second.ParticipantId);
        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public async Task A_different_device_is_a_different_participant()
    {
        var (_, _, _, audience) = Create();

        var a = await audience.JoinAsync("SESS", "device-secret-aaaaaaaaaa");
        var b = await audience.JoinAsync("SESS", "device-secret-bbbbbbbbbb");

        Assert.NotEqual(a.ParticipantId, b.ParticipantId);
    }

    [Fact]
    public async Task A_workspace_member_may_drive_their_own_session()
    {
        var (resolver, workspaces, _, _) = Create();
        var owner = await SignedInOwnerAsync(workspaces);

        var principal = await resolver.ResolveAsync(
            new RealtimeConnectionRequest(owner.SessionToken, "SESS", owner.WorkspaceId, null));

        Assert.NotNull(principal);
        Assert.Equal(PrincipalKind.WorkspaceUser, principal!.Kind);
        Assert.Equal(Role.Performer, principal.Role);
        Assert.True(principal.Can(Capability.IssueGameCommand));
        Assert.True(principal.Can(Capability.RequestPlayback));
        Assert.False(principal.Can(Capability.SubmitAnswer));
    }

    [Fact]
    public async Task A_workspace_member_cannot_drive_a_workspace_they_have_not_selected()
    {
        // Mirrors RequireSelectedAsync on every workspace-scoped HTTP route: membership is not
        // enough, the principal must have that workspace active.
        //
        // The user owns BOTH workspaces here, so membership passes and only the selection check
        // can reject this. An unrelated workspace id would be refused by the membership lookup
        // instead, and the test would still pass with the selection check deleted.
        var (resolver, workspaces, _, _) = Create();
        var link = await workspaces.IssueSignInAsync("owner@example.test");
        var redeemed = await workspaces.RedeemAsync(link.Token);
        var selected = await workspaces.CreateWorkspaceAsync(redeemed!.Principal, "The Satellites");
        var alsoMine = await workspaces.CreateWorkspaceAsync(redeemed.Principal, "Side Project");
        await workspaces.SelectAsync(redeemed.Principal, selected.WorkspaceId);

        var principal = await resolver.ResolveAsync(
            new RealtimeConnectionRequest(redeemed.SessionToken, "SESS", alsoMine.WorkspaceId, null));

        Assert.Null(principal);
    }

    [Fact]
    public async Task A_workspace_id_the_member_has_nothing_to_do_with_resolves_to_nothing()
    {
        var (resolver, workspaces, _, _) = Create();
        var owner = await SignedInOwnerAsync(workspaces);

        Assert.Null(await resolver.ResolveAsync(
            new RealtimeConnectionRequest(owner.SessionToken, "SESS", "some-other-workspace", null)));
    }

    [Fact]
    public async Task A_workspace_token_without_a_workspace_id_resolves_to_nothing()
    {
        var (resolver, workspaces, _, _) = Create();
        var owner = await SignedInOwnerAsync(workspaces);

        Assert.Null(await resolver.ResolveAsync(new RealtimeConnectionRequest(owner.SessionToken, "SESS", null, null)));
    }

    [Fact]
    public async Task A_paired_venue_device_may_watch_and_report_but_never_command()
    {
        // A projector sitting on a venue's network is the least trusted thing in the system.
        var (resolver, workspaces, devices, _) = Create();
        var owner = await SignedInOwnerAsync(workspaces);
        var pairing = await devices.IssuePairingCodeAsync(owner.WorkspaceId, "SESS", owner.UserId);
        var paired = await devices.PairAsync(pairing.Code, "Stage projector");

        var principal = await resolver.ResolveAsync(
            new RealtimeConnectionRequest(paired!.AccessToken, "SESS", null, "projector"));

        Assert.NotNull(principal);
        Assert.Equal(PrincipalKind.VenueDevice, principal!.Kind);
        Assert.Equal(Role.Projector, principal.Role);
        Assert.True(principal.Can(Capability.Subscribe));
        Assert.True(principal.Can(Capability.ReportDeviceStatus));
        Assert.False(principal.Can(Capability.IssueGameCommand));
        Assert.False(principal.Can(Capability.SubmitAnswer));
    }

    [Fact]
    public async Task A_device_paired_to_one_session_cannot_follow_a_code_into_another()
    {
        var (resolver, workspaces, devices, _) = Create();
        var owner = await SignedInOwnerAsync(workspaces);
        var pairing = await devices.IssuePairingCodeAsync(owner.WorkspaceId, "SESS", owner.UserId);
        var paired = await devices.PairAsync(pairing.Code, "Stage projector");

        Assert.Null(await resolver.ResolveAsync(
            new RealtimeConnectionRequest(paired!.AccessToken, "OTHER", null, "projector")));
    }

    [Fact]
    public async Task A_device_without_a_projector_role_is_treated_as_the_audio_engine()
    {
        var (resolver, workspaces, devices, _) = Create();
        var owner = await SignedInOwnerAsync(workspaces);
        var pairing = await devices.IssuePairingCodeAsync(owner.WorkspaceId, "SESS", owner.UserId);
        var paired = await devices.PairAsync(pairing.Code, "Show agent");

        var principal = await resolver.ResolveAsync(
            new RealtimeConnectionRequest(paired!.AccessToken, "SESS", null, null));

        Assert.Equal(Role.Engine, principal!.Role);
    }

    [Fact]
    public async Task An_expired_audience_token_stops_working()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var audience = new InMemoryAudienceJoinStore(new InMemoryParticipantIdentityStore(), clock);
        var resolver = new ConnectionPrincipalResolver(
            new InMemoryWorkspaceAccessStore(), new InMemoryShowAgentAccessStore(), audience);
        var ticket = await audience.JoinAsync("SESS", "device-secret-0123456789");

        clock.Advance(TimeSpan.FromHours(9));

        Assert.Null(await resolver.ResolveAsync(new RealtimeConnectionRequest(ticket.Token, "SESS", null, null)));
    }

    sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
