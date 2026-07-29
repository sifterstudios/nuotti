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

        await backend.Bus.PublishAsync(new PlayTrack("file:///song.mp3")
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        }, CancellationToken.None);

        plays.Should().HaveCount(1);
    }
}
```

These shapes are verified against the tree at `31b3972` — use them as written:

- `IEventBus`: `IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task>)` and `Task PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default)`
- `CreateSession(string SessionId) : CommandBase`
- `PlayTrack(string FileUrl) : CommandBase`
- `StopTrack() : CommandBase`
- `QuestionPushed(string Text, string[] Options) : CommandBase`
- `AnswerSubmitted(string AudienceId, int ChoiceIndex) : EventBase`

`CommandBase` supplies `SessionCode`, `IssuedByRole` and `IssuedById` as init-only members, which is why they appear in the object initializer rather than the constructor.

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

Create `Nuotti.SimKit.Tests/AudienceActorSubscriptionTests.cs`. The existing `CapturingHubClient` in `AudienceActorAnsweringTests.cs` records answers but cannot push a snapshot (its subscription returns a no-op), so this file declares its own `file`-scoped double in the same style:

```csharp
using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class AudienceActorSubscriptionTests
{
    static AudienceActor AnAudience(PushingHubClientFactory factory) =>
        new(factory, new Uri("http://in-proc"), "dev", "aud-1",
            random: LaneRandom.ForLane(seed: 1, laneIndex: 0),
            options: new AudienceOptions { MinDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero },
            timeProvider: new ImmediateTimeProvider());

    static GameStateSnapshot AGuessingSnapshot() => new()
    {
        SessionCode = "dev",
        Phase = Phase.Guessing,
        SongIndex = 0,
        Choices = ["a", "b", "c", "d"],
    };

    [Fact]
    public async Task Answers_a_guessing_snapshot_without_being_called_directly()
    {
        // The actor must wire itself up on start. Before this, OnStateAsync existed but
        // nothing ever invoked it, so a simulated audience never answered.
        var factory = new PushingHubClientFactory();
        var actor = AnAudience(factory);

        await actor.StartAsync();
        await factory.Client!.PushAsync(AGuessingSnapshot());

        factory.Client.Answers.Should().HaveCount(1);
    }

    [Fact]
    public async Task Stops_answering_once_the_actor_stops()
    {
        var factory = new PushingHubClientFactory();
        var actor = AnAudience(factory);

        await actor.StartAsync();
        await actor.StopAsync();
        await factory.Client!.PushAsync(AGuessingSnapshot());

        factory.Client.Answers.Should().BeEmpty();
    }
}

file sealed class PushingHubClientFactory : IHubClientFactory
{
    public PushingHubClient? Client { get; private set; }
    public IHubClient Create(Uri baseAddress) => Client = new PushingHubClient();
}

