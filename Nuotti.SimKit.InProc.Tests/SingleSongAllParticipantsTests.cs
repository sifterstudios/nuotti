using FluentAssertions;
using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Time;
using Xunit;

namespace Nuotti.SimKit.InProc.Tests;

// Stage 2a's exit criterion: one song driven through a real, no-network backend while a
// projector, an engine and simulated audiences all react through the seam.
//
// REACHABLE PHASE SEQUENCE (established by reading the code, not by trial and error):
// SessionCommandProcessor.Guard requires BOTH IPhaseRestricted.AllowedPhases AND, for
// IPhaseChange commands, AllowedSourcePhases (via IsPhaseChangeAllowed) to accept the current
// phase. Every phase command must declare the same set for both members (enforced by
// PhaseMachineTests.Commands_implementing_both_guards_keep_them_in_step as of this change).
//
// PlaySong previously violated this invariant: AllowedPhases was [Play] while AllowedSourcePhases
// was [Reveal], making the command unsatisfiable from any phase. This was a production defect
// (stage2a fix cc2292b), now fixed: both declare [Reveal]. PlaySong is still not in the script
// below - reaching Play/Intermission/Finished is follow-up work, not part of this exit criterion.
//
// The commands that DO agree with themselves give a fully drivable path to a revealed answer:
// CreateSession (-> Lobby), StartGame (Lobby -> Start), OpenAnswers (Start -> Guessing),
// LockAnswers (Guessing -> Lock), RevealAnswer (Lock -> Reveal). PhaseMachineTests already
// confirms the AllowedSourcePhases half of that chain; the AllowedPhases half was checked here
// directly against SessionCommandProcessor.Guard's source.
//
// RevealAnswer's SongRef comes from UpdateCatalog: UpdateCatalog is not a phase-endpoint command
// (it is absent from HttpCommandEmitter.Routes, so InProcCommandEmitter would refuse it - that is
// by design, the same gate HTTP's fidelity uses), so it is applied directly through
// ISessionCommandProcessor, exactly the way Nuotti.Backend.Endpoints.ApiEndpoints'
// "/api/manifest/{session}" route does. The resulting Catalog[0] is a legitimate, backend-issued
// SongRef, not a fabricated one.
//
// QuestionPushed is handled the same way: it, too, is absent from HttpCommandEmitter.Routes (it
// is a relay - see its own doc comment - not a phase endpoint), so it goes straight through
// ISessionCommandProcessor.ApplyAsync. Unlike PlayTrack/StopTrack it DOES touch state:
// SessionCommandProcessor.EffectsFor now also emits a QuestionOffered event alongside the raw
// relay (stage2a's QuestionOffered fix), and GameReducer's QuestionOffered case is what finally
// populates GameStateSnapshot.Choices and sizes Tallies to match. It is sent after StartGame and
// before OpenAnswers - the phase is still Start - precisely so Choices is already on the snapshot
// the instant OpenAnswers moves the phase to Guessing; AudienceActor.OnStateAsync refuses to act
// on a Guessing snapshot with empty Choices, so sending the question any later would leave every
// audience silent again.
//
// Engine playback (PlayTrack/StopTrack) is handled the same way: both are relay commands, also
// absent from the phase-endpoint route table, so they are applied directly through the processor
// - mirroring the real "/api/play" and "/api/stop" routes - rather than through
// InProcCommandEmitter.
//
// UPDATED PHASE SEQUENCE: adding QuestionPushed between StartGame and OpenAnswers adds one entry
// to the projector's expected sequence, not zero. QuestionPushed is a relay (CommandBase only,
// neither IPhaseRestricted nor IPhaseChange), so Guard never rejects it regardless of phase - but
// SessionCommandProcessor.ApplyAsync still broadcasts a snapshot for it: EffectsFor gives it
// BroadcastSnapshot: true, and the QuestionOffered event it emits genuinely changes state (Choices
// goes from empty to populated, so `stateChanged` at line ~127 is true), so the broadcast fires at
// line 135-138 exactly the way UpdateCatalog's does. The broadcast's Phase is still Start (only
// GamePhaseChanged moves Phase, and this event does not carry one), so the sequence gains an
// extra Start entry, not a new phase value: Lobby, Lobby, Start, Start, Guessing, Lock, Reveal.
public class SingleSongAllParticipantsTests
{
    const string Session = "dev";
    static readonly Uri BaseUri = new("http://in-proc");

    [Fact]
    public async Task Performer_projector_and_engine_all_react_through_the_seam_for_one_song()
    {
        using var backend = new InProcBackend();
        var factory = new InProcHubClientFactory(backend, Session);
        var emitter = new InProcCommandEmitter(backend.Processor);

        var performer = new PerformerActor(factory, BaseUri, Session);
        var projector = new ProjectorActor(factory, BaseUri, Session);
        var engine = new EngineActor(factory, BaseUri, Session, LaneRandom.ForLane(seed: 1, laneIndex: 0));
        var audiences = new[]
        {
            new AudienceActor(factory, BaseUri, Session, "aud-1", LaneRandom.ForLane(seed: 1, laneIndex: 1), timeProvider: new ImmediateTimeProvider()),
            new AudienceActor(factory, BaseUri, Session, "aud-2", LaneRandom.ForLane(seed: 1, laneIndex: 2), timeProvider: new ImmediateTimeProvider()),
            new AudienceActor(factory, BaseUri, Session, "aud-3", LaneRandom.ForLane(seed: 1, laneIndex: 3), timeProvider: new ImmediateTimeProvider()),
        };

        await performer.StartAsync();
        await projector.StartAsync();
        await engine.StartAsync();
        foreach (var audience in audiences) await audience.StartAsync();

        const string issuedBy = "performer-script";

        // The performer's script, phase commands emitted the same way HTTP would send them.
        await emitter.EmitAsync(new CreateSession(Session)
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = issuedBy
        });

