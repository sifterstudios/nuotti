# SimKit Trace & Viewer — design

Date: 2026-07-28
Status: Approved (design); implementation plan pending

## Problem

There is no way to watch state flow across Nuotti's services. When a phase stalls, a
fan-out goes missing, or a client's `GameStateSnapshot` drifts from the Backend's, the
only evidence is interleaved console logs with no causality and no view of what each
participant believed at the time.

Two distinct needs follow from that:

1. **Deterministic scenario tests** that assert on cross-service flow, and produce a
   visual artifact to open when one fails.
2. **A realistic-speed live run** with the real UIs on screen, observed through the
   logging and telemetry the repo already has.

Both are served by one trace format.

## Non-goals

- Real UI screenshots embedded in the timeline. That is visual-regression work, tracked
  separately as I16.
- Replacing the Aspire dashboard. Mode 2 uses it as-is.
- A live-streaming viewer. The format is designed not to preclude it; it is not built here.

## Decisions

| Decision | Choice |
|---|---|
| Primary job | Debugging first; assertions layer onto the same trace |
| Fidelity | In-memory by default, real backend opt-in, one trace format for both |
| Delivery | Self-contained HTML artifact; live viewer is a later, non-blocking follow-on |
| Viewer layout | Message swimlanes and a state board driven by a single playhead |
| Trace depth | Transport + reducer + derived view layer (`ViewSpec`) |
| Backbone | OpenTelemetry, extending the existing `nuotti` spans |
| Code location | Extend `Nuotti.SimKit`; two small satellite projects (below) |

### Rejected alternatives

- **A separate `Nuotti.Harness` project.** Cleaner on paper, but it would either reference
  SimKit (buying no boundary) or duplicate the actors (two drifting models of audience
  behaviour). SimKit's actors already model exactly the participants to be traced.
- **Everything through an OTLP collector.** Uniform across both modes, but it puts an
  exporter and a flush in the loop of every unit test. Mode 2 gets this via Aspire anyway,
  so mode 1 does not need to pay for it.
- **Serilog logs as the backbone.** Zero new instrumentation, but logs carry no
  parent/child or duration, so causality and latency would be reconstructed by guesswork,
  and a log-level change would silently break the viewer.
- **Reusing `web/`'s SvelteKit for the viewer.** Would put `npm run build` in the path of
  every generated trace. The cost of rejecting it is that viewer JS shares no code with the
  web app — acceptable, since it renders traces, not game UI.

## Existing code this builds on

Verified against the tree at `af5d388`:

- `SessionCommandProcessor(store, idempotency, bus, logger, metrics?, audit?)` — plain
  constructor. An in-proc Backend needs no host, no Kestrel, no ports.
- `InMemoryEventBus` invokes subscribers **synchronously in registration order**. This is
  the deterministic pump; it does not need to be written.
- `BackendActivitySource` already emits `command.*`, `event.broadcast.*` and
  `event.process.*` spans carrying `session.code` and `correlation.id`.
- `LatencyPolicy.SampleDelay(Random?)` and `ChaosPolicy.SampleDowntime(Random?)` accept an
  injectable `Random`, so seeding is a matter of threading one seeded instance through.
- `IHubClientFactory` and `ICommandEmitter` are the two seams the fidelity swap uses.
- `ServiceDefaults` already wires OTLP export and Serilog JSON with `service`, `version`,
  `session`, `role`, `connectionId` and PII redaction.
- The Aspire AppHost (`Nuotti/Program.cs`) already runs all five services.

### Gaps in existing code that this work must close

1. **`ICommandEmitter` has no implementation anywhere.** `PerformerActor` can build commands
   from a script but nothing sends them, so SimKit cannot currently drive a session. Both an
   `InProcCommandEmitter` and an `HttpCommandEmitter` are required.
2. **`PhasePresenter` and `ViewSpec` live inside `Nuotti.Projector`**, which references
   Avalonia. Using them from tests would drag Avalonia into the test run. They must be
   extracted to an Avalonia-free `Nuotti.Projector.Presentation`. `CONTEXT.md` already
   states `ViewSpec` holds no Avalonia types, so the move is mechanical.