file sealed class PushingHubClient : IHubClient
{
    Func<GameStateSnapshot, Task>? _onSnapshot;

    public List<(string Session, int ChoiceIndex)> Answers { get; } = new();

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    {
        Answers.Add((session, choiceIndex));
        return Task.CompletedTask;
    }

    public IDisposable On<T>(Func<T, Task> handler)
    {
        if (typeof(T) == typeof(GameStateSnapshot))
            _onSnapshot = s => handler((T)(object)s);
        return new Sub(this);
    }

    // Awaited, not discarded: a discarded Task would reintroduce the async-void hazard
    // stage 1 removed, and would make these assertions race.
    public Task PushAsync(GameStateSnapshot snapshot) => _onSnapshot?.Invoke(snapshot) ?? Task.CompletedTask;

    sealed class Sub(PushingHubClient owner) : IDisposable
    {
        public void Dispose() => owner._onSnapshot = null;
    }
}
```

`GameStateSnapshot` is an init-only record — `SessionCode`, `Phase`, `SongIndex` and `Choices` are the members this test needs. If `Choices` has a different element type than `string`, adapt the collection expression; the rest holds.

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

Create `Nuotti.SimKit.Tests/EngineActorReactionTests.cs`:

```csharp
using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class EngineActorReactionTests
{
    static EngineActor AnEngine(RelayHubClientFactory factory, double failureRate) =>
        new(factory, new Uri("http://in-proc"), "dev",
            failureRate: failureRate,
            random: LaneRandom.ForLane(seed: 3, laneIndex: 0));

    static PlayTrack APlayTrack() => new("file:///song.mp3")
    {
        SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
    };

    static StopTrack AStopTrack() => new()
    {
        SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
    };

    [Fact]
    public async Task Reports_playing_when_a_play_track_arrives()
    {
        var factory = new RelayHubClientFactory();
        var actor = AnEngine(factory, failureRate: 0);

        await actor.StartAsync();
        await factory.Client!.PushAsync(APlayTrack());

        actor.Emitted.Should().ContainSingle()
            .Which.Status.Should().Be(EngineStatus.Playing);
    }

    [Fact]
    public async Task Reports_ready_when_a_stop_arrives()
    {
        var factory = new RelayHubClientFactory();
        var actor = AnEngine(factory, failureRate: 0);

        await actor.StartAsync();
        await factory.Client!.PushAsync(AStopTrack());

        actor.Emitted.Should().ContainSingle()
            .Which.Status.Should().Be(EngineStatus.Ready);
    }

    [Fact]
    public async Task Reports_error_when_the_failure_rate_is_certain()
    {
        var factory = new RelayHubClientFactory();
        var actor = AnEngine(factory, failureRate: 1.0);

        await actor.StartAsync();
        await factory.Client!.PushAsync(APlayTrack());

        actor.Emitted.Should().ContainSingle()
            .Which.Status.Should().Be(EngineStatus.Error);
    }

    [Fact]
    public async Task Stops_reacting_once_the_actor_stops()
    {
        var factory = new RelayHubClientFactory();
        var actor = AnEngine(factory, failureRate: 0);

        await actor.StartAsync();
        await actor.StopAsync();
        await factory.Client!.PushAsync(APlayTrack());

        actor.Emitted.Should().BeEmpty();
    }
}

file sealed class RelayHubClientFactory : IHubClientFactory
{
    public RelayHubClient? Client { get; private set; }
    public IHubClient Create(Uri baseAddress) => Client = new RelayHubClient();
}

