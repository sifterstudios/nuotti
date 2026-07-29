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
// (stage2a fix cc2292b). The fix also unblocked subsequent phases (Play → Intermission → Finished).
// PlaySong is not yet in the script below - that is work for a later task.
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
// Engine playback (PlayTrack/StopTrack) is handled the same way: both are relay commands, also
// absent from the phase-endpoint route table, so they are applied directly through the processor
// - mirroring the real "/api/play" and "/api/stop" routes - rather than through
// InProcCommandEmitter.
//
// WHAT THIS TEST DOES NOT PROVE, AND WHY:
// Two of the brief's four required assertions - "all three audiences submitted exactly one
// answer" and "the tally sums to 3" - are deliberately not written here. GameStateSnapshot.Choices
// is never populated by any command or event this codebase currently has: GameReducer only
// handles GamePhaseChanged, AnswerSubmitted, CorrectAnswerRevealed, HintGiven and CatalogUpdated,
// and only the last of those touches state - setting Catalog, not Choices. SetlistManifest's
// SongEntry has no choices/options field, and QuestionPushed (which does carry Options) is a
// fire-and-forget relay with no reducer case, and no actor in Nuotti.SimKit subscribes to it.
// AudienceActor.OnStateAsync refuses to act while Choices is empty, and even a forced answer
// would be silently discarded by GameReducer's own bounds check against Choices. So no audience -
// simulated or real - can currently submit an answer that the reducer will count, at any phase,
// no matter how the session got there. This is a genuine, previously-undiscovered production gap,
// not a setup step this test forgot to perform; see task-5-report.md for the full analysis.
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
        // are real, not a mistake: CreateSession publishes its own snapshot, then UpdateCatalog
        // publishes another (Catalog changes, Phase does not). This fails if any phase command in
        // the script were rejected, or if the projector never subscribed.
        projector.ReceivedPhases.Should().Equal(
            Phase.Lobby, Phase.Lobby, Phase.Start, Phase.Guessing, Phase.Lock, Phase.Reveal);

        // Assertion 2 (engine): reacted to exactly the play/stop the script requested, in order.
        // failureRate defaults to 0 for this engine, so Playing is only possible if PlayTrack
        // genuinely reached it through the hub, not by chance.
        engine.Emitted.Select(e => e.Status).Should().Equal(EngineStatus.Playing, EngineStatus.Ready);

        // Assertions 3 and 4 from the brief - three audiences each submitting exactly one answer,
        // and the backend's final tally summing to 3 - are not written. See the file-level comment
        // above and task-5-report.md: GameStateSnapshot.Choices is never populated by any command
        // this codebase has today, so AudienceActor.OnStateAsync refuses to act on every Guessing
        // snapshot it receives here, and no audience can currently produce a counted answer.
    }
}