3. **Sampled latency must be applied through `ITimeProvider`, not `Task.Delay`**, or mode 1
   is not reproducible.

## Architecture

### New modules

| Module | Responsibility |
|---|---|
| `Nuotti.SimKit/World/` | `SimWorld` assembles a run from `SimWorldOptions` (roster, seed, clock, fidelity); `RunAsync(scenario, ct)` returns a `TraceRun` |
| `Nuotti.SimKit/Trace/` | `NuottiTrace` (the span convention), `TraceRecorder` (`ActivityListener`), `TraceRecord`, `ITraceSink`, `JsonlTraceSink` |
| `Nuotti.SimKit/Viewer/` | `TraceViewerWriter` — turns a `TraceRun` into one self-contained `trace.html` |
| `Nuotti.SimKit/Hub/HttpCommandEmitter` | The real-mode `ICommandEmitter` (gap 1) |
| `Nuotti.SimKit.InProc` *(new project)* | `InProcHubClientFactory`, `InProcHubClient`, `InProcCommandEmitter`. References SimKit + Backend, keeping the Backend dependency out of the CLI |
| `Nuotti.Projector.Presentation` *(extracted)* | `PhasePresenter` + `ViewSpec`, Avalonia-free (gap 2) |

`Nuotti.SimKit` itself keeps its current `Nuotti.Contracts`-only project reference.

### The fidelity swap

Two factory substitutions, nothing else. Actors, chaos, latency and `ITimeProvider` never
learn which mode they are in.

| Seam | In-memory | Real |
|---|---|---|
| `IHubClientFactory` | `InProcHubClientFactory` → `SessionCommandProcessor` + `IEventBus` directly | `HubConnectionFactory` (exists) → SignalR |
| `ICommandEmitter` | `InProcCommandEmitter` | `HttpCommandEmitter` |

## Trace format

One JSON object per line in `runs/<id>/trace.jsonl`:

```json
{"seq":14,"t":40,"kind":"transport.drop","lane":"audience:a3",
 "from":"backend","to":"audience:a3","session":"dev",
 "corr":"8f2c…","span":"3a91…","parent":"1b04…",
 "payload":{"dropped":"GamePhaseChanged","cause":"ChaosInjection"},
 "synthetic":true}
```

- `seq` — monotonic integer, the **sole** ordering key. Never sort by time.
- `t` — milliseconds since run start, read from `ITimeProvider`. Virtual under
  `ImmediateTimeProvider` (mode 1); wall-clock under `RealTimeProvider` (mode 2).
- `lane` — the participant this record belongs to. **The Backend is always a lane.** In
  mode 1 its snapshot is read directly from the in-proc store; in mode 2 it is taken from
  the `GameStateChanged` fan-out payload, which is the only server state a client can
  observe. Divergence is always defined as "this lane's snapshot hash versus the Backend
  lane's most recent hash at or before this `seq`".
- `span` / `parent` — real OpenTelemetry ids, so any row can be located in the Aspire
  dashboard.
- `synthetic` — true for records only the harness can know (chaos injected here, virtual
  clock now reads T, this actor is a fake).

### Kinds

- **Protocol** — `command.issued`, `command.outcome` (mirrors `Outcome`:
  Applied / Duplicate / Rejected, carrying `NuottiProblem` on rejection), `event.fanout`,
  `event.received`, `transport.drop`, `transport.delay`, `connection.state`
- **State** — `reducer.applied`, `reducer.rejected`
- **View** — `view.rendered` (payload is the `ViewSpec`)
- **Run** — `run.started` (lane roster, seed, options, `"format":1`), `run.ended`
  (`RunMetrics`, `traceIncomplete`)

### Snapshot size

A full `GameStateSnapshot` per `reducer.applied` is unusable at 200 audiences. Therefore:

- `reducer.applied` carries a **diff against that lane's previous snapshot, plus a content
  hash**.
