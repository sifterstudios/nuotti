# Stage 2a — Hub Seam and Participants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every simulated participant actually react in-process — audiences answer, the projector tracks phases, the engine responds to play/stop — by widening the hub seam to carry everything the Backend really broadcasts.

**Architecture:** `IHubClient` gains a generic `On<T>` subscription keyed on the *payload* type, replacing the single `OnGameStateChanged`. A new `InProcHubClient` adapts `IEventBus` to that interface, translating events to payloads exactly as `HubBroadcastSubscriber` does for the real wire, and filtering by session. `AudienceActor` and `EngineActor` then subscribe to what they need.

**Tech Stack:** .NET 10, xUnit, FluentAssertions, ASP.NET Core (Backend, referenced only from `Nuotti.SimKit.InProc`).

**Spec:** `docs/superpowers/specs/2026-07-28-simkit-trace-viewer-design.md`. This is the first half of that spec's stage 2, split because closing the participant gaps is its own reviewable unit. Stage 2b adds `SimWorld`, `SimWorldOptions` and lanes on top.

**Predecessor:** `docs/superpowers/plans/2026-07-28-harness-unblock.md` (stage 1), merged at `263f1c1`.

## Global Constraints

- **Build and test with `~/.dotnet/dotnet`, never bare `dotnet`.** The asdf shim on this machine exits 0 without compiling, so a bare `dotnet build` reports success on code that does not compile.
- Target `net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- **`Nuotti.SimKit.csproj` must keep exactly one `ProjectReference`: `Nuotti.Contracts`.** Anything needing the Backend goes in `Nuotti.SimKit.InProc`.
- Existing test projects keep their pinned versions (`Nuotti.SimKit.Tests`: xunit 2.9.3 / Test.Sdk 17.12.0 / FluentAssertions 6.12.0; `Nuotti.SimKit.InProc.Tests`: xunit 2.9.2 / Test.Sdk 17.8.0 / FluentAssertions 6.12.0).
- **No `Random.Shared` anywhere in `Nuotti.SimKit` production code.** Stage 1 removed it from latency and chaos; this stage removes the last one. A seeded `Random` is supplied by the caller.
- Baseline: 493 tests passing across 9 assemblies, 0 build errors. The count may only go up.
- Conventional commits.

## The wire contract this stage mirrors

`Nuotti.Backend/Eventing/Subscribers/HubBroadcastSubscriber.cs` is the single source of truth for what reaches clients. Verified at `263f1c1`:

| Bus subscription | SignalR method name | Payload actually sent |
|---|---|---|
| `GameStateChanged` | `"GameStateChanged"` | `evt.Snapshot` — the bare `GameStateSnapshot`, **not** the event |
| `AnswerSubmitted` | `"AnswerSubmitted"` | the `AnswerSubmitted` event itself |
| `QuestionPushed` | `"QuestionPushed"` | the `QuestionPushed` command |
| `PlayTrack` | `"PlayTrack"` | the `PlayTrack` command |
| `StopTrack` | **`"Stop"`** | the `StopTrack` command |

**Two traps live in that table.** `StopTrack` is sent under the name `"Stop"`, so any assumption that the method name equals the type name silently produces a subscription that never fires. And `GameStateChanged` puts the snapshot on the wire, not the envelope — so the subscription payload type is `GameStateSnapshot`.

That is why `On<T>` is keyed on **payload type**, and why the type-to-method-name map is explicit rather than derived from `typeof(T).Name`.

Also note `AnswerSubmitted` carries no snapshot push by design (`BroadcastSnapshot: false` in `SessionCommandProcessor`, because one snapshot per answer is quadratic in audience size). Clients are expected to apply `GameReducer` to the event. This stage delivers the event to clients; applying the reducer client-side is stage 2b's concern, not yours.

---

### Task 1: Replace `OnGameStateChanged` with a generic `On<T>`

**Files:**
- Modify: `Nuotti.SimKit/Hub/IHubClient.cs`
- Create: `Nuotti.SimKit/Hub/HubWireNames.cs`
- Modify: `Nuotti.SimKit/Hub/HubConnectionFactory.cs` (`RealHubClient`)
- Modify: `Nuotti.SimKit/Hub/ConcurrencyThrottle.cs`, `LatencyInjection.cs`, `ChaosInjection.cs`
- Modify: `Nuotti.SimKit/Actors/ProjectorActor.cs`
- Modify: the 12 test doubles in `Nuotti.SimKit.Tests` (the compiler will list them)
- Test: `Nuotti.SimKit.Tests/HubWireNamesTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks in this plan.
- Produces:
  - `IDisposable IHubClient.On<T>(Func<T, Task> handler)` — replaces `OnGameStateChanged`. Subscribing to `GameStateSnapshot` is what the old member did.
  - `public static class HubWireNames` with `public static IReadOnlyDictionary<Type, string> ByPayloadType { get; }` and `public static string For<T>()` (throws `NotSupportedException` for an unmapped type).

  Every later task in this plan depends on both.