file sealed class RelayHubClient : IHubClient
{
    readonly Dictionary<Type, Func<object, Task>> _handlers = new();

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IDisposable On<T>(Func<T, Task> handler)
    {
        _handlers[typeof(T)] = payload => handler((T)payload);
        return new Sub(this, typeof(T));
    }

    public Task PushAsync<T>(T payload) where T : notnull =>
        _handlers.TryGetValue(typeof(T), out var h) ? h(payload) : Task.CompletedTask;

    sealed class Sub(RelayHubClient owner, Type key) : IDisposable
    {
        public void Dispose() => owner._handlers.Remove(key);
    }
}
```

`EngineStatusChanged` is `record EngineStatusChanged(EngineStatus Status, double LatencyMs)`, so `.Status` is correct. `StopTrack` is `record StopTrack() : CommandBase` — no constructor arguments, session details via the initializer.

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

---

## Addendum: Tasks 6-8 — the two production defects Task 5 uncovered

Task 5 proved `Reveal` is reachable and wrote the projector and engine assertions, then stopped
rather than bending the two audience assertions into something that would pass. Two pre-existing
production defects blocked them. The decision was to fix both here rather than defer.

**Defect A — `PlaySong` can never fire.** It declares `AllowedPhases = [Play]` and
`AllowedSourcePhases = [Reveal]`. `SessionCommandProcessor.Guard` enforces both, so the session
would have to be in `Play` and `Reveal` at once.

**Defect B — answers can never be tallied.** No command or event ever populates
`GameStateSnapshot.Choices`; the only writers are the snapshot's own constructors. But
`GameReducer` reads it to bounds-check an incoming answer (`idx >= state.Choices.Count`) and to
size the tally array. So through the real command path `Choices` is always empty, every
`AnswerSubmitted` is out of range, and the reducer silently ignores it. `QuestionPushed` carries
the options but is routed as a relay that touches no state.

### Design decisions taken, with their evidence

**A is a one-value correction, not a judgement call.** Every other command implementing both
`IPhaseRestricted` and `IPhaseChange` has `AllowedPhases == AllowedSourcePhases` — verified across
all eight (`EndGame`, `EndSong`, `LockAnswers`, `NextRound`, `OpenAnswers`, `RevealAnswer`,
`StartGame`, and `PlaySong` itself as the outlier). `PlaySong`'s XML comment reads "Allowed phases:
Play", which describes its *target* phase; that is almost certainly how the wrong value arrived.
`AllowedPhases` becomes `[Reveal]`.

**B needs a new Event, because the reducer consumes Events only.** `CONTEXT.md` is explicit:
"Event — something that happened... Events are what the Reducer consumes." Feeding the
`QuestionPushed` *command* to the reducer would violate that split. Three options were considered:

- *Reducer case for the `QuestionPushed` command* — rejected, category violation as above.
- *Populate `Choices` at `NextRound` from the catalog* — rejected, `SongRef(Id, Title, Artist)`
  carries no choices, so there is nothing to populate from.
- *Emit a state Event alongside the relay* — chosen. `QuestionPushed` keeps its relay behaviour
  on the wire and additionally produces an Event the reducer consumes.

**ADR 0002 is amended, not contradicted.** Its decision is narrowly that relay commands skip the
idempotency stage. Its sentence "They change no game state" describes the status quo and stops
being true for `QuestionPushed`. The rationale still holds: re-setting identical choices is
idempotent in effect, so a duplicate remains harmless.

**Naming.** The new event is `QuestionOffered(string Text, IReadOnlyList<string> Choices)`,
following the past-tense fact convention of `HintGiven`, `CatalogUpdated`, `AnswerSubmitted`,
`CorrectAnswerRevealed`. This is a naming call rather than a derived fact — if a better term
exists in the domain, rename before this spreads.

---

### Task 6: Fix `PlaySong`'s phase declaration

**Files:**
- Modify: `Nuotti.Contracts/V1/Message/Phase/PlaySong.cs`
- Test: `Nuotti.Contracts.Tests/V1/Message/PlaySongPhaseTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `PlaySong.AllowedPhases == [Phase.Reveal]`. Task 8 relies on `PlaySong` being
  applicable so the engine reacts to a real play command.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Nuotti.Contracts.V1.Message.Phase;
using Xunit;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;

namespace Nuotti.Contracts.Tests.V1.Message;

public class PlaySongPhaseTests
{
    static PlaySong ACommand() => new(new Nuotti.Contracts.V1.Model.SongId(Guid.NewGuid()))
    {
        SessionCode = "dev",
        IssuedByRole = Nuotti.Contracts.V1.Enum.Role.Performer,
        IssuedById = "perf-1"
    };

    [Fact]
    public void Is_applicable_from_at_least_one_phase()
    {
        var cmd = ACommand();

        // SessionCommandProcessor.Guard enforces AllowedPhases AND IsPhaseChangeAllowed, so a
        // command whose two declarations disjoint can never be applied from any phase at all.
        var applicable = System.Enum.GetValues<PhaseEnum>()
            .Where(p => cmd.AllowedPhases.Contains(p) && cmd.IsPhaseChangeAllowed(p))
            .ToList();

        applicable.Should().NotBeEmpty(
            "a command that satisfies neither guard simultaneously is dead code");
    }

    [Fact]
    public void Declares_the_same_source_phases_on_both_interfaces()
    {
        var cmd = ACommand();

        // Every other command implementing both interfaces keeps these in step; PlaySong was
        // the sole outlier, and its "Allowed phases: Play" comment described the TARGET phase.
        cmd.AllowedPhases.Should().BeEquivalentTo(cmd.AllowedSourcePhases);
    }
}
```

Confirm `SongId`'s constructor shape before writing (`rtk proxy grep -rn 'record SongId' -A 3 Nuotti.Contracts/V1/Model/`) and adapt.

- [ ] **Step 2: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.Contracts.Tests/Nuotti.Contracts.Tests.csproj --filter FullyQualifiedName~PlaySongPhaseTests`
Expected: FAIL — both tests. `applicable` is empty and the two collections differ.

