# Nuotti — domain language

Shared vocabulary for the Nuotti codebase. Terms here are the names used in code; if a
concept is missing from this file, it does not yet have an agreed name.

Architecture vocabulary (module, interface, depth, seam, adapter, leverage, locality) is
defined separately by the `codebase-design` skill and is not restated here.

## Roles

**Performer** — runs the show. The only role permitted to issue phase-changing Commands.

**Audience** — a participant. Submits answers, may request playback.

**Participant** — the anonymous, device-bound identity of one Audience member within one
Session. A Participant has a display name, answers, and score, and may reconnect only to
that Session from the same device.

**Projector** — the big-screen display, paired read-only to one Session. It renders the
current Phase but cannot issue Commands or access Workspace content outside that Session.

**Engine** — the audio process (`Nuotti.AudioEngine`). Plays tracks on request and reports
`EngineStatus`.

## SaaS and show preparation

**Workspace** — one tenant in the Nuotti service. A Workspace owns its members, Song
Library, Setlists, Sessions, and registered Show Agents. A person may belong to more than
one Workspace.

**Workspace Owner** — a Workspace member who manages membership, Workspace settings, and
destructive Workspace actions.

**Workspace Member** — a person who can manage show material and Sessions for a Workspace
and may act as a Session's Performer.

**Song Library** — the Workspace's reusable collection of Song Packages.

**Song Catalog** — the answer bank from which Audience members search and select their
answers. For a Session, it combines the Shared Song Catalog with that Session's Workspace
Song Catalog.

**Shared Song Catalog** — Nuotti-curated song entries available to every Workspace.

**Workspace Song Catalog** — private song entries created by a Workspace and available
only to that Workspace and its Audience Sessions.

**Song Package** — the prepared material for one entry in either layer of the Song
Catalog: a Playback Configuration, Hints, and an optional Lyric Track.

**Song Package Revision** — an immutable published version of a Song Package. Editing
published Playback Configuration or Hints creates a new Draft; Setlists upgrade to a
newer revision explicitly.

**Lyric Track** — optional line-timed lyrics attached to a Song Package and versioned
independently of its published Revisions. A Session captures the current Lyric Track so
later edits cannot alter an active show.

**Playback Configuration** — the prepared playback material for a Song Package Revision:
live-only, click-only, backing-only, or backing with click. When both tracks exist, they
share one timeline.

**Setlist** — an ordered selection of Song Package Revisions prepared for a show.

**Session Setlist Snapshot** — the immutable order and exact Song Package Revisions
captured from a Setlist when a Session is created.

**Show Agent** — a named band-side device identity paired to one Workspace with a
revocable credential. It joins an explicitly selected Session on behalf of that Workspace
and makes prepared show material available to the Engine and Projector.

**Round** — one Song Package presented as a song-guessing challenge within a Session. A
Round contains at least one Hint and may contain multiple Guessing Windows before its
answer is revealed and the song is played.

**Hint** — information intended to make the Round's song guessable. A Visual Hint is
shown on the Projector; a Live Hint privately cues the Performer to have the band perform
it. Hints are ordered, and each Guessing Window follows one newly revealed Hint.

**Guessing Window** — a timed opportunity within a Round for Audience members to select
or revise their answers. The answer held when the window locks is the answer that counts.

**Scoring Policy** — the versioned rules and parameters that turn a correct locked answer,
its server timestamp, and its Guessing Window into points. A Session captures one Scoring
Policy for all of its Rounds.

## Core concepts

**Session** — one show, identified by a session code. Everything is scoped to a Session:
groups, authorization, state, audit. A Session belongs to one Workspace. Audience members
join it through the shared public experience using its globally unique session code.

**Phase** — where a Session is in its lifecycle: `Lobby`, `Start`, `Hint`, `Guessing`,
`Lock`, `Reveal`, `Play`, `Intermission`, `Finished`, plus `Idle`. Defined once in
`Nuotti.Contracts.V1.Enum.Phase`. `Start` is the Round Intro; timed countdowns belong to
the Round's Guessing Windows. There is no other phase vocabulary.

**Command** — an intent issued by a role, derived from `CommandBase`. Carries `CommandId`
for idempotency, `SessionCode`, `IssuedByRole`, `IssuedById`. Commands are requests; they
may be rejected.

**Event** — something that happened, derived from `EventBase`. Events are facts and are
never rejected. Events are what the Reducer consumes.

**Choices** — the answer options on offer for the current round. Carried to clients by the
`QuestionPushed` relay Command and put into the Snapshot by the `QuestionOffered` Event. The
Reducer needs them present to bounds-check an `AnswerSubmitted` and to size the tally.

**Reducer** — `GameReducer.Reduce(snapshot, event)`, a pure function returning the next
snapshot or an error. The single place where game state changes. Both the Backend and the
clients run it, on the same events, to reach the same state.

**GameStateSnapshot** — the immutable state of a Session, and the single source of truth
for every UI. Serialized to clients over SignalR (PascalCase) and REST (camelCase), and
mirrored by hand in `Nuotti.Contracts/web/shared/contracts.ts`. A pure DTO — derived
display values live in **Snapshot views**, never on the record.

**Snapshot views** — `GameStateSnapshotViews`, extension methods in Contracts holding
every derived display value (totals, top players, song display text, hint numbering). Kept
off the record so the wire format cannot drift by accident.

## Backend

**SessionCommandProcessor** — the module that applies a Command to a Session. One
interface: `ApplyAsync(session, actor, command, ct)`. Behind it sit role authorization,
idempotency, the phase guard, the Reducer, persistence, audit, metrics and tracing. It is
the only path by which a Session's state changes. It never touches SignalR and never
throws.

**Actor** — who is issuing a Command: role, id, and whether the role was verified by the
server. The hub knows the role from `Join` (verified); an HTTP caller merely claims it in
the request body (unverified). The distinction is deliberately visible.

**Outcome** — the result of applying a Command: `Applied`, `Duplicate` (idempotency hit),
or `Rejected` (carries a `NuottiProblem`). Rejection is a return value, not an exception.

**Fan-out** — `IEventBus` is the only way an Event reaches clients. Subscribers own the
SignalR wire contract; nothing else calls `IHubContext`.

## Projector

**PhasePresenter** — the module that decides what the Projector shows:
`Present(snapshot, settings, windowSize)` returns a **ViewSpec**. Owns view selection,
content safety, localization, tally visibility and typography scaling.

**ViewSpec** — a fully-derived description of one screen: which view, resolved text,
choices, visibility flags, font sizes. Contains no Avalonia types, so it can be asserted
on without a window. `MainWindow` is the adapter that realises a ViewSpec.

The `Nuotti.Projector.Models` and `Nuotti.Projector.Services` namespaces each span two
assemblies: the Avalonia-free types (`ProjectorSettings`, `ContentSafetyService`,
`LocalizationService`, `ResponsiveTypographyService`) live in `Nuotti.Projector.Presentation`;
everything else with the same namespace prefix stays in `Nuotti.Projector`. The namespace
alone does not say which assembly a type lives in — check the project file.