- [ ] **Step 1: Write the failing test**

Create `Nuotti.SimKit.Tests/HubWireNamesTests.cs`:

```csharp
using FluentAssertions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class HubWireNamesTests
{
    [Theory]
    [InlineData(typeof(GameStateSnapshot), "GameStateChanged")]
    [InlineData(typeof(AnswerSubmitted), "AnswerSubmitted")]
    [InlineData(typeof(QuestionPushed), "QuestionPushed")]
    [InlineData(typeof(PlayTrack), "PlayTrack")]
    [InlineData(typeof(StopTrack), "Stop")]
    public void Maps_each_payload_type_to_the_name_the_backend_actually_sends(Type payload, string expected)
    {
        HubWireNames.ByPayloadType[payload].Should().Be(expected);
    }

    [Fact]
    public void StopTrack_is_not_named_after_its_type()
    {
        // Guarding the specific trap: HubBroadcastSubscriber sends StopTrack as "Stop".
        // Deriving the method name from typeof(T).Name would produce a subscription that
        // silently never fires.
        HubWireNames.ByPayloadType[typeof(StopTrack)].Should().NotBe(nameof(StopTrack));
    }

    [Fact]
    public void An_unmapped_payload_type_is_rejected_rather_than_guessed()
    {
        var act = () => HubWireNames.For<HubWireNamesTests>();

        act.Should().Throw<NotSupportedException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj --filter FullyQualifiedName~HubWireNamesTests`

Expected: FAIL to compile — `HubWireNames` does not exist.

- [ ] **Step 3: Add `HubWireNames`**

`Nuotti.SimKit/Hub/HubWireNames.cs`:

```csharp
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.SimKit.Hub;

/// <summary>
/// Maps a broadcast payload type to the SignalR method name the Backend sends it under.
/// </summary>
/// <remarks>
/// Mirrors Nuotti.Backend.Eventing.Subscribers.HubBroadcastSubscriber, which owns the outbound
/// wire contract. Two entries are not derivable from the type name and must not be guessed:
/// StopTrack is sent as "Stop", and GameStateChanged sends the bare GameStateSnapshot rather
/// than the event envelope — so the snapshot, not the event, is the payload type here.
///
/// If a broadcast is added to HubBroadcastSubscriber, add it here too.
/// </remarks>
public static class HubWireNames
{
    public static IReadOnlyDictionary<Type, string> ByPayloadType { get; } = new Dictionary<Type, string>
    {
        [typeof(GameStateSnapshot)] = "GameStateChanged",
        [typeof(AnswerSubmitted)] = "AnswerSubmitted",
        [typeof(QuestionPushed)] = "QuestionPushed",
        [typeof(PlayTrack)] = "PlayTrack",
        [typeof(StopTrack)] = "Stop",
    };

    public static string For<T>() =>
        ByPayloadType.TryGetValue(typeof(T), out var name)
            ? name
            : throw new NotSupportedException(
                $"{typeof(T).Name} is not a broadcast payload. Subscribable payloads are: " +
                string.Join(", ", ByPayloadType.Keys.Select(t => t.Name)));
}
```

- [ ] **Step 4: Change the interface**

In `Nuotti.SimKit/Hub/IHubClient.cs`, replace `OnGameStateChanged` with:

```csharp
    /// <summary>
    /// Subscribe to a broadcast from the hub, keyed on the payload type.
    /// Returns IDisposable to allow unsubscription.
    /// </summary>
    /// <remarks>
    /// T is the payload type, not the event type — see <see cref="HubWireNames"/>. Subscribing
    /// to GameStateSnapshot is what the former OnGameStateChanged did.
    ///
    /// The handler returns a Task so the publisher can await it. With Action&lt;T&gt;, any handler
    /// that awaited was an async void: receive order was unguaranteed and exceptions were
    /// unobservable, which makes a recorded run irreproducible.
    /// </remarks>
    IDisposable On<T>(Func<T, Task> handler);
```