- [ ] **Step 3: Fix the declaration**

In `Nuotti.Contracts/V1/Message/Phase/PlaySong.cs`, change `AllowedPhases` to `[Enum.Phase.Reveal]`
and correct the XML comment, which currently states the target phase as if it were the source:

```csharp
/// <summary>
/// Starts playing a track for the current song, moving the session to Play.
/// Allowed from: Reveal.
/// </summary>
```

- [ ] **Step 4: Run the suite and commit**

Run: `~/.dotnet/dotnet test Nuotti.Contracts.Tests/Nuotti.Contracts.Tests.csproj` → PASS
Run: `~/.dotnet/dotnet build Nuotti.sln -c Debug` → 0 errors

```bash
git add Nuotti.Contracts/V1/Message/Phase/PlaySong.cs Nuotti.Contracts.Tests
git commit -m "fix(contracts): make PlaySong applicable at all

PlaySong declared AllowedPhases [Play] and AllowedSourcePhases [Reveal]. Guard
enforces both, so the session would have needed to be in two phases at once and
the command could never be applied from any state.

Every other command implementing both interfaces keeps the two in step. The
\"Allowed phases: Play\" comment described the target phase, which is likely how
the wrong value arrived."
```

---

### Task 7: Populate `Choices` via a `QuestionOffered` event

**Files:**
- Create: `Nuotti.Contracts/V1/Event/QuestionOffered.cs`
- Modify: `Nuotti.Contracts/V1/Reducer/GameReducer.cs` (new case)
- Modify: `Nuotti.Backend/Commands/SessionCommandProcessor.cs` (`EffectsFor` and the publish map)
- Modify: `docs/adr/0002-relay-commands-are-at-least-once.md` (amend the premise)
- Modify: `CONTEXT.md` (add the term)
- Test: `Nuotti.Contracts.Tests/V1/Reducer/QuestionOfferedTests.cs`, `Nuotti.Backend.Tests/QuestionPushedEffectsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `sealed record QuestionOffered(string Text, IReadOnlyList<string> Choices) : EventBase`.
  After the reducer handles it, `GameStateSnapshot.Choices` holds the offered choices and
  `Tallies` is a zeroed array of the same length. Task 8 depends on both.

- [ ] **Step 1: Write the failing reducer test**

```csharp
using FluentAssertions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Reducer;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Reducer;

public class QuestionOfferedTests
{
    [Fact]
    public void Puts_the_choices_on_the_snapshot()
    {
        var state = GameReducer.Initial("dev");

        var next = GameReducer.Reduce(state, new QuestionOffered("Which song?", ["a", "b", "c", "d"])
        {
            SessionCode = "dev"
        });

        next.IsSuccess.Should().BeTrue();
        next.Value.Choices.Should().Equal("a", "b", "c", "d");
    }

    [Fact]
    public void Sizes_the_tallies_to_the_choices_and_zeroes_them()
    {
        var state = GameReducer.Initial("dev");

        var next = GameReducer.Reduce(state, new QuestionOffered("Which song?", ["a", "b", "c", "d"])
        {
            SessionCode = "dev"
        });

        // Without this, AnswerSubmitted's bounds check against Choices.Count rejects every
        // answer and no tally ever moves — the defect this task exists to fix.
        next.Value.Tallies.Should().HaveCount(4);
        next.Value.Tallies.Should().OnlyContain(t => t == 0);
    }