- Full snapshots appear only at `run.started`, at explicit checkpoints, and on any lane
  whose hash stops matching the Backend's.
- Divergence detection is an O(1) hash compare; the viewer reconstructs state by replaying
  diffs up to the playhead.

### Mapping onto OpenTelemetry

Each record is one `Activity` or `ActivityEvent`, with tags prefixed `nuotti.`:
`nuotti.kind`, `nuotti.lane`, `nuotti.seq`, `nuotti.t`, `nuotti.session`, `nuotti.corr`,
`nuotti.payload`. `TraceRecorder` rebuilds records from those tags; Aspire reads the same
spans natively.

**The Backend needs no rewrite to start.** `TraceRecorder` aliases the spans that already
exist: `command.{name}` → `command.issued`, `event.broadcast.{type}` → `event.fanout`,
`event.process.{sub}.{type}` → `event.received`. The Backend is only touched later, to add
tags the aliases cannot supply.

**Payload cardinality.** `nuotti.payload` is unbounded and would breach dashboard size
limits in live mode. File mode keeps the full payload; live mode truncates to a configurable
cap and sets `payload.truncated: true`.

## Mode 1 — deterministic test run (default)

```
SimWorld.Build(opts) ─ in-proc Backend: GameStateStore + IdempotencyStore
                     │                  + InMemoryEventBus + SessionCommandProcessor
                     ├─ lanes from roster: actor + InProcHubClient
                     │                     + snapshot holder + PhasePresenter (projector lanes)
                     └─ TraceRecorder attached BEFORE anything starts

Performer ─ InProcCommandEmitter ─▸ processor ─▸ events ─▸ bus
   ─▸ chaos/latency decorators ─▸ lane clients ─▸ GameReducer ─▸ ViewSpec
        (every hop emits its nuotti.* span)

finally ─▸ trace.jsonl ─▸ trace.html ─▸ TraceRun returned to the test
```

Determinism rests on four things: synchronous ordered dispatch (already true of
`InMemoryEventBus`), one seeded `Random` threaded through chaos and latency, `seq` rather
than time as the ordering key, and delays applied via `ITimeProvider` so
`ImmediateTimeProvider` collapses them to zero wall-clock while `t` still advances.

Given a fixed seed, two runs of a scenario produce a byte-identical `trace.jsonl`.

## Mode 2 — realistic-speed live run

The AppHost gains SimKit as a resource referencing `backend`. Fidelity is `RealBackend`
with `RealTimeProvider` and the existing `--speed` flag. The real Projector, Blazor apps and
web app run and are watched directly. Spans export over OTLP to the Aspire dashboard;
Serilog JSON reaches the dashboard's log view, joined on `session` and `corr`.
`--trace-file` optionally also writes `trace.jsonl` so the same viewer works post-hoc.

**Limitation.** The harness only sees inside real clients that are instrumented. Initially
mode 2 yields transport-level spans (which the Backend already emits); `reducer.applied` and
`view.rendered` from the real Projector require adding its `ActivitySource`. This is
incremental and does not block mode 2 being useful.

## Viewer

`TraceViewerWriter.Write(run, path)` substitutes the trace into a `viewer.html` template
embedded as an assembly resource. Vanilla JS and CSS: no npm, no CDN, no build step — the
test suite must never depend on a JS toolchain.

**Layout.** Message swimlanes (one column per participant, time descending, arrows between
columns) beside a state board showing each lane's belief at the playhead, with divergence
from the Backend highlighted. One playhead drives both. Selecting an arrow shows its payload
and highlights which lanes diverged from it.

**Size.** The JSONL is inlined **gzipped and base64-encoded**, inflated in-browser via
`DecompressionStream`. This keeps one self-contained file at roughly a tenth of the size.
Fetching a sibling `trace.jsonl` was rejected because `file://` CORS blocks it; downsampling
was rejected because it loses data silently.

**Interactions.** Scrub and step the playhead, click an arrow for its payload, filter lanes,
jump to next divergence, keyboard navigation.

