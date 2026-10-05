using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;
using Nuotti.Contracts.V1.Reducer;
using Xunit;

namespace Nuotti.Backend.Tests;

/// <summary>
/// SessionCommitPlan is the test surface for durable vs live vs ADR 0002 side-commit branching.
/// </summary>
public class SessionCommitPlanTests
{
    static readonly Guid CommandId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    static GameStateSnapshot Snap(string session = "S1") => GameReducer.Initial(session);

    [Fact]
    public void Durable_idempotent_command_commits_durable_and_publishes_relays_live()
    {
        var phase = new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start,
            SessionCode = "S1",
            CorrelationId = CommandId,
            CausedByCommandId = CommandId
        };
        var play = new PlayTrack("track.wav")
        {
            SessionCode = "S1",
            IssuedByRole = Role.Performer,
            IssuedById = "p1",
            CommandId = CommandId
        };

        var plan = SessionCommitPlan.Build(
            checkIdempotency: true,
            publications: [phase, play],
            next: Snap(),
            stateChanged: true,
            expectedSequence: SessionSequence.None,
            durableConfigured: true,
            workspaceId: "ws-1",
            commandId: CommandId);

        Assert.True(plan.UsePrimaryDurableCommit);
        Assert.False(plan.SideCommitDurableWithFreshCommandId);
        Assert.Equal(CommandId, plan.CommandIdForDurableCommit);
        Assert.Single(plan.DurablePublications);
        Assert.IsType<GamePhaseChanged>(plan.DurablePublications[0]);
        Assert.Single(plan.LivePublications);
        Assert.IsType<PlayTrack>(plan.LivePublications[0]);
    }

    [Fact]
    public void PlayTrack_alone_is_live_only_with_no_durable_side_commit()
    {
        var play = new PlayTrack("track.wav")
        {
            SessionCode = "S1",
            IssuedByRole = Role.Performer,
            IssuedById = "p1",
            CommandId = CommandId
        };

        var plan = SessionCommitPlan.Build(
            checkIdempotency: false,
            publications: [play],
            next: Snap(),
            stateChanged: false,
            expectedSequence: SessionSequence.None,
            durableConfigured: true,
            workspaceId: "ws-1",
            commandId: CommandId);

        Assert.False(plan.UsePrimaryDurableCommit);
        Assert.False(plan.SideCommitDurableWithFreshCommandId);
        Assert.Empty(plan.DurablePublications);
        Assert.Single(plan.LivePublications);
    }

    [Fact]
    public void QuestionPushed_with_workspace_side_commits_durable_under_a_fresh_CommandId()
    {
        var pushed = new QuestionPushed("Q?", ["a", "b"])
        {
            SessionCode = "S1",
            IssuedByRole = Role.Performer,
            IssuedById = "p1",
            CommandId = CommandId
        };
        var offered = new QuestionOffered("Q?", ["a", "b"])
        {
            Text = "Q?",
            Choices = ["a", "b"],
            SessionCode = "S1",
            CorrelationId = CommandId,
            CausedByCommandId = CommandId
        };

        var plan = SessionCommitPlan.Build(
            checkIdempotency: false,
            publications: [pushed, offered],
            next: Snap() with { Choices = ["a", "b"] },
            stateChanged: true,
            expectedSequence: SessionSequence.None,
            durableConfigured: true,
            workspaceId: "ws-1",
            commandId: CommandId);

        Assert.False(plan.UsePrimaryDurableCommit);
        Assert.True(plan.SideCommitDurableWithFreshCommandId);
        Assert.NotEqual(CommandId, plan.CommandIdForDurableCommit);
        Assert.Equal(2, plan.LivePublications.Count);
        Assert.Single(plan.DurablePublications);
        Assert.IsType<QuestionOffered>(plan.DurablePublications[0]);
    }

    [Fact]
    public void QuestionPushed_on_legacy_workspace_skips_durable_side_commit()
    {
        var pushed = new QuestionPushed("Q?", ["a", "b"])
        {
            SessionCode = "S1",
            IssuedByRole = Role.Performer,
            IssuedById = "p1",
            CommandId = CommandId
        };
        var offered = new QuestionOffered("Q?", ["a", "b"])
        {
            Text = "Q?",
            Choices = ["a", "b"],
            SessionCode = "S1",
            CorrelationId = CommandId,
            CausedByCommandId = CommandId
        };

        var plan = SessionCommitPlan.Build(
            checkIdempotency: false,
            publications: [pushed, offered],
            next: Snap() with { Choices = ["a", "b"] },
            stateChanged: true,
            expectedSequence: SessionSequence.None,
            durableConfigured: true,
            workspaceId: "legacy",
            commandId: CommandId);

        Assert.False(plan.SideCommitDurableWithFreshCommandId);
        Assert.Empty(plan.DurablePublications);
    }
}