    [Fact]
    public void An_answer_is_counted_once_choices_are_offered()
    {
        var state = GameReducer.Initial("dev");
        state = GameReducer.Reduce(state, new QuestionOffered("Which song?", ["a", "b", "c", "d"])
        {
            SessionCode = "dev"
        }).Value;

        var next = GameReducer.Reduce(state, new AnswerSubmitted("aud-1", 2) { SessionCode = "dev" });

        next.IsSuccess.Should().BeTrue();
        next.Value.Tallies[2].Should().Be(1);
    }
}
```

`GameReducer.Reduce`'s return shape and `GameReducer.Initial`'s signature must be confirmed before
writing — the assertions above assume a result type with `IsSuccess`/`Value`. Read
`Nuotti.Contracts/V1/Reducer/GameReducer.cs` and adapt the mechanics **without weakening what is
asserted**. The third test is the one that matters: it is the defect, stated as a test.

- [ ] **Step 2: Run to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.Contracts.Tests/Nuotti.Contracts.Tests.csproj --filter FullyQualifiedName~QuestionOfferedTests`
Expected: FAIL to compile — `QuestionOffered` does not exist.

- [ ] **Step 3: Add the event**

`Nuotti.Contracts/V1/Event/QuestionOffered.cs`:

```csharp
namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// The question and its answer choices are now the ones on offer for the current round.
/// </summary>
/// <remarks>
/// Emitted alongside the QuestionPushed relay command. The relay carries the question to
/// clients on the wire; this event is what puts the choices into GameStateSnapshot, which
/// GameReducer needs before it can bounds-check an answer or size a tally. Before this
/// existed, Choices was never populated by any command or event, so every AnswerSubmitted
/// failed its bounds check and no tally ever moved.
/// </remarks>
public sealed record QuestionOffered(string Text, IReadOnlyList<string> Choices) : EventBase;
```

Match the style of the sibling events in that folder — check whether they redeclare positional
members as `required` properties (`AnswerSubmitted` does) and follow suit.

- [ ] **Step 4: Handle it in the reducer**

Add a case to `GameReducer.Reduce` that sets `Choices` to the event's choices and replaces
`Tallies` with a zeroed array of the same length. Follow the shape of the existing
`CatalogUpdated` case, which is the closest analogue — a fact that replaces a collection on the
snapshot. Do not touch `Phase`, `SongIndex` or scores.

- [ ] **Step 5: Emit it from the processor**

In `SessionCommandProcessor.EffectsFor`, `QuestionPushed` currently shares a relay arm with
`PlayTrack` and `StopTrack`. Split it out:

```csharp
            // QuestionPushed is still relayed untouched for the wire, but it now also produces a
            // state event: the choices have to reach GameStateSnapshot or the reducer cannot
            // bounds-check an answer. Idempotency stays off per docs/adr/0002 — re-offering the
            // same choices is idempotent in effect.
            case QuestionPushed pushed:
                return new Effects(
                    [pushed, new QuestionOffered(pushed.Text, pushed.Options)
                    {
                        SessionCode = pushed.SessionCode,
                        CorrelationId = correlation
                    }],
                    BroadcastSnapshot: true,
                    CheckIdempotency: false);

            // Relay Commands: forwarded to clients untouched, no state change, no idempotency
            // (docs/adr/0002). The reducer ignores them, so no snapshot is broadcast either.
            case PlayTrack:
            case StopTrack:
                return new Effects([command], BroadcastSnapshot: false, CheckIdempotency: false);
```

Confirm `EventBase`'s settable members (`SessionCode`, `CorrelationId`) before writing, and add
`QuestionOffered e => bus.PublishAsync(e, ct)` to the publish map alongside the other events —
without it the event reaches no subscriber and the processor logs "No publish mapping".

- [ ] **Step 6: Write the processor test**

`Nuotti.Backend.Tests/QuestionPushedEffectsTests.cs` must assert that applying a `QuestionPushed`
leaves the stored snapshot carrying the choices, and that a subsequent `SubmitAnswer` moves the
tally. Follow the construction style of the existing `SessionCommandProcessorTests`. The point is
end-to-end through the real processor, not the reducer in isolation.

- [ ] **Step 7: Amend ADR 0002 and CONTEXT.md**

In `docs/adr/0002-relay-commands-are-at-least-once.md`, the Context section states relay commands
"change no game state". Add a dated amendment rather than rewriting history:

```markdown
## Amendment — 2026-07-29

`QuestionPushed` now also produces a `QuestionOffered` event, so it does change game state; the
sentence above no longer describes it. The decision is unchanged and the rationale still holds:
re-offering the same choices is idempotent in effect, so a duplicate relay remains harmless.
`PlayTrack` and `StopTrack` are untouched and remain pure relays.
```