**Stability.** Output must be byte-stable for a given trace — no generation timestamp baked
in — so the HTML itself remains diffable.

## Assertions

`RunAsync` returns a `TraceRun`:

```csharp
run.Records                  // IReadOnlyList<TraceRecord>
run.Lane("audience:a3")      // lane view: snapshots, views, connection history
run.Divergences()            // lanes whose snapshot hash != Backend's
run.Views("projector")       // ViewSpec sequence
run.Metrics                  // existing RunMetrics
run.ViewerPath               // printed on failure
```

Helpers throw with both the offending `seq` and the viewer path:

```csharp
TraceAssert.Converges(run);
TraceAssert.NoDivergence(run);
TraceAssert.PhaseSequence(run, "projector", expected);
TraceAssert.NoLostEvents(run);
TraceAssert.NoRejections(run);
```

**`Converges` versus `NoDivergence` is the distinction that matters.** `NoDivergence`
forbids a lane ever disagreeing with the Backend. `Converges` permits transient
disagreement but requires every lane to finish equal to the Backend. Chaos scenarios must
use `Converges`: a dropped packet *should* diverge a client temporarily, and the bug is when
it never catches up. Using the strict assertion there would fail every chaos test for the
wrong reason.

**Snapshot regression.** The repo already uses Verify. `await Verify(run.ToTimeline())` —
a compact deterministic text rendering of the trace — is the cheap regression assertion, and
is reviewed as a diff alongside existing snapshots.

## Error handling

- The recorder never fails a run. Sink errors log a warning and continue; `run.ended`
  carries `traceIncomplete: true`.
- The trace is flushed in a `finally`. A trace matters most when the run failed; losing it
  on exception would be this feature's worst possible bug.
- Assertion failures quote the `seq` and print the `trace.html` path.
- The viewer tolerates a half-written trailing line after a crash by skipping the last
  unparseable line rather than refusing to load.

## Testing

| Suite | Contents |
|---|---|
| `Nuotti.SimKit.Tests` | Harness unit tests: recorder ordering, snapshot diff/replay round-trip, viewer writer output stability, seeded determinism (same seed → identical `trace.jsonl`) |
| `tests/Nuotti.IntegrationTests` | Scenario tests spanning Backend + Contracts + Presentation: single song happy path, multi-song scoring, chaos-with-recovery, reconnect resync |
| CI | Uploads `runs/` as an artifact so a red build yields an openable `trace.html` |

## Implementation order

The work is large enough that it should be staged. Each stage is independently useful and
leaves the tree green.

1. **Unblock the harness** — extract `Nuotti.Projector.Presentation`; write
   `InProcCommandEmitter` and `HttpCommandEmitter`; route sampled latency through
   `ITimeProvider`. Closes all three gaps above. No trace yet.
2. **In-proc world** — `Nuotti.SimKit.InProc`, `SimWorld`, `SimWorldOptions`, lanes. Proven
   by a test that drives a single song end to end with no trace.
3. **Trace** — `NuottiTrace`, `TraceRecorder` with alias mapping, `TraceRecord`,
   `JsonlTraceSink`, snapshot diff/hash. Proven by the seeded-determinism test.
4. **Viewer** — `TraceViewerWriter` and the `viewer.html` template.
5. **Assertions** — `TraceRun` query surface, `TraceAssert`, `ToTimeline()` for Verify, and
   the first integration scenarios.
6. **Mode 2** — SimKit as an AppHost resource, `--trace-file`, and instrumenting the real
   Projector with its `ActivitySource`.

Stages 1–3 are the critical path; 4 and 5 can proceed in parallel once 3 lands. Stage 6
depends only on stage 1.

## Relationship to the testing master plan

This work supplies infrastructure that several pending items in
`docs/testing-master-plan.md` depend on: I11 (reconnect/resync), I12 and I13 (E2E song
flows) and I18 (load and chaos). It does not supersede I16 (visual regression), which
remains separate.