- [ ] **Step 5: Update the four production implementors**

- `HubConnectionFactory.cs` (`RealHubClient`): `public IDisposable On<T>(Func<T, Task> handler) => _connection.On(HubWireNames.For<T>(), handler);` — note it must go through `HubWireNames`, not `typeof(T).Name`.
- `ConcurrencyThrottle.cs` (`ThrottlingHubClient`): pure pass-through — `public IDisposable On<T>(Func<T, Task> handler) => _inner.On(handler);`
- `LatencyInjection.cs` (`LatencyInjectingHubClient`): keep the existing receive-side delay body, just make it generic:

```csharp
    public IDisposable On<T>(Func<T, Task> handler)
    {
        return _inner.On<T>(async payload =>
        {
            if (_activePolicy is { ApplyToReceives: true } p)
                await _time.Delay(p.SampleDelay(_random)).ConfigureAwait(false);
            await handler(payload).ConfigureAwait(false);
        });
    }
```

- `ChaosInjection.cs` (`ChaosInjectingHubClient`): same shape, keeping its existing disconnect-cycle body:

```csharp
    public IDisposable On<T>(Func<T, Task> handler)
    {
        return _inner.On<T>(async payload =>
        {
            var p = _activePolicy;
            if (p is { ApplyToReceives: true } pp && _random.NextDouble() < pp.Probability)
                await DisconnectCycleAsync(pp).ConfigureAwait(false);
            await handler(payload).ConfigureAwait(false);
        });
    }
```

- [ ] **Step 6: Update `ProjectorActor`**

`Nuotti.SimKit/Actors/ProjectorActor.cs` currently subscribes with `Client.OnGameStateChanged(OnStateAsync)`. It becomes:

```csharp
            _subscription = Client.On<GameStateSnapshot>(OnStateAsync);
```

`OnStateAsync` already takes a `GameStateSnapshot` and returns `Task`, so nothing else changes.

- [ ] **Step 7: Update the test doubles**

Let the compiler enumerate them:

```bash
~/.dotnet/dotnet build Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj 2>&1 | grep -E 'error CS'
```

Mechanical transformation per double:

- `public IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler)` becomes `public IDisposable On<T>(Func<T, Task> handler)`.
- A stored field `Func<GameStateSnapshot, Task>? _handler` becomes `Func<GameStateSnapshot, Task>? _handler`, assigned only when `typeof(T) == typeof(GameStateSnapshot)`:

```csharp
    public IDisposable On<T>(Func<T, Task> handler)
    {
        if (typeof(T) == typeof(GameStateSnapshot))
            _handler = snapshot => handler((T)(object)snapshot);
        return new Sub(this);
    }
```

- Doubles that ignore the subscription entirely keep doing so: `public IDisposable On<T>(Func<T, Task> handler) => new D();`
- Do not change what any double asserts or records. This step is signature plumbing only.

- [ ] **Step 8: Run the suites**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj`
Expected: PASS — 62 baseline plus the 7 new `HubWireNamesTests` cases, no pre-existing test changed in what it asserts.

Run: `~/.dotnet/dotnet build Nuotti.sln -c Debug`
Expected: PASS, 0 errors.

- [ ] **Step 9: Commit**

```bash
git add Nuotti.SimKit/Hub Nuotti.SimKit/Actors/ProjectorActor.cs Nuotti.SimKit.Tests
git commit -m "refactor(simkit): key hub subscriptions on the payload type

IHubClient exposed only OnGameStateChanged, but the Backend broadcasts five
things: the snapshot, AnswerSubmitted, and the QuestionPushed/PlayTrack/StopTrack
relay commands. A simulated engine could never see PlayTrack and a simulated
audience could never see QuestionPushed.

On<T> replaces the single member, keyed on the payload type. HubWireNames mirrors
HubBroadcastSubscriber, which owns the outbound contract — the map is explicit
because two entries are not derivable from the type name: StopTrack is sent as
\"Stop\", and GameStateChanged sends the bare snapshot, not the envelope."
```

---

### Task 2: `InProcHubClient` over `IEventBus`

**Files:**
- Create: `Nuotti.SimKit.InProc/InProcHubClient.cs`
- Create: `Nuotti.SimKit.InProc/InProcHubClientFactory.cs`
- Modify: `Nuotti.SimKit.InProc/InProcBackend.cs` (expose what the factory needs)
- Test: `Nuotti.SimKit.InProc.Tests/InProcHubClientTests.cs`

**Interfaces:**
- Consumes: `IHubClient.On<T>` and `HubWireNames` from Task 1; `InProcBackend` from stage 1.
- Produces: `InProcHubClientFactory(InProcBackend backend, string session) : IHubClientFactory`. `Create(Uri)` ignores the address — there is no network — and returns an `InProcHubClient` bound to that backend and session.

- [ ] **Step 1: Write the failing test**

Create `Nuotti.SimKit.InProc.Tests/InProcHubClientTests.cs`:

```csharp
using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.InProc.Tests;