        // Not a phase-endpoint command; applied directly, exactly as ApiEndpoints' manifest route
        // does. This is where RevealAnswer's SongRef legitimately comes from.
        var manifest = new SetlistManifest
        {
            Songs = { new SetlistManifest.SongEntry { Title = "Test Song", Artist = "Test Artist", File = "https://example.test/song.mp3" } }
        };
        var updateCatalog = new UpdateCatalog(manifest)
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = issuedBy
        };
        var catalogResult = await backend.Processor.ApplyAsync(Session, Actor.Claimed(updateCatalog), updateCatalog);
        catalogResult.Outcome.Should().Be(Outcome.Applied);
        var songRef = catalogResult.State!.Catalog.Single();

        await emitter.EmitAsync(new StartGame
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = issuedBy
        });

        // QuestionPushed is not a phase-endpoint command either (see file-level comment): applied
        // directly through the processor, exactly as UpdateCatalog/PlayTrack/StopTrack are above.
        // Sent before OpenAnswers so Choices is already on the snapshot the moment Guessing opens.
        var questionPushed = new QuestionPushed("Who sang it?", ["A", "B", "C"])
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = issuedBy
        };
        var questionResult = await backend.Processor.ApplyAsync(Session, Actor.Claimed(questionPushed), questionPushed);
        questionResult.Outcome.Should().Be(Outcome.Applied);

        await emitter.EmitAsync(new OpenAnswers
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = issuedBy
        });
        await emitter.EmitAsync(new LockAnswers
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = issuedBy
        });
        await emitter.EmitAsync(new RevealAnswer(songRef, CorrectChoiceIndex: 0)
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = issuedBy
        });

        // Not phase-endpoint commands either; applied directly, exactly as /api/play and /api/stop do.
        var playTrack = new PlayTrack(songRef.Id.Value)
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = issuedBy
        };
        await backend.Processor.ApplyAsync(Session, Actor.Claimed(playTrack), playTrack);
        var stopTrack = new StopTrack
        {
            SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = issuedBy
        };
        await backend.Processor.ApplyAsync(Session, Actor.Claimed(stopTrack), stopTrack);

        foreach (var audience in audiences) await audience.StopAsync();
        await engine.StopAsync();
        await projector.StopAsync();
        await performer.StopAsync();

        // Assertion 1 (projector): the exact phase sequence broadcast, in order. Two Lobby entries
        // and two Start entries are both real, not a mistake: CreateSession publishes its own
        // snapshot, then UpdateCatalog publishes another (Catalog changes, Phase does not); the
        // same shape repeats at Start - StartGame's phase-change snapshot, then QuestionPushed's
        // state-change snapshot (Choices changes, Phase does not; see the file-level comment for
        // the derivation). This fails if any phase command in the script were rejected, if
        // QuestionPushed stopped changing state, or if the projector never subscribed.
        projector.ReceivedPhases.Should().Equal(
            Phase.Lobby, Phase.Lobby, Phase.Start, Phase.Start, Phase.Guessing, Phase.Lock, Phase.Reveal);

        // Assertion 2 (engine): reacted to exactly the play/stop the script requested, in order.
        // failureRate defaults to 0 for this engine, so Playing is only possible if PlayTrack
        // genuinely reached it through the hub, not by chance.
        engine.Emitted.Select(e => e.Status).Should().Equal(EngineStatus.Playing, EngineStatus.Ready);

        // The backend's own state, read straight from the store - not from a hub broadcast - is
        // the authority for the last two assertions.
        backend.States.TryGet(Session, out var finalState).Should().BeTrue();

        // Assertion 3 (all three audiences): each audience's last recorded answer is keyed by the
        // name it joined with (InProcHubClient.JoinAsync's _participantId, threaded through to
        // AnswerSubmitted.AudienceId). Exactly these three keys being present means all three
        // audiences - not two, not a fourth stand-in - got as far as a counted answer. On its own
        // this proves "at least one each"; paired with assertion 4 below (total submitted count
        // across all audiences is exactly 3) it proves "exactly one each", since a double answer
        // from one audience would still leave this at 3 keys but would push assertion 4 to 4.
        finalState!.Answers.Keys.Should().BeEquivalentTo(["aud-1", "aud-2", "aud-3"]);

        // Assertion 4 (backend tally): Tallies is sized and zeroed by QuestionOffered, then
        // incremented once per accepted AnswerSubmitted event (GameReducer's AnswerSubmitted case).
        // Three audiences each answering once sums to 3 regardless of which of the three options
        // each one happened to pick - this checks the total, not any single bucket.
        // Load-bearing: tallies increment cumulatively and a re-answer never moves a vote, so
        // sum == distinct answerers is what makes the pair prove "exactly one answer each".
        finalState.Tallies.Sum().Should().Be(finalState.Answers.Count);
        finalState.Tallies.Sum().Should().Be(3);
    }
}
