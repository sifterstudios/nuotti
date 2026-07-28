using FluentAssertions;
using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.InProc.Tests;

public class InProcCommandEmitterTests
{
    static Actor Performer => Actor.Verified(Role.Performer, "perf-1");
    static Actor Audience => Actor.Verified(Role.Audience, "aud-1");

    [Fact]
    public async Task Applies_a_command_through_the_real_processor()
    {
        using var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor, Performer);

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
        var wrongRole = new InProcCommandEmitter(backend.Processor, Audience);

        var act = async () => await wrongRole.EmitAsync(
            new StartGame { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" });

        await act.Should().ThrowAsync<CommandRejectedException>();
    }

    [Fact]
    public async Task Duplicate_command_ids_do_not_throw()
    {
        using var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor, Performer);
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
        var emitter = new InProcCommandEmitter(backend.Processor, Performer);

        await emitter.EmitAsync(new CreateSession("dev")
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
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