public class InProcHubClientTests
{
    static readonly Uri Unused = new("http://in-proc");

    static async Task<InProcBackend> ASessionAsync(string session = "dev")
    {
        var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor);
        await emitter.EmitAsync(new CreateSession(session)
        {
            SessionCode = session, IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });
        return backend;
    }

    [Fact]
    public async Task Delivers_the_bare_snapshot_not_the_event_envelope()
    {
        using var backend = await ASessionAsync();
        var factory = new InProcHubClientFactory(backend, "dev");
        var client = factory.Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "projector");

        var received = new List<GameStateSnapshot>();
        using var sub = client.On<GameStateSnapshot>(s => { received.Add(s); return Task.CompletedTask; });

        var emitter = new InProcCommandEmitter(backend.Processor);
        await emitter.EmitAsync(new StartGame
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });

        received.Should().NotBeEmpty();
        received[^1].Phase.Should().Be(Phase.Start);
    }

    [Fact]
    public async Task Does_not_deliver_messages_from_another_session()
    {
        using var backend = await ASessionAsync("dev");
        var emitterOther = new InProcCommandEmitter(backend.Processor);
        await emitterOther.EmitAsync(new CreateSession("other")
        {
            SessionCode = "other", IssuedByRole = Role.Performer, IssuedById = "perf-2"
        });

        var factory = new InProcHubClientFactory(backend, "dev");
        var client = factory.Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "projector");

        var received = new List<GameStateSnapshot>();
        using var sub = client.On<GameStateSnapshot>(s => { received.Add(s); return Task.CompletedTask; });

        await emitterOther.EmitAsync(new StartGame
        {
            SessionCode = "other", IssuedByRole = Role.Performer, IssuedById = "perf-2"
        });

        // The real hub sends to Clients.Group(session); the in-proc client must filter the
        // same way or lanes in different sessions cross-talk.
        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Stops_delivering_after_the_subscription_is_disposed()
    {
        using var backend = await ASessionAsync();
        var factory = new InProcHubClientFactory(backend, "dev");
        var client = factory.Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "projector");

        var received = new List<GameStateSnapshot>();
        var sub = client.On<GameStateSnapshot>(s => { received.Add(s); return Task.CompletedTask; });
        sub.Dispose();

        var emitter = new InProcCommandEmitter(backend.Processor);
        await emitter.EmitAsync(new StartGame
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Delivers_a_relay_command_to_its_payload_subscribers()
    {
        using var backend = await ASessionAsync();
        var factory = new InProcHubClientFactory(backend, "dev");
        var client = factory.Create(Unused);
        await client.StartAsync();
        await client.JoinAsync("dev", "engine");

        var plays = new List<PlayTrack>();
        using var sub = client.On<PlayTrack>(p => { plays.Add(p); return Task.CompletedTask; });

        await backend.Bus.PublishAsync(new PlayTrack(/* fill from the real ctor */)
        {
            SessionCode = "dev"
        }, CancellationToken.None);

        plays.Should().HaveCount(1);
    }
}
```

`CreateSession`, `PlayTrack` and `IEventBus`'s publish member may not match these shapes exactly. Confirm before writing the implementation and adapt:

```bash
rtk proxy grep -rn 'record CreateSession\|record PlayTrack' -A 6 Nuotti.Contracts/V1/Message/
rtk proxy grep -n 'Task Publish\|Subscribe<' Nuotti.Contracts/V1/Eventing/IEventBus.cs
```

The last test's point is that a relay command reaches a payload subscriber; if publishing one directly onto the bus is awkward, drive it through a `PlaySong` command via the emitter instead and say so in your report.

- [ ] **Step 2: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.InProc.Tests/Nuotti.SimKit.InProc.Tests.csproj --filter FullyQualifiedName~InProcHubClientTests`

Expected: FAIL to compile — `InProcHubClientFactory` does not exist.

