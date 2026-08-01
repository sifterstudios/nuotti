using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.InProc.Tests;

public class InProcCommandEmitterTests
{
    [Fact]
    public async Task Applies_a_command_through_the_real_processor()
    {
        using var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor);

        await emitter.EmitAsync(new CreateSession("dev") { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" });

        // A freshly created session already reads as Lobby (GameReducer.Initial), not Idle, so
        // asserting NotBe(Idle) here would pass even if StartGame were never applied. Assert the
        // exact phase StartGame produces (its TargetPhase) to prove the second command landed.
        backend.States.TryGet("dev", out var afterCreate).Should().BeTrue();
        afterCreate.Phase.Should().Be(Phase.Lobby);

        await emitter.EmitAsync(new StartGame { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" });

        backend.States.TryGet("dev", out var state).Should().BeTrue();
        state.Should().NotBeNull();
        state.Phase.Should().Be(Phase.Start);
    }

    [Fact]
    public async Task Throws_when_the_processor_rejects_the_command()
    {
        using var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor);
        await emitter.EmitAsync(new CreateSession("dev") { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" });

        // A wrong-role command would also reject, but wrong-role rejection depends on which
        // Actor the emitter constructs - and that used to be the bug (fixed constructor Actor vs.
        // HTTP's Actor.Claimed(command) could disagree). LockAnswers is only allowed in Guessing
        // (IPhaseRestricted.AllowedPhases); the session is still in Lobby (GameReducer.Initial)
        // right after CreateSession, and the role here (Performer) is the one LockAnswers
        // requires. So this rejection comes from SessionCommandProcessor's phase guard, not the
        // role check - a mechanism with no Actor-identity dependency at all, and therefore
        // identical whether the command travels through this emitter or over HTTP through
        // PhaseEndpoints into the same SessionCommandProcessor.ApplyAsync.
        var act = async () => await emitter.EmitAsync(
            new LockAnswers { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" });

        var thrown = await act.Should().ThrowAsync<CommandRejectedException>();

        // Assert the specific reason, not just "it threw": that is what proves the rejection came
        // from the phase guard (InvalidStateTransition) and not from something incidental, like an
        // idempotency duplicate (which would not throw) or a reducer-level error.
        thrown.Which.Problem.Should().NotBeNull();
        thrown.Which.Problem!.Reason.Should().Be(ReasonCode.InvalidStateTransition);
    }

    [Fact]
    public async Task Rejection_carries_the_same_structured_reason_a_wrong_role_would_get_over_http()
    {
        // Companion to Throws_when_the_processor_rejects_the_command above: that test proves a
        // phase-guard rejection is fidelity-independent because it does not depend on which Actor
        // the emitter builds. This test proves the *other* rejection path - a wrong role - now
        // agrees with HTTP too, because InProcCommandEmitter derives Actor.Claimed(command) from
        // the command body exactly as PhaseEndpoints does, instead of a constructor-fixed Actor.
        using var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor);
        await emitter.EmitAsync(new CreateSession("dev") { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" });

        var act = async () => await emitter.EmitAsync(
            new StartGame { SessionCode = "dev", IssuedByRole = Role.Audience, IssuedById = "aud-1" });

        var thrown = await act.Should().ThrowAsync<CommandRejectedException>();
        thrown.Which.Problem.Should().NotBeNull();
        thrown.Which.Problem!.Reason.Should().Be(ReasonCode.UnauthorizedRole);
    }

    [Fact]
    public async Task Rejects_a_command_type_with_no_phase_route()
    {
        // Mirrors HttpCommandEmitterTests.Rejects_a_command_type_with_no_phase_route. Before this
        // fix, InProcCommandEmitter forwarded SubmitAnswer straight to the processor, which has no
        // effects mapped for it and silently returned Applied - so the same command "succeeded"
        // in-proc and threw NotSupportedException over HTTP. Both emitters must now agree.
        using var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor);

        var act = async () => await emitter.EmitAsync(new SubmitAnswer(null, 0)
        {
            SessionCode = "dev",
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1"
        });

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Duplicate_command_ids_do_not_throw()
    {
        using var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor);
        var create = new CreateSession("dev") { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" };

        await emitter.EmitAsync(create);
        var act = async () => await emitter.EmitAsync(create);

        // The same CommandId twice is an idempotency hit, not a rejection.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Performer_script_drives_a_session_end_to_end_with_no_network()
    {
        using var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor);

        await emitter.EmitAsync(new CreateSession("dev")
        {
            SessionCode = "dev",
            IssuedByRole = Role.Performer,
            IssuedById = "perf-1"
        });

        // Before the script runs, the session sits in Lobby (GameReducer.Initial). Only the
        // script's StartSet step (-> StartGame -> Phase.Start) can move it past that, so
        // asserting the exact post-script phase is what proves the script — not just
        // CreateSession — drove the session.
        backend.States.TryGet("dev", out var beforeScript).Should().BeTrue();
        beforeScript.Phase.Should().Be(Phase.Lobby);

        var script = new Nuotti.SimKit.Script.ScriptModel
        {
            Steps =
            {
                new Nuotti.SimKit.Script.ScriptStep { Kind = Nuotti.SimKit.Script.StepKind.StartSet }
            }
        };

        var performer = new Nuotti.SimKit.Actors.PerformerActor(
            hubClientFactory: null!, baseUri: new Uri("http://unused"), session: "dev");

        await performer.RunScriptAsync(script, emitter);

        backend.States.TryGet("dev", out var state).Should().BeTrue();
        state.Phase.Should().Be(Phase.Start);
    }
}
