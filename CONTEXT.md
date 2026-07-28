# Nuotti — domain language

Shared vocabulary for the Nuotti codebase. Terms here are the names used in code; if a
concept is missing from this file, it does not yet have an agreed name.

Architecture vocabulary (module, interface, depth, seam, adapter, leverage, locality) is
defined separately by the `codebase-design` skill and is not restated here.

## Roles

**Performer** — runs the show. The only role permitted to issue phase-changing Commands.

**Audience** — a participant. Submits answers, may request playback.

**Projector** — the big-screen display. Read-only view of the Session; renders the current
Phase.

**Engine** — the audio process (`Nuotti.AudioEngine`). Plays tracks on request and reports
`EngineStatus`.

## Core concepts

**Session** — one show, identified by a session code. Everything is scoped to a Session:
groups, authorization, state, audit.

**Phase** — where a Session is in its lifecycle: `Lobby`, `Start`, `Hint`, `Guessing`,
`Lock`, `Reveal`, `Play`, `Intermission`, `Finished`, plus `Idle`. Defined once in
`Nuotti.Contracts.V1.Enum.Phase`. There is no other phase vocabulary.

**Command** — an intent issued by a role, derived from `CommandBase`. Carries `CommandId`
for idempotency, `SessionCode`, `IssuedByRole`, `IssuedById`. Commands are requests; they
may be rejected.

**Event** — something that happened, derived from `EventBase`. Events are facts and are
never rejected. Events are what the Reducer consumes.

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