- [ ] **Step 3: Write `InProcHubClient` and its factory**

`Nuotti.SimKit.InProc/InProcHubClient.cs`:

```csharp
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.InProc;

/// <summary>
/// An <see cref="IHubClient"/> backed by the in-process <see cref="IEventBus"/> instead of a
/// SignalR connection.
/// </summary>
/// <remarks>
/// Mirrors HubBroadcastSubscriber's translation exactly — same payload for each broadcast, and
/// the same per-session scoping the real hub gets from Clients.Group(session). Getting either
/// wrong would make in-memory runs disagree with real ones for reasons unrelated to the code
/// under test.
///
/// There is no connection to open, so StartAsync and StopAsync only gate delivery: a client
/// that has not started, or has stopped, receives nothing. That is what makes the chaos
/// decorator's disconnect cycle observable in-process.
/// </remarks>
public sealed class InProcHubClient : IHubClient
{
    readonly IEventBus _bus;
    readonly string _session;
    readonly List<IDisposable> _busSubs = [];
    readonly object _gate = new();
    bool _started;

    public InProcHubClient(IEventBus bus, string session)
    {
        _bus = bus;
        _session = session;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) _started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) _started = false;
        return Task.CompletedTask;
    }

    // Joining is a no-op in-process: there is no group to add a connection to, and the
    // session scope is fixed at construction.
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "InProcHubClient cannot submit answers directly — a SubmitAnswer command goes " +
            "through InProcCommandEmitter, the same path the HTTP fidelity uses.");

    public IDisposable On<T>(Func<T, Task> handler)
    {
        // Fail fast on a payload type the Backend never broadcasts, matching HubWireNames.For<T>().
        _ = HubWireNames.For<T>();

        IDisposable sub = typeof(T) switch
        {
            var t when t == typeof(GameStateSnapshot) =>
                _bus.Subscribe<GameStateChanged>((evt, ct) =>
                    Deliver(evt.SessionCode, (T)(object)evt.Snapshot, handler)),
            var t when t == typeof(AnswerSubmitted) =>
                _bus.Subscribe<AnswerSubmitted>((evt, ct) => Deliver(evt.SessionCode, (T)(object)evt, handler)),
            var t when t == typeof(QuestionPushed) =>
                _bus.Subscribe<QuestionPushed>((cmd, ct) => Deliver(cmd.SessionCode, (T)(object)cmd, handler)),
            var t when t == typeof(PlayTrack) =>
                _bus.Subscribe<PlayTrack>((cmd, ct) => Deliver(cmd.SessionCode, (T)(object)cmd, handler)),
            var t when t == typeof(StopTrack) =>
                _bus.Subscribe<StopTrack>((cmd, ct) => Deliver(cmd.SessionCode, (T)(object)cmd, handler)),
            _ => throw new NotSupportedException($"No in-proc subscription for {typeof(T).Name}."),
        };

        lock (_gate) _busSubs.Add(sub);
        return sub;
    }

    Task Deliver<T>(string session, T payload, Func<T, Task> handler)
    {
        bool deliver;
        lock (_gate) deliver = _started && string.Equals(session, _session, StringComparison.Ordinal);
        return deliver ? handler(payload) : Task.CompletedTask;
    }
}
```

`Nuotti.SimKit.InProc/InProcHubClientFactory.cs`:

```csharp
using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.InProc;

/// <summary>
/// Produces hub clients wired to an in-process backend. The base address is ignored — this is
/// the fidelity swap's in-memory half, and there is no network.
/// </summary>
public sealed class InProcHubClientFactory(InProcBackend backend, string session) : IHubClientFactory
{
    public IHubClient Create(Uri baseAddress) => new InProcHubClient(backend.Bus, session);
}
```

If `_bus.Subscribe<T>` returns something other than `IDisposable`, or its handler signature differs, adapt — the shape was verified as `IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task>)` at `263f1c1`.

- [ ] **Step 4: Run test to verify it passes**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.InProc.Tests/Nuotti.SimKit.InProc.Tests.csproj`
Expected: PASS — 6 baseline plus the 4 new ones.

- [ ] **Step 5: Commit**

```bash
git add Nuotti.SimKit.InProc Nuotti.SimKit.InProc.Tests
git commit -m "feat(simkit-inproc): a hub client backed by the event bus

InProcHubClient adapts IEventBus to IHubClient, translating each broadcast to the
same payload HubBroadcastSubscriber puts on the wire and scoping delivery to one
session the way Clients.Group(session) does. Start/Stop gate delivery so the chaos
decorator's disconnect cycle is observable in-process.