In `CONTEXT.md`, add to Core concepts, after **Event**:

```markdown
**Choices** — the answer options on offer for the current round. Carried to clients by the
`QuestionPushed` relay Command and put into the Snapshot by the `QuestionOffered` Event. The
Reducer needs them present to bounds-check an `AnswerSubmitted` and to size the tally.
```

- [ ] **Step 8: Run everything and commit**

Run: `~/.dotnet/dotnet build Nuotti.sln -c Debug` → 0 errors
Run: `~/.dotnet/dotnet test Nuotti.sln` → all green

```bash
git add Nuotti.Contracts Nuotti.Backend Nuotti.Contracts.Tests Nuotti.Backend.Tests docs/adr CONTEXT.md
git commit -m "fix(contracts): put offered choices into the snapshot

Nothing populated GameStateSnapshot.Choices — the only writers were the snapshot's
own constructors. GameReducer reads it to bounds-check an incoming answer and to
size the tally, so through the real command path every AnswerSubmitted was out of
range and silently ignored: answers could never be tallied.

QuestionPushed carries the options but was routed as a pure relay. It now also
emits QuestionOffered, an Event the reducer consumes. The relay behaviour on the
wire is unchanged, and idempotency stays off per docs/adr/0002 — re-offering the
same choices is idempotent in effect. ADR 0002's premise is amended accordingly."
```

---

### Task 8: Complete the full-participant run

With Tasks 6 and 7 landed, the two assertions Task 5 could not prove become provable.

**Files:**
- Modify: `Nuotti.SimKit.InProc.Tests/SingleSongAllParticipantsTests.cs` (the file Task 5 created)

**Interfaces:**
- Consumes: everything from Tasks 1-7.
- Produces: nothing. This is the stage exit criterion.

- [ ] **Step 1: Extend the existing test**

Read what Task 5 wrote and keep its projector and engine assertions — they were verified
non-vacuous by mutation and must not be disturbed. Add the two that were blocked:

- all three audiences submitted exactly one answer for the song
- the backend's final snapshot has a tally summing to 3

The script must now push a question before opening answers, so the choices reach the snapshot.
Use `LaneRandom.ForLane(seed: 1, laneIndex: n)` per participant and `ImmediateTimeProvider`.

- [ ] **Step 2: Prove each assertion is non-vacuous**

For each of the four, establish what would make it fail — by mutation where practical (comment out
the participant's subscription, re-run, confirm red, restore). Record the evidence per assertion in
your report. An assertion that holds whether or not its participant acted is worthless, and three
such assertions have already been caught on this project.

- [ ] **Step 3: Run everything and commit**

Run: `~/.dotnet/dotnet build Nuotti.sln -c Debug` → 0 errors
Run: `~/.dotnet/dotnet test Nuotti.sln` → all green

```bash
git add Nuotti.SimKit.InProc.Tests
git commit -m "test(simkit-inproc): one song, every participant, no network

A performer script drives a full song while a projector, an engine and three
audiences all react through the in-process hub. The audience half of this was
unprovable until the two defects it uncovered were fixed: PlaySong could never
fire, and offered choices never reached the snapshot so answers were never tallied.

This is stage 2a's exit criterion."
```

## Revised stage exit criteria

Superseding the list above:

- `~/.dotnet/dotnet test Nuotti.sln` passes.
- `IHubClient.On<T>` carries all five broadcast payloads; `HubWireNames` mirrors
  `HubBroadcastSubscriber`, guarded by a test including the `StopTrack` → `"Stop"` trap.
- `InProcHubClient` scopes delivery per session and gates it on start/stop.
- A simulated audience answers without being driven directly; a simulated engine reacts to play
  and stop; **and the answers are actually tallied**.
- No `Random.Shared` in `Nuotti.SimKit` production code.
- `Nuotti.SimKit.csproj` still references only `Nuotti.Contracts`.
- `PlaySong` is applicable from at least one phase.
- `GameStateSnapshot.Choices` is populated through the real command path.