SubmitAnswer throws: answers go through InProcCommandEmitter, the same path the
HTTP fidelity uses."
```

---

### Task 3: Make `AudienceActor` subscribe, and seed it

`AudienceActor.OnStateAsync` is never wired to anything — `ProjectorActor` overrides `OnStartedAsync` to subscribe but `AudienceActor` does not, so simulated audiences never answer. It also falls back to `Random.Shared` when no seed is given, the last such fallback in SimKit production code.

**Files:**
- Modify: `Nuotti.SimKit/Actors/AudienceActor.cs`
- Modify: `Nuotti.SimKit.Tests/AudienceActorAnsweringTests.cs`, `AudienceActorTimeControlTests.cs` (constructor call sites)
- Test: `Nuotti.SimKit.Tests/AudienceActorSubscriptionTests.cs`

**Interfaces:**
- Consumes: `IHubClient.On<T>` from Task 1.
- Produces: `AudienceActor(IHubClientFactory, Uri, string session, string name, Random random, AudienceOptions? options = null, ITimeProvider? timeProvider = null)` — `random` is now required and positioned before the optionals.

- [ ] **Step 1: Write the failing test**

Create `Nuotti.SimKit.Tests/AudienceActorSubscriptionTests.cs`. It needs a hub client double that can push a snapshot and observe a submitted answer; `AudienceActorAnsweringTests.cs` already has a `CapturingHubClient` — read it and follow its shape rather than inventing a new one.

```csharp
using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Time;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class AudienceActorSubscriptionTests
{
    [Fact]
    public async Task Answers_a_guessing_snapshot_without_being_called_directly()
    {
        // The actor must wire itself up on start. Before this, OnStateAsync existed but
        // nothing ever invoked it, so a simulated audience never answered.
        var capturing = /* the double from AudienceActorAnsweringTests, capturing SubmitAnswer */;
        var actor = new AudienceActor(
            /* factory returning `capturing` */, new Uri("http://in-proc"), "dev", "aud-1",
            random: LaneRandom.ForLane(seed: 1, laneIndex: 0),
            options: new AudienceOptions { MinDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero },
            timeProvider: new ImmediateTimeProvider());

        await actor.StartAsync();
        await capturing.PushAsync(AGuessingSnapshot());

        capturing.SubmittedAnswers.Should().HaveCount(1);
    }

    [Fact]
    public async Task Stops_answering_once_the_actor_stops()
    {
        var capturing = /* same double */;
        var actor = new AudienceActor(/* … as above … */);

        await actor.StartAsync();
        await actor.StopAsync();
        await capturing.PushAsync(AGuessingSnapshot());

        capturing.SubmittedAnswers.Should().BeEmpty();
    }

    static GameStateSnapshot AGuessingSnapshot() => /* Phase.Guessing, 4 choices, SongIndex 0 */;
}
```

Fill the elided pieces from the existing double and from `GameStateSnapshot`'s real shape. The assertions above are the requirement; the scaffolding is yours to match to what is already there.

- [ ] **Step 2: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj --filter FullyQualifiedName~AudienceActorSubscriptionTests`

Expected: FAIL — the actor never subscribes, so no answer is submitted. Confirm the failure is an empty `SubmittedAnswers`, not a compile error from your scaffolding.

- [ ] **Step 3: Subscribe and require the seed**

In `Nuotti.SimKit/Actors/AudienceActor.cs`:

- Change the constructor to take `Random random` as a required parameter and assign `_random = random`. Delete the `_options.RandomSeed.HasValue ? … : Random.Shared` expression.
- If `AudienceOptions.RandomSeed` now has no reader, leave the property in place but note it in your report — removing it is a public-surface change beyond this task.
- Add the subscription lifecycle, mirroring `ProjectorActor`:

```csharp
    IDisposable? _subscription;

    protected override Task OnStartedAsync(CancellationToken cancellationToken = default)
    {
        if (Client is not null)
            _subscription = Client.On<GameStateSnapshot>(s => OnStateAsync(s, cancellationToken));
        return Task.CompletedTask;
    }

    protected override Task OnStoppingAsync(CancellationToken cancellationToken = default)
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }
```

- [ ] **Step 4: Update existing call sites**

```bash
~/.dotnet/dotnet build Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj 2>&1 | grep -E 'error CS'
```

Pass `LaneRandom.ForLane(seed, laneIndex)` at each construction. Where a test previously relied on `AudienceOptions.RandomSeed` to get deterministic behaviour, pass the equivalent seed through the new parameter instead — **do not change what those tests assert.** If a test's expected answer index changes because the random source changed, that is a legitimate re-baseline: say so explicitly in your report, and confirm the new expectation by running it rather than by reasoning.

- [ ] **Step 5: Run the suite and commit**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj`
Expected: PASS.

```bash
git add Nuotti.SimKit/Actors/AudienceActor.cs Nuotti.SimKit.Tests
git commit -m "fix(simkit): make the audience actor actually answer

OnStateAsync existed but nothing ever invoked it — ProjectorActor subscribes itself
on start and AudienceActor did not, so a simulated audience never submitted an
answer. It now subscribes to the snapshot broadcast on start and unsubscribes on
stop.

Random is a required constructor parameter; the Random.Shared fallback was the last
nondeterministic draw in SimKit production code."
```

---

### Task 4: Make `EngineActor` react to play and stop

`EngineActor.OnTrackPlayRequested`/`OnTrackStopped` exist but nothing calls them — `Emit` even carries the comment "In a real implementation, this would publish to the hub". Task 1 makes `PlayTrack` and `StopTrack` subscribable, so the engine can now react.

**Scope boundary:** the engine *reacting* is this task. The engine *publishing* `EngineStatusChanged` back is not — that needs a send member on `IHubClient` and a `QuizHub` round trip. Leave the existing local `_emitted` list as the observable outcome and note the publishing gap in your report.

**Files:**
- Modify: `Nuotti.SimKit/Actors/EngineActor.cs`
- Modify: `Nuotti.SimKit.Tests/EngineActorLifecycleTests.cs` (constructor call sites, if the signature changes)
- Test: `Nuotti.SimKit.Tests/EngineActorReactionTests.cs`

**Interfaces:**
- Consumes: `IHubClient.On<T>` from Task 1.
- Produces: `EngineActor` subscribing to `PlayTrack` and `StopTrack` on start; `Emitted` remains the observable outcome. Its `Random? random = null` parameter becomes a required `Random random`, for the same reason as Task 3.

- [ ] **Step 1: Write the failing test**

Create `Nuotti.SimKit.Tests/EngineActorReactionTests.cs`, following the double pattern in `EngineActorLifecycleTests.cs`. Required assertions:

```csharp
    [Fact]
    public async Task Reports_playing_when_a_play_track_arrives()
    {
        // engine with failureRate 0
        await actor.StartAsync();
        await hub.PushAsync(APlayTrack());

        actor.Emitted.Should().ContainSingle()
            .Which.Status.Should().Be(EngineStatus.Playing);
    }

    [Fact]
    public async Task Reports_ready_when_a_stop_arrives()
    {
        await actor.StartAsync();
        await hub.PushAsync(AStopTrack());

        actor.Emitted.Should().ContainSingle()
            .Which.Status.Should().Be(EngineStatus.Ready);
    }

    [Fact]
    public async Task Reports_error_when_the_failure_rate_is_certain()
    {
        // engine with failureRate 1.0
        await actor.StartAsync();
        await hub.PushAsync(APlayTrack());

        actor.Emitted.Should().ContainSingle()
            .Which.Status.Should().Be(EngineStatus.Error);
    }

    [Fact]
    public async Task Stops_reacting_once_the_actor_stops()
    {
        await actor.StartAsync();
        await actor.StopAsync();
        await hub.PushAsync(APlayTrack());

        actor.Emitted.Should().BeEmpty();
    }
```

Confirm `EngineStatusChanged`'s member name for the status before writing (`rtk proxy grep -rn 'record EngineStatusChanged' -A 4 Nuotti.Contracts/`).

- [ ] **Step 2: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj --filter FullyQualifiedName~EngineActorReactionTests`

Expected: FAIL — `Emitted` is empty, because nothing subscribes the engine to `PlayTrack`.

- [ ] **Step 3: Subscribe the engine**

In `Nuotti.SimKit/Actors/EngineActor.cs`, make `random` required, and add:

```csharp
    readonly List<IDisposable> _subscriptions = [];

    protected override Task OnStartedAsync(CancellationToken cancellationToken = default)
    {
        if (Client is not null)
        {
            _subscriptions.Add(Client.On<PlayTrack>(_ => { OnTrackPlayRequested(); return Task.CompletedTask; }));
            _subscriptions.Add(Client.On<StopTrack>(_ => { OnTrackStopped(); return Task.CompletedTask; }));
        }
        return Task.CompletedTask;
    }

    protected override Task OnStoppingAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }
```

Replace `Emit`'s "In a real implementation, this would publish to the hub" comment with an accurate one: the status is recorded locally, and publishing back to the hub needs a send member on `IHubClient` that does not exist yet.

- [ ] **Step 4: Run the suite and commit**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj`
Expected: PASS.

```bash
git add Nuotti.SimKit/Actors/EngineActor.cs Nuotti.SimKit.Tests
git commit -m "feat(simkit): make the engine actor react to play and stop

OnTrackPlayRequested and OnTrackStopped existed but nothing called them, because
PlayTrack and StopTrack were not subscribable. The engine now reacts to both and
records the resulting status.

Publishing EngineStatusChanged back to the hub still needs a send member on
IHubClient; the comment claiming otherwise is corrected rather than left."
```

---

### Task 5: One song, every participant, in process

The stage's payoff and its exit criterion.

**Files:**
- Test: `Nuotti.SimKit.InProc.Tests/SingleSongAllParticipantsTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-4.
- Produces: nothing. This is the proof.

- [ ] **Step 1: Write the test**

This one is written last deliberately: it composes finished parts, so there is no red-then-green cycle to stage — write it and expect it to fail only where the composition is genuinely wrong.

Create `Nuotti.SimKit.InProc.Tests/SingleSongAllParticipantsTests.cs`, asserting:

1. An `InProcBackend` and an `InProcHubClientFactory` for session `"dev"`.
2. A `PerformerActor`, a `ProjectorActor`, an `EngineActor` and **three** `AudienceActor`s, each with `LaneRandom.ForLane(seed: 1, laneIndex: n)` and `ImmediateTimeProvider`, all started.
3. A performer script driving one song through to a revealed answer, emitted via `InProcCommandEmitter`.
4. Assertions, each naming a distinct participant:
   - the projector's `ReceivedPhases` contains the expected phase sequence in order
   - all three audiences submitted exactly one answer for the song
   - the backend's final snapshot has a tally summing to 3
   - the engine's `Emitted` reflects the play that the script requested

Determine the reachable phase sequence from `GameReducer` and the phase commands' `AllowedPhases` rather than guessing it; if the script cannot reach `Reveal` without setlist/manifest setup the plan did not anticipate, **stop and report** rather than weakening the test to whatever passes.

- [ ] **Step 2: Run it**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.InProc.Tests/Nuotti.SimKit.InProc.Tests.csproj`
Expected: PASS.

- [ ] **Step 3: Run everything**

Run: `~/.dotnet/dotnet build Nuotti.sln -c Debug` → 0 errors
Run: `~/.dotnet/dotnet test Nuotti.sln` → all green, count ≥ 493 plus this plan's additions.

- [ ] **Step 4: Commit**

```bash
git add Nuotti.SimKit.InProc.Tests
git commit -m "test(simkit-inproc): one song, every participant, no network

A performer script drives a full song while a projector, an engine and three
audiences all react through the in-process hub — the first run in which every
simulated participant genuinely participates.

This is stage 2a's exit criterion: the seam carries what the Backend actually
broadcasts, and each actor reacts to its own part of it."
```

---

## Stage exit criteria

- `~/.dotnet/dotnet test Nuotti.sln` passes, count ≥ 493 plus this plan's additions.
- `IHubClient.On<T>` carries all five broadcast payloads; `HubWireNames` mirrors `HubBroadcastSubscriber` and is guarded by a test, including the `StopTrack` → `"Stop"` trap.
- `InProcHubClient` scopes delivery per session and gates it on start/stop.
- A simulated audience answers without being driven directly; a simulated engine reacts to play and stop.
- No `Random.Shared` remains in `Nuotti.SimKit` production code.
- `Nuotti.SimKit.csproj` still references only `Nuotti.Contracts`.

## Known gaps this stage deliberately leaves

- The engine cannot publish `EngineStatusChanged` back — `IHubClient` has no send member. Stage 2b or later.
- Clients do not apply `GameReducer` to `AnswerSubmitted`; they receive it but keep no local snapshot. Stage 2b, with lanes.
- No `SimWorld`, `SimWorldOptions` or lane roster — that is stage 2b.
- The CLI still parses `--jitter`, `--disconnect-rate` and `--instant` without wiring them to the injection factories. Carried from stage 1's review.
