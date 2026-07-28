# Harness Unblock (Stage 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the three gaps that currently make a SimKit trace harness impossible to build, ending with SimKit able to drive a full session in-process, deterministically, with no network.

**Architecture:** Three independent unblocks plus the project that ties them together. `PhasePresenter`/`ViewSpec` move out of the Avalonia assembly so presentation can be simulated headlessly. `ICommandEmitter` — which today has no implementation anywhere — gains an HTTP one for real mode and an in-process one for test mode. Latency and chaos injection stop calling `Task.Delay` and `Random.Shared` so runs become reproducible.

**Tech Stack:** .NET 10, xUnit, FluentAssertions, Avalonia 11.3 (Projector only), ASP.NET Core (Backend).

**Spec:** `docs/superpowers/specs/2026-07-28-simkit-trace-viewer-design.md` — this plan is Stage 1 of the six-stage order in that spec's *Implementation order* section.

## Global Constraints

- **Build and test with `~/.dotnet/dotnet`, never bare `dotnet`.** The asdf shim on this machine exits 0 without compiling, so a bare `dotnet build` reports success on code that does not compile.
- Target framework `net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` on every new project.
- When editing an existing test project, keep the package versions it already pins (`Nuotti.SimKit.Tests` is on xunit 2.9.3 / Test.Sdk 17.12.0; `Nuotti.Projector.Tests` on 2.9.2 / 17.8.0). New test projects follow `Nuotti.Projector.Tests`.
- **`Nuotti.SimKit.Tests` does not currently reference FluentAssertions.** Task 2 adds it. Do not write FluentAssertions syntax in that project before that step.
- **`Nuotti.SimKit.csproj` must keep exactly one `ProjectReference`: `Nuotti.Contracts`.** Anything needing the Backend goes in `Nuotti.SimKit.InProc`. A Backend reference leaking into SimKit puts ASP.NET Core in the CLI.
- Namespaces of moved files do not change. Only the assembly they live in changes. This keeps the diff in `Nuotti.Projector` to project references and call sites.
- Conventional commits.
- Add every new project to `Nuotti.sln`.

---

### Task 1: Extract `Nuotti.Projector.Presentation`

`PhasePresenter` and `ResponsiveTypographyService` take `Avalonia.Size`, so today the presentation layer cannot be referenced without dragging in Avalonia. Replace that one type with a local `WindowSize` and move the Avalonia-free presentation code into its own assembly.

**Files:**
- Create: `Nuotti.Projector.Presentation/Nuotti.Projector.Presentation.csproj`
- Create: `Nuotti.Projector.Presentation/WindowSize.cs`
- Move (via `git mv`, namespaces unchanged):
  - `Nuotti.Projector/Presentation/ViewSpec.cs` → `Nuotti.Projector.Presentation/ViewSpec.cs`
  - `Nuotti.Projector/Presentation/PhasePresenter.cs` → `Nuotti.Projector.Presentation/PhasePresenter.cs`
  - `Nuotti.Projector/Models/ProjectorSettings.cs` → `Nuotti.Projector.Presentation/Models/ProjectorSettings.cs`
  - `Nuotti.Projector/Services/ContentSafetyService.cs` → `Nuotti.Projector.Presentation/Services/ContentSafetyService.cs`
  - `Nuotti.Projector/Services/LocalizationService.cs` → `Nuotti.Projector.Presentation/Services/LocalizationService.cs`
  - `Nuotti.Projector/Services/ResponsiveTypographyService.cs` → `Nuotti.Projector.Presentation/Services/ResponsiveTypographyService.cs`
- Modify: `Nuotti.Projector/Nuotti.Projector.csproj` (add ProjectReference)
- Modify: `Nuotti.Projector.Tests/Nuotti.Projector.Tests.csproj` (add ProjectReference)
- Modify: call sites of `Present(...)` and `CalculateFontSizeFromWindow(...)` (located in Step 6)
- Test: `Nuotti.Projector.Tests/PresentationAssemblyTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `Nuotti.Projector.Presentation.WindowSize` — `public readonly record struct WindowSize(double Width, double Height)`. `PhasePresenter.Present(GameStateSnapshot state, ProjectorSettings settings, WindowSize windowSize)` returns `ViewSpec`. Task 4 and all later stages depend on this assembly being Avalonia-free.

- [ ] **Step 1: Write the failing test**

Create `Nuotti.Projector.Tests/PresentationAssemblyTests.cs`:

```csharp
using System;
using FluentAssertions;
using Nuotti.Projector.Presentation;
using Xunit;

namespace Nuotti.Projector.Tests;

public class PresentationAssemblyTests
{
    /// <summary>
    /// The presentation layer must be usable from the SimKit harness and from tests without a
    /// window. If Avalonia creeps back into this assembly, headless simulation breaks.
    /// </summary>
    [Fact]
    public void Presentation_assembly_does_not_reference_Avalonia()
    {
        var referenced = typeof(PhasePresenter).Assembly.GetReferencedAssemblies();

        referenced.Should().NotContain(
            a => a.Name != null && a.Name.StartsWith("Avalonia", StringComparison.Ordinal));
    }

    [Fact]
    public void WindowSize_carries_width_and_height()
    {
        var size = new WindowSize(1920, 1080);

        size.Width.Should().Be(1920);
        size.Height.Should().Be(1080);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.Projector.Tests/Nuotti.Projector.Tests.csproj --filter FullyQualifiedName~PresentationAssemblyTests`

Expected: FAIL to compile with `CS0246: The type or namespace name 'WindowSize' could not be found`. That compile failure *is* the red state — do not proceed until you have seen it.

- [ ] **Step 3: Create the project and `WindowSize`**

`Nuotti.Projector.Presentation/Nuotti.Projector.Presentation.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Nuotti.Contracts\Nuotti.Contracts.csproj" />
  </ItemGroup>

</Project>
```

`Nuotti.Projector.Presentation/WindowSize.cs`:

```csharp
namespace Nuotti.Projector.Presentation;

/// <summary>
/// Window dimensions in device-independent pixels.
/// </summary>
/// <remarks>
/// Replaces Avalonia.Size in the presentation layer. Avalonia.Size is the only reason
/// PhasePresenter could not be referenced without a UI framework; keeping our own two-field
/// record means the layer stays testable and simulatable without a window.
/// </remarks>
public readonly record struct WindowSize(double Width, double Height);
```

Add to the solution:

```bash
~/.dotnet/dotnet sln Nuotti.sln add Nuotti.Projector.Presentation/Nuotti.Projector.Presentation.csproj
```

- [ ] **Step 4: Move the six files**

```bash
mkdir -p Nuotti.Projector.Presentation/Models Nuotti.Projector.Presentation/Services
git mv Nuotti.Projector/Presentation/ViewSpec.cs                        Nuotti.Projector.Presentation/ViewSpec.cs
git mv Nuotti.Projector/Presentation/PhasePresenter.cs                  Nuotti.Projector.Presentation/PhasePresenter.cs
git mv Nuotti.Projector/Models/ProjectorSettings.cs                     Nuotti.Projector.Presentation/Models/ProjectorSettings.cs
git mv Nuotti.Projector/Services/ContentSafetyService.cs                Nuotti.Projector.Presentation/Services/ContentSafetyService.cs
git mv Nuotti.Projector/Services/LocalizationService.cs                 Nuotti.Projector.Presentation/Services/LocalizationService.cs
git mv Nuotti.Projector/Services/ResponsiveTypographyService.cs         Nuotti.Projector.Presentation/Services/ResponsiveTypographyService.cs
```

Do **not** edit the `namespace` declarations in these files. `ProjectorSettings` stays `Nuotti.Projector.Models`, the three services stay `Nuotti.Projector.Services`. Only the assembly changes, so no `using` statement anywhere else in the Projector needs touching.

- [ ] **Step 5: Replace `Avalonia.Size` with `WindowSize` in the two files that use it**

In `Nuotti.Projector.Presentation/PhasePresenter.cs`:
- Delete the line `using Avalonia;`
- Change the signature `public ViewSpec Present(GameStateSnapshot state, ProjectorSettings settings, Size windowSize)` to `public ViewSpec Present(GameStateSnapshot state, ProjectorSettings settings, WindowSize windowSize)`

In `Nuotti.Projector.Presentation/Services/ResponsiveTypographyService.cs`:
- Delete the line `using Avalonia;`
- Add `using Nuotti.Projector.Presentation;`
- Change `public double CalculateFontSizeFromWindow(double minSize, double maxSize, Size windowSize, double safeAreaMargin = 0.05)` to `public double CalculateFontSizeFromWindow(double minSize, double maxSize, WindowSize windowSize, double safeAreaMargin = 0.05)`

The body of `CalculateFontSizeFromWindow` reads `windowSize.Width` and `windowSize.Height`, which `WindowSize` provides — the body needs no change.

- [ ] **Step 6: Wire up references and fix call sites**

Add to `Nuotti.Projector/Nuotti.Projector.csproj` and `Nuotti.Projector.Tests/Nuotti.Projector.Tests.csproj`, inside the `ItemGroup` that already holds `ProjectReference` entries:

```xml
<ProjectReference Include="..\Nuotti.Projector.Presentation\Nuotti.Projector.Presentation.csproj" />
```

Find every call site that passes an `Avalonia.Size`:

```bash
rtk proxy grep -rn 'CalculateFontSizeFromWindow\|\.Present(' --include='*.cs' Nuotti.Projector Nuotti.Projector.Tests
```

At each one, replace the Avalonia size argument with a `WindowSize`. The two shapes you will encounter:

- `presenter.Present(state, settings, Bounds.Size)` becomes `presenter.Present(state, settings, new WindowSize(Bounds.Width, Bounds.Height))`
- `presenter.Present(state, settings, new Size(1920, 1080))` becomes `presenter.Present(state, settings, new WindowSize(1920, 1080))`

Add `using Nuotti.Projector.Presentation;` to any file where `WindowSize` is now unresolved.

- [ ] **Step 7: Run the full Projector test suite**

Run: `~/.dotnet/dotnet test Nuotti.Projector.Tests/Nuotti.Projector.Tests.csproj`

Expected: PASS, including the pre-existing `PhasePresenterTests` and both new tests. `PhasePresenterTests` passing unchanged (apart from its `WindowSize` call sites) is the evidence that the move altered no behaviour.

- [ ] **Step 8: Build the whole solution**

Run: `~/.dotnet/dotnet build Nuotti.sln -c Debug`

Expected: PASS. This catches any Projector file that referenced the moved types through a path you missed.

- [ ] **Step 9: Commit**

```bash
git add Nuotti.Projector.Presentation Nuotti.Projector Nuotti.Projector.Tests Nuotti.sln
git commit -m "refactor(projector): extract an Avalonia-free presentation assembly

PhasePresenter and ResponsiveTypographyService took Avalonia.Size, so the
presentation layer could not be referenced without a UI framework. A local
WindowSize record replaces it, and the Avalonia-free presentation code moves
to Nuotti.Projector.Presentation.

Namespaces are unchanged, so only project references and Size call sites move.
A guard test fails if Avalonia ever returns to the assembly."
```

---

### Task 2: `HttpCommandEmitter`

`ICommandEmitter` has no implementation anywhere in the repo. `PerformerActor.BuildCommandsFromScript` produces commands and nothing sends them, which is why SimKit cannot currently drive a session. This adds the real-mode implementation, posting to the Backend's phase endpoints.

**Files:**
- Create: `Nuotti.SimKit/Hub/CommandRejectedException.cs`
- Create: `Nuotti.SimKit/Hub/HttpCommandEmitter.cs`
- Modify: `Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj` (add FluentAssertions)
- Test: `Nuotti.SimKit.Tests/HttpCommandEmitterTests.cs`

**Interfaces:**
- Consumes: `ICommandEmitter` (exists, `Nuotti.SimKit.Actors`), `ContractsJson.RestOptions` (exists, `Nuotti.Contracts.V1`).
- Produces:
  - `public sealed class CommandRejectedException : Exception` with `public CommandBase Command { get; }` and `public string ResponseBody { get; }`.
  - `public sealed class HttpCommandEmitter(HttpClient http) : ICommandEmitter` with `Task EmitAsync(CommandBase command, CancellationToken cancellationToken = default)`.
  - `public static IReadOnlyDictionary<Type, string> HttpCommandEmitter.Routes` — command type to phase-endpoint route segment. Task 4 does **not** use this; later stages may.

- [ ] **Step 1: Add FluentAssertions to the SimKit test project**

`Nuotti.SimKit.Tests` does not reference it yet, and the tests below use it. Add to the existing `PackageReference` `ItemGroup` in `Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj`:

```xml
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

Version 6.12.0 matches the other test projects in the solution. Do not take 8.x — its licence terms changed.

Run: `~/.dotnet/dotnet restore Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj`
Expected: PASS.

- [ ] **Step 2: Write the failing test**

Create `Nuotti.SimKit.Tests/HttpCommandEmitterTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class HttpCommandEmitterTests
{
    sealed class StubHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    static StartGame AStartGame() => new()
    {
        SessionCode = "dev",
        IssuedByRole = Role.Performer,
        IssuedById = "perf-1"
    };

    [Fact]
    public async Task Posts_to_the_phase_route_for_the_command_type()
    {
        var handler = new StubHandler(HttpStatusCode.Accepted);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        await emitter.EmitAsync(AStartGame());

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be("/v1/message/phase/start-game/dev");
    }

    [Fact]
    public async Task Serialises_the_command_with_rest_camel_case_options()
    {
        var handler = new StubHandler(HttpStatusCode.Accepted);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        await emitter.EmitAsync(AStartGame());

        handler.LastBody.Should().Contain("\"sessionCode\":\"dev\"");
    }

    [Fact]
    public async Task Treats_accepted_as_success()
    {
        var handler = new StubHandler(HttpStatusCode.Accepted);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        var act = async () => await emitter.EmitAsync(AStartGame());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Throws_with_the_response_body_when_the_command_is_rejected()
    {
        var handler = new StubHandler(HttpStatusCode.Forbidden, "{\"reasonCode\":\"UnauthorizedRole\"}");
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        var act = async () => await emitter.EmitAsync(AStartGame());

        var thrown = await act.Should().ThrowAsync<CommandRejectedException>();
        thrown.Which.ResponseBody.Should().Contain("UnauthorizedRole");
    }

    [Fact]
    public async Task Rejects_a_command_type_with_no_phase_route()
    {
        var handler = new StubHandler(HttpStatusCode.Accepted);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        var act = async () => await emitter.EmitAsync(new SubmitAnswer(0)
        {
            SessionCode = "dev",
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1"
        });

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
```

If `SubmitAnswer`'s constructor shape differs, substitute any command type that `PhaseEndpoints.MapPhaseEndpoints` does not map — the point of the test is the unmapped-type branch. Confirm the shape with:

```bash
rtk proxy grep -rn 'record SubmitAnswer' --include='*.cs' Nuotti.Contracts
```

- [ ] **Step 3: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj --filter FullyQualifiedName~HttpCommandEmitterTests`

Expected: FAIL to compile — `HttpCommandEmitter` and `CommandRejectedException` do not exist.

- [ ] **Step 4: Write the implementation**

`Nuotti.SimKit/Hub/CommandRejectedException.cs`:

```csharp
using Nuotti.Contracts.V1.Message;

namespace Nuotti.SimKit.Hub;

/// <summary>
/// Thrown when the Backend rejects a Command. Rejection is a return value on the server
/// (see SessionCommandProcessor); it becomes an exception here because a scenario that
/// issues an illegal Command has a bug in the scenario, and should stop loudly.
/// </summary>
public sealed class CommandRejectedException : Exception
{
    public CommandRejectedException(CommandBase command, string responseBody)
        : base($"Command {command.GetType().Name} for session '{command.SessionCode}' was rejected: {responseBody}")
    {
        Command = command;
        ResponseBody = responseBody;
    }

    public CommandBase Command { get; }
    public string ResponseBody { get; }
}
```

`Nuotti.SimKit/Hub/HttpCommandEmitter.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.SimKit.Actors;

namespace Nuotti.SimKit.Hub;

/// <summary>
/// Sends Commands to a running Backend over its phase endpoints.
/// </summary>
/// <remarks>
/// Mirrors Nuotti.Backend.Endpoints.PhaseEndpoints.MapPhaseEndpoints. If a Command type is
/// added there, add it to <see cref="Routes"/> too — the unmapped case throws rather than
/// guessing a route.
/// </remarks>
public sealed class HttpCommandEmitter(HttpClient http) : ICommandEmitter
{
    public static IReadOnlyDictionary<Type, string> Routes { get; } = new Dictionary<Type, string>
    {
        [typeof(CreateSession)] = "create-session",
        [typeof(StartGame)] = "start-game",
        [typeof(OpenAnswers)] = "open-answers",
        [typeof(EndSong)] = "end-song",
        [typeof(LockAnswers)] = "lock-answers",
        [typeof(RevealAnswer)] = "reveal-answer",
        [typeof(NextRound)] = "next-round",
        [typeof(PlaySong)] = "play-song",
        [typeof(GiveHint)] = "give-hint",
        [typeof(EndGame)] = "end-game",
    };

    public async Task EmitAsync(CommandBase command, CancellationToken cancellationToken = default)
    {
        if (!Routes.TryGetValue(command.GetType(), out var route))
        {
            throw new NotSupportedException(
                $"{command.GetType().Name} has no phase endpoint. Commands that are not phase " +
                "commands (for example SubmitAnswer) go through the hub, not this emitter.");
        }

        var uri = $"/v1/message/phase/{route}/{command.SessionCode}";
        using var content = JsonContent.Create(command, command.GetType(), options: ContractsJson.RestOptions);
        using var response = await http.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);

        // Duplicate is reported as Accepted by the Backend: the caller's intent is satisfied,
        // just not twice. Anything else is a rejection.
        if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new CommandRejectedException(command, body);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj --filter FullyQualifiedName~HttpCommandEmitterTests`

Expected: PASS, 5 tests.

- [ ] **Step 6: Commit**

```bash
git add Nuotti.SimKit/Hub/HttpCommandEmitter.cs Nuotti.SimKit/Hub/CommandRejectedException.cs Nuotti.SimKit.Tests/HttpCommandEmitterTests.cs Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj
git commit -m "feat(simkit): add the HTTP ICommandEmitter

ICommandEmitter had no implementation anywhere, so PerformerActor could build
commands from a script but nothing could send them. This posts phase commands
to the Backend's /v1/message/phase routes using ContractsJson.RestOptions, and
throws CommandRejectedException on anything other than Accepted.

Command types with no phase route throw rather than guessing a URL."
```

---

### Task 3: Make the hub subscription async

`IHubClient.OnGameStateChanged` takes `Action<GameStateSnapshot>`, so every handler that needs to await something is an async lambda bound to a void-returning delegate — an async void. Receive order is not guaranteed and exceptions are swallowed, which no amount of seeding can make reproducible. Change the delegate to `Func<GameStateSnapshot, Task>`.

This task is a pure signature change. **No behaviour changes here** — determinism is Task 4. Keeping them apart means the 17-implementor churn can be reviewed as the mechanical change it is.

**Files:**
- Modify: `Nuotti.SimKit/Hub/IHubClient.cs`
- Modify production implementors: `Nuotti.SimKit/Hub/HubConnectionFactory.cs` (`RealHubClient`), `Nuotti.SimKit/Hub/ConcurrencyThrottle.cs` (`ThrottlingHubClient`), `Nuotti.SimKit/Hub/LatencyInjection.cs` (`LatencyInjectingHubClient`), `Nuotti.SimKit/Hub/ChaosInjection.cs` (`ChaosInjectingHubClient`)
- Modify: `Nuotti.SimKit/Actors/ProjectorActor.cs:47`
- Modify these 13 test doubles: `ParallelismControlsTests.cs` (two — `CountingBlockingHubClient`, `NoopHubClient`), `ScriptMappingTests.cs`, `ChaosDisconnectTests.cs`, `BaselineScenarioTests.cs`, `ActorJoinTests.cs`, `ProjectorActorStateSubscriptionTests.cs`, `MultiSongScenarioTests.cs`, `AudienceActorAnsweringTests.cs`, `AudienceActorTimeControlTests.cs`, `LatencyInjectionTests.cs`, `EngineActorLifecycleTests.cs`
- Test: `Nuotti.SimKit.Tests/HubSubscriptionAsyncTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler)` on `IHubClient`. Task 4 and every later stage depend on this shape.

- [ ] **Step 1: Write the failing test**

Create `Nuotti.SimKit.Tests/HubSubscriptionAsyncTests.cs`:

```csharp
using FluentAssertions;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class HubSubscriptionAsyncTests
{
    /// <summary>
    /// A publisher that awaits each handler, the way a deterministic in-process bus will.
    /// </summary>
    sealed class AwaitingHubClient : IHubClient
    {
        Func<GameStateSnapshot, Task>? _handler;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler)
        {
            _handler = handler;
            return new Sub(this);
        }

        public Task PublishAsync(GameStateSnapshot snapshot) => _handler?.Invoke(snapshot) ?? Task.CompletedTask;

        sealed class Sub(AwaitingHubClient owner) : IDisposable
        {
            public void Dispose() => owner._handler = null;
        }
    }

    static GameStateSnapshot ASnapshot() => new() { SessionCode = "dev" };

    [Fact]
    public async Task Publisher_can_await_an_async_handler_to_completion()
    {
        var client = new AwaitingHubClient();
        var finished = false;

        using var sub = client.OnGameStateChanged(async _ =>
        {
            await Task.Yield();
            finished = true;
        });

        await client.PublishAsync(ASnapshot());

        // With Action<T> this assertion raced: the publisher could not await the handler.
        finished.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_exceptions_reach_the_publisher_instead_of_vanishing()
    {
        var client = new AwaitingHubClient();
        using var sub = client.OnGameStateChanged(_ => throw new InvalidOperationException("boom"));

        var act = async () => await client.PublishAsync(ASnapshot());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}
```

`GameStateSnapshot`'s required members may differ. Construct a minimal valid one — check with:

```bash
rtk proxy grep -n 'record GameStateSnapshot' -A 20 Nuotti.Contracts/V1/Model/GameStateSnapshot.cs
```

If the SimKit test project already has a snapshot builder, use it instead of hand-constructing.

- [ ] **Step 2: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj --filter FullyQualifiedName~HubSubscriptionAsyncTests`

Expected: FAIL to compile — `AwaitingHubClient` does not satisfy `IHubClient`, whose member still takes `Action<GameStateSnapshot>`.

- [ ] **Step 3: Change the interface**

In `Nuotti.SimKit/Hub/IHubClient.cs`:

```csharp
    /// <summary>
    /// Subscribe to GameStateChanged broadcast from the hub.
    /// Returns IDisposable to allow unsubscription.
    /// </summary>
    /// <remarks>
    /// The handler returns a Task so the publisher can await it. With Action&lt;T&gt;, any handler
    /// that awaited was an async void: receive order was unguaranteed and exceptions were
    /// unobservable, which makes a recorded run irreproducible.
    /// </remarks>
    IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler);
```

- [ ] **Step 4: Update the four production implementors**

For each, change the parameter type to `Func<GameStateSnapshot, Task>`:

- `ConcurrencyThrottle.cs` — pure pass-through, only the signature changes.
- `HubConnectionFactory.cs` (`RealHubClient`) — it adapts a SignalR `On<T>` callback. SignalR's `On<T>` has an overload taking `Func<T, Task>`; switch to it and pass the handler straight through rather than calling it from a void callback. Inspect first: `rtk proxy sed -n '50,70p' Nuotti.SimKit/Hub/HubConnectionFactory.cs`
- `LatencyInjection.cs` — the existing `async snapshot =>` lambda becomes a legitimate `async` handler now that the delegate returns `Task`. Keep the body; it is no longer async void. **Leave `Task.Delay` and `SampleDelay()` exactly as they are — Task 4 changes those.**
- `ChaosInjection.cs` — same: the existing `async snapshot =>` wrapper is now well-typed. **Leave `Random.Shared` and `Task.Delay` alone; Task 4 owns them.**

- [ ] **Step 5: Update `ProjectorActor`**

`Nuotti.SimKit/Actors/ProjectorActor.cs:47` currently reads:

```csharp
            _subscription = Client.OnGameStateChanged(s => OnStateAsync(s).GetAwaiter().GetResult());
```

`OnStateAsync` already returns `Task`, so it now matches the delegate directly:

```csharp
            _subscription = Client.OnGameStateChanged(OnStateAsync);
```

This removes a second sync-over-async block.

- [ ] **Step 6: Update the 13 test doubles**

Find every remaining compile error:

```bash
~/.dotnet/dotnet build Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj 2>&1 | grep -E 'error CS'
```

For each double, apply the mechanical transformation:

- `public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler)` becomes `public IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler)`
- A stored field of type `Action<GameStateSnapshot>?` becomes `Func<GameStateSnapshot, Task>?`
- A double that *invokes* the handler (`handler(snapshot)`) becomes `await handler(snapshot)` if the calling method is async, or `handler(snapshot).GetAwaiter().GetResult()` if the trigger method is synchronous and only used by tests. Prefer making the trigger method async and awaiting.
- A subscribing test lambda `snapshot => { ...; }` becomes `snapshot => { ...; return Task.CompletedTask; }`

Do not change what any double asserts or records. This step is signature plumbing only.

- [ ] **Step 7: Run the SimKit suite**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj`

Expected: PASS. The baseline for this project is 44 tests; you should now have 46 (the two new ones), all passing, with no pre-existing test changed in what it asserts.

- [ ] **Step 8: Build the solution**

Run: `~/.dotnet/dotnet build Nuotti.sln -c Debug`

Expected: PASS, 0 errors.

- [ ] **Step 9: Commit**

```bash
git add Nuotti.SimKit/Hub Nuotti.SimKit/Actors/ProjectorActor.cs Nuotti.SimKit.Tests
git commit -m "refactor(simkit): make the hub subscription handler async

OnGameStateChanged took Action<GameStateSnapshot>, so every handler that needed
to await was an async lambda bound to a void-returning delegate. Receive order
was unguaranteed and handler exceptions were swallowed — neither survives being
recorded into a reproducible trace.

The handler now returns Task, so a publisher can await it. ProjectorActor loses
its GetAwaiter().GetResult() as a direct consequence.

Signature change only; latency and chaos behaviour is untouched."
```

---

### Task 4: Make latency and chaos deterministic

`LatencyInjectingHubClient` and `ChaosInjectingHubClient` call `Task.Delay` and `Random.Shared` directly. Both must route through `ITimeProvider` and a caller-supplied `Random`, or no scenario is reproducible and `--instant` still sleeps.

**Files:**
- Create: `Nuotti.SimKit/Time/DeterministicRandom.cs`
- Modify: `Nuotti.SimKit/Hub/LatencyInjection.cs` (the `LatencyInjectingHubClientFactory` and `LatencyInjectingHubClient` classes)
- Modify: `Nuotti.SimKit/Hub/ChaosInjection.cs` (the `ChaosInjectingHubClientFactory` and `ChaosInjectingHubClient` classes)
- Modify: `Nuotti.SimKit.Tests/LatencyInjectionTests.cs`, `Nuotti.SimKit.Tests/ChaosDisconnectTests.cs` (constructor call sites)
- Test: `Nuotti.SimKit.Tests/InjectionDeterminismTests.cs`

**Interfaces:**
- Consumes: `ITimeProvider`, `ImmediateTimeProvider` (exist, `Nuotti.SimKit.Time`); `IHubClient.OnGameStateChanged(Func<GameStateSnapshot, Task>)` as Task 3 left it.
- Produces:
  - `public static class DeterministicRandom` with `public static Random ForLane(int seed, int laneIndex)`.
  - `LatencyInjectingHubClientFactory(IHubClientFactory inner, ILatencyPolicyResolver resolver, ITimeProvider time, Func<Random> randomForClient)`.
  - `ChaosInjectingHubClientFactory(IHubClientFactory inner, IChaosPolicyResolver resolver, ITimeProvider time, Func<Random> randomForClient)`.

  `Func<Random>` rather than `Random`: a single `Random` shared across concurrently running lanes is not thread-safe and would produce different sequences run to run. The factory calls it once per `Create`, so each client gets its own instance.

- [ ] **Step 1: Write the failing test**

Create `Nuotti.SimKit.Tests/InjectionDeterminismTests.cs`:

```csharp
using System.Diagnostics;
using FluentAssertions;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class InjectionDeterminismTests
{
    sealed class RecordingHubClient : IHubClient
    {
        public List<string> Calls { get; } = new();
        public Task StartAsync(CancellationToken cancellationToken = default)
        { Calls.Add("start"); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken = default)
        { Calls.Add("stop"); return Task.CompletedTask; }
        public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
        { Calls.Add($"join:{role}"); return Task.CompletedTask; }
        public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
        { Calls.Add($"answer:{choiceIndex}"); return Task.CompletedTask; }
        public IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler) => new Noop();
        sealed class Noop : IDisposable { public void Dispose() { } }
    }

    sealed class SingleClientFactory(IHubClient client) : IHubClientFactory
    {
        public IHubClient Create(Uri baseAddress) => client;
    }

    static readonly Uri Any = new("http://localhost:5240");

    static LatencyPolicy SlowPolicy => new(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(400));

    static ILatencyPolicyResolver ResolverFor(LatencyPolicy policy) =>
        new DictionaryLatencyPolicyResolver(new Dictionary<string, LatencyPolicy> { ["audience"] = policy });

    [Fact]
    public async Task Immediate_time_provider_means_latency_costs_no_wall_clock()
    {
        var inner = new RecordingHubClient();
        var factory = new LatencyInjectingHubClientFactory(
            new SingleClientFactory(inner),
            ResolverFor(SlowPolicy),
            new ImmediateTimeProvider(),
            () => DeterministicRandom.ForLane(seed: 1, laneIndex: 0));

        var client = factory.Create(Any);
        var sw = Stopwatch.StartNew();
        await client.JoinAsync("dev", "audience");
        for (var i = 0; i < 20; i++) await client.SubmitAnswerAsync("dev", i);
        sw.Stop();

        // 21 operations at a 500ms mean would be over ten seconds of real sleeping.
        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
        inner.Calls.Should().HaveCount(21);
    }

    [Fact]
    public void Same_seed_gives_the_same_delay_sequence()
    {
        var policy = SlowPolicy;

        // One Random per run, drawn from ten times. Creating a fresh Random inside the loop
        // would yield ten identical values and the test would pass without proving anything.
        var runOne = DeterministicRandom.ForLane(seed: 7, laneIndex: 3);
        var first = Enumerable.Range(0, 10).Select(_ => policy.SampleDelay(runOne)).ToList();

        var runTwo = DeterministicRandom.ForLane(seed: 7, laneIndex: 3);
        var second = Enumerable.Range(0, 10).Select(_ => policy.SampleDelay(runTwo)).ToList();

        second.Should().Equal(first);
        first.Distinct().Should().HaveCountGreaterThan(1, "a jittered policy must vary its samples");
    }

    [Fact]
    public void Different_lanes_get_different_sequences_from_the_same_seed()
    {
        var a = DeterministicRandom.ForLane(seed: 7, laneIndex: 0);
        var b = DeterministicRandom.ForLane(seed: 7, laneIndex: 1);

        var fromA = Enumerable.Range(0, 5).Select(_ => a.Next()).ToList();
        var fromB = Enumerable.Range(0, 5).Select(_ => b.Next()).ToList();

        fromB.Should().NotEqual(fromA);
    }

    [Fact]
    public async Task Chaos_downtime_also_costs_no_wall_clock_under_immediate_time()
    {
        var inner = new RecordingHubClient();
        var chaos = new DictionaryChaosPolicyResolver(new Dictionary<string, ChaosPolicy>
        {
            // Probability 1.0 so a disconnect cycle fires on every receive-eligible operation.
            ["audience"] = new ChaosPolicy(1.0, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), ApplyToSends: true)
        });

        var factory = new ChaosInjectingHubClientFactory(
            new SingleClientFactory(inner),
            chaos,
            new ImmediateTimeProvider(),
            () => DeterministicRandom.ForLane(seed: 2, laneIndex: 0));

        var client = factory.Create(Any);
        await client.StartAsync();
        await client.JoinAsync("dev", "audience");

        var sw = Stopwatch.StartNew();
        await client.SubmitAnswerAsync("dev", 1);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
        inner.Calls.Should().Contain("stop");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj --filter FullyQualifiedName~InjectionDeterminismTests`

Expected: FAIL to compile — `DeterministicRandom` does not exist and both factories take two constructor arguments, not four.

- [ ] **Step 3: Add `DeterministicRandom`**

`Nuotti.SimKit/Time/DeterministicRandom.cs`:

```csharp
namespace Nuotti.SimKit.Time;

/// <summary>
/// Per-lane random sources derived from one run seed.
/// </summary>
/// <remarks>
/// One shared Random across concurrently running lanes is not thread-safe, and interleaving
/// would make the draw order — and therefore the run — irreproducible. Deriving one instance
/// per lane keeps each lane's sequence stable no matter how the lanes interleave.
/// </remarks>
public static class DeterministicRandom
{
    public static Random ForLane(int seed, int laneIndex) => new(HashCode.Combine(seed, laneIndex));
}
```

- [ ] **Step 4: Rewrite `LatencyInjectingHubClientFactory` and `LatencyInjectingHubClient`**

In `Nuotti.SimKit/Hub/LatencyInjection.cs`, add `using Nuotti.SimKit.Time;` at the top and replace both classes with:

```csharp
/// <summary>
/// Factory that wraps produced hub clients with latency injection based on the role used when joining.
/// </summary>
public sealed class LatencyInjectingHubClientFactory : IHubClientFactory
{
    private readonly IHubClientFactory _inner;
    private readonly ILatencyPolicyResolver _resolver;
    private readonly ITimeProvider _time;
    private readonly Func<Random> _randomForClient;

    public LatencyInjectingHubClientFactory(
        IHubClientFactory inner,
        ILatencyPolicyResolver resolver,
        ITimeProvider time,
        Func<Random> randomForClient)
    {
        _inner = inner;
        _resolver = resolver;
        _time = time;
        _randomForClient = randomForClient;
    }

    public IHubClient Create(Uri baseAddress)
        => new LatencyInjectingHubClient(_inner.Create(baseAddress), _resolver, _time, _randomForClient());
}

internal sealed class LatencyInjectingHubClient : IHubClient
{
    private readonly IHubClient _inner;
    private readonly ILatencyPolicyResolver _resolver;
    private readonly ITimeProvider _time;
    private readonly Random _random;
    private LatencyPolicy? _activePolicy;

    public LatencyInjectingHubClient(
        IHubClient inner, ILatencyPolicyResolver resolver, ITimeProvider time, Random random)
    {
        _inner = inner;
        _resolver = resolver;
        _time = time;
        _random = random;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => _inner.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default)
        => _inner.StopAsync(cancellationToken);

    public async Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
    {
        if (_resolver.TryGetPolicy(role, out var policy))
            _activePolicy = policy;
        if (_activePolicy is { ApplyToSends: true } p)
            await _time.Delay(p.SampleDelay(_random), cancellationToken).ConfigureAwait(false);
        await _inner.JoinAsync(session, role, name, cancellationToken).ConfigureAwait(false);
    }

    public async Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    {
        if (_activePolicy is { ApplyToSends: true } p)
            await _time.Delay(p.SampleDelay(_random), cancellationToken).ConfigureAwait(false);
        await _inner.SubmitAnswerAsync(session, choiceIndex, cancellationToken).ConfigureAwait(false);
    }

    public IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler)
    {
        return _inner.OnGameStateChanged(async snapshot =>
        {
            // Task 3 made the delegate return Task, so this awaits properly instead of
            // being an async void. Under ImmediateTimeProvider the delay is a no-op.
            if (_activePolicy is { ApplyToReceives: true } p)
                await _time.Delay(p.SampleDelay(_random)).ConfigureAwait(false);
            await handler(snapshot).ConfigureAwait(false);
        });
    }
}
```

- [ ] **Step 5: Rewrite the chaos factory and client the same way**

In `Nuotti.SimKit/Hub/ChaosInjection.cs`, add `using Nuotti.SimKit.Time;` and make these changes to `ChaosInjectingHubClientFactory` and `ChaosInjectingHubClient`:

- Give the factory the same two extra constructor parameters (`ITimeProvider time`, `Func<Random> randomForClient`) and pass `_time` and `_randomForClient()` into the client.
- Give the client `private readonly ITimeProvider _time;` and `private readonly Random _random;` set from the constructor.
- In `SubmitAnswerAsync`, replace `Random.Shared.NextDouble()` with `_random.NextDouble()`.
- In `OnGameStateChanged`, keep the lambda async (Task 3 made that well-typed) and swap the random source:

```csharp
    public IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler)
    {
        return _inner.OnGameStateChanged(async snapshot =>
        {
            var p = _activePolicy;
            if (p is { ApplyToReceives: true } pp && _random.NextDouble() < pp.Probability)
                await DisconnectCycleAsync(pp).ConfigureAwait(false);
            await handler(snapshot).ConfigureAwait(false);
        });
    }
```

- In `DisconnectCycleAsync`, replace `policy.SampleDowntime()` with `policy.SampleDowntime(_random)`, and replace both `await Task.Delay(...)` calls with `await _time.Delay(...)`. The 50ms retry backoff becomes `await _time.Delay(TimeSpan.FromMilliseconds(50), cancellationToken)`.

- [ ] **Step 6: Fix the existing test call sites**

The two existing suites construct these factories with two arguments. Find them:

```bash
rtk proxy grep -rn 'LatencyInjectingHubClientFactory\|ChaosInjectingHubClientFactory' --include='*.cs' Nuotti.SimKit.Tests
```

At each construction, append the two new arguments:

```csharp
new ImmediateTimeProvider(),
() => DeterministicRandom.ForLane(seed: 1, laneIndex: 0)
```

Add `using Nuotti.SimKit.Time;` to those test files.

If a test asserted that real time elapsed during injection, it is now asserting the wrong thing — it was measuring `Task.Delay`, and the whole point of this task is that the harness no longer sleeps. Rewrite such an assertion to check the recorded call sequence instead, and note the change in the commit body.

- [ ] **Step 7: Run the SimKit suite**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.Tests/Nuotti.SimKit.Tests.csproj`

Expected: PASS, including `LatencyInjectionTests`, `ChaosDisconnectTests` and the four new determinism tests.

- [ ] **Step 8: Commit**

```bash
git add Nuotti.SimKit/Time/DeterministicRandom.cs Nuotti.SimKit/Hub/LatencyInjection.cs Nuotti.SimKit/Hub/ChaosInjection.cs Nuotti.SimKit.Tests
git commit -m "fix(simkit): make latency and chaos injection deterministic

Both injecting clients called Task.Delay and Random.Shared directly, so --instant
still slept and no run could be reproduced. Delays now go through ITimeProvider
and draws come from a per-lane Random derived from the run seed, which is also
what makes the sequence stable when lanes interleave.

Both OnGameStateChanged handlers were async lambdas bound to Action<T> — async
void, so exceptions were unobservable and receive order was not guaranteed. They
are synchronous now; under ImmediateTimeProvider that blocks for zero time."
```

---

### Task 5: `Nuotti.SimKit.InProc` and the in-process emitter

The payoff task: SimKit drives a real session through a real `SessionCommandProcessor` with no sockets, no host and no ports. This is the substrate every later stage builds on.

**Files:**
- Create: `Nuotti.SimKit.InProc/Nuotti.SimKit.InProc.csproj`
- Create: `Nuotti.SimKit.InProc/InProcCommandEmitter.cs`
- Create: `Nuotti.SimKit.InProc/InProcBackend.cs`
- Create: `Nuotti.SimKit.InProc.Tests/Nuotti.SimKit.InProc.Tests.csproj`
- Test: `Nuotti.SimKit.InProc.Tests/InProcCommandEmitterTests.cs`

**Interfaces:**
- Consumes: `ICommandEmitter` (Task 2's namespace, `Nuotti.SimKit.Actors`), `CommandRejectedException` (Task 2), `PerformerActor.BuildCommandsFromScript` (exists).
- Produces:
  - `public sealed class InProcBackend : IDisposable` with `public ISessionCommandProcessor Processor { get; }`, `public IEventBus Bus { get; }`, `public IGameStateStore States { get; }`.
  - `public sealed class InProcCommandEmitter(ISessionCommandProcessor processor, Actor actor) : ICommandEmitter`.

  Stage 2 adds `InProcHubClientFactory` to this same project and builds `SimWorld` on `InProcBackend`. Its hub client will implement `OnGameStateChanged(Func<GameStateSnapshot, Task>)` as Task 3 defined it, and can therefore await each subscriber in registration order — the property the deterministic trace depends on.

- [ ] **Step 1: Write the failing test**

Create `Nuotti.SimKit.InProc.Tests/InProcCommandEmitterTests.cs`:

```csharp
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

        await emitter.EmitAsync(new CreateSession { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" });
        await emitter.EmitAsync(new StartGame { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" });

        var state = backend.States.Get("dev");
        state.Should().NotBeNull();
        state!.Phase.Should().NotBe(Phase.Idle);
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
        var create = new CreateSession { SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1" };

        await emitter.EmitAsync(create);
        var act = async () => await emitter.EmitAsync(create);

        // The same CommandId twice is an idempotency hit, not a rejection.
        await act.Should().NotThrowAsync();
    }
}
```

`IGameStateStore`'s read method may not be named `Get`. Confirm before writing the implementation:

```bash
rtk proxy grep -n 'interface IGameStateStore' -A 12 Nuotti.Backend/Sessions/*.cs
```

Use whatever that interface exposes, and adjust the assertion in the first test to match.

- [ ] **Step 2: Create the two projects**

`Nuotti.SimKit.InProc/Nuotti.SimKit.InProc.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Nuotti.SimKit\Nuotti.SimKit.csproj" />
    <ProjectReference Include="..\Nuotti.Backend\Nuotti.Backend.csproj" />
  </ItemGroup>

</Project>
```

`Nuotti.SimKit.InProc.Tests/Nuotti.SimKit.InProc.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Nuotti.SimKit.InProc\Nuotti.SimKit.InProc.csproj" />
  </ItemGroup>

</Project>
```

```bash
~/.dotnet/dotnet sln Nuotti.sln add Nuotti.SimKit.InProc/Nuotti.SimKit.InProc.csproj Nuotti.SimKit.InProc.Tests/Nuotti.SimKit.InProc.Tests.csproj
```

- [ ] **Step 3: Run test to verify it fails**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.InProc.Tests/Nuotti.SimKit.InProc.Tests.csproj`

Expected: FAIL to compile — `InProcBackend` and `InProcCommandEmitter` do not exist.

- [ ] **Step 4: Write `InProcBackend`**

`Nuotti.SimKit.InProc/InProcBackend.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Eventing;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Eventing;

namespace Nuotti.SimKit.InProc;

/// <summary>
/// A Backend with no web host: the real SessionCommandProcessor over in-memory stores and
/// the real InMemoryEventBus.
/// </summary>
/// <remarks>
/// SessionCommandProcessor has a plain constructor, so none of Kestrel, SignalR or a port is
/// needed to exercise the true command path. InMemoryEventBus invokes subscribers
/// synchronously in registration order, which is what makes a simulated run reproducible.
/// </remarks>
public sealed class InProcBackend : IDisposable
{
    public InProcBackend()
    {
        States = new InMemoryGameStateStore();
        Idempotency = new InMemoryIdempotencyStore();
        Bus = new InMemoryEventBus();
        Processor = new SessionCommandProcessor(
            States,
            Idempotency,
            Bus,
            NullLogger<SessionCommandProcessor>.Instance);
    }

    public IGameStateStore States { get; }
    public IIdempotencyStore Idempotency { get; }
    public IEventBus Bus { get; }
    public ISessionCommandProcessor Processor { get; }

    public void Dispose()
    {
        (Bus as IDisposable)?.Dispose();
        (Idempotency as IDisposable)?.Dispose();
    }
}
```

If `InMemoryGameStateStore` or `InMemoryIdempotencyStore` require constructor arguments, supply them; check with:

```bash
rtk proxy grep -n 'public InMemoryGameStateStore\|public InMemoryIdempotencyStore' Nuotti.Backend/Sessions/InMemoryGameStateStore.cs Nuotti.Backend/Idempotency/InMemoryIdempotencyStore.cs
```

- [ ] **Step 5: Write `InProcCommandEmitter`**

`Nuotti.SimKit.InProc/InProcCommandEmitter.cs`:

```csharp
using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1.Message;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.InProc;

/// <summary>
/// Applies Commands straight to a <see cref="ISessionCommandProcessor"/>, with no transport.
/// </summary>
/// <remarks>
/// The in-memory counterpart to HttpCommandEmitter, and one half of the fidelity swap: a
/// scenario is written once and run either through this or over HTTP without changing.
/// </remarks>
public sealed class InProcCommandEmitter(ISessionCommandProcessor processor, Actor actor) : ICommandEmitter
{
    public async Task EmitAsync(CommandBase command, CancellationToken cancellationToken = default)
    {
        var result = await processor
            .ApplyAsync(command.SessionCode, actor, command, correlationId: null, cancellationToken)
            .ConfigureAwait(false);

        // Duplicate is an idempotency hit, not a failure — the intent is satisfied. Only a
        // genuine rejection means the scenario asked for something illegal.
        if (result.Outcome == Outcome.Rejected)
            throw new CommandRejectedException(command, result.Problem?.ToString() ?? "rejected");
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.InProc.Tests/Nuotti.SimKit.InProc.Tests.csproj`

Expected: PASS, 3 tests.

- [ ] **Step 7: Add the end-to-end script test**

This is the deliverable that proves the stage. Append to `InProcCommandEmitterTests.cs`:

```csharp
    [Fact]
    public async Task Performer_script_drives_a_session_end_to_end_with_no_network()
    {
        using var backend = new InProcBackend();
        var emitter = new InProcCommandEmitter(backend.Processor, Performer);

        await emitter.EmitAsync(new CreateSession
        {
            SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
        });

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

        backend.States.Get("dev")!.Phase.Should().NotBe(Phase.Idle);
    }
```

`ScriptModel`/`ScriptStep`'s exact shape governs how the steps collection is built. Confirm before writing:

```bash
rtk proxy cat Nuotti.SimKit/Script/ScriptModels.cs
```

Build the single `StartSet` step however that file requires. `hubClientFactory: null!` is deliberate: `RunScriptAsync` only uses the emitter, and passing null proves the script path needs no hub connection. If `PerformerActor`'s constructor dereferences the factory, pass `SimKit`'s existing test double instead and note it.

- [ ] **Step 8: Run the test and the full solution build**

Run: `~/.dotnet/dotnet test Nuotti.SimKit.InProc.Tests/Nuotti.SimKit.InProc.Tests.csproj`
Expected: PASS, 4 tests.

Run: `~/.dotnet/dotnet build Nuotti.sln -c Debug`
Expected: PASS.

Run: `~/.dotnet/dotnet test Nuotti.sln`
Expected: PASS. Any pre-existing failures listed in `docs/FLAKY_TESTS.md` are acceptable; anything else is a regression from this stage and must be fixed before committing.

- [ ] **Step 9: Commit**

```bash
git add Nuotti.SimKit.InProc Nuotti.SimKit.InProc.Tests Nuotti.sln
git commit -m "feat(simkit): drive a session in-process with no network

InProcBackend stands up the real SessionCommandProcessor over in-memory stores
and the real InMemoryEventBus — no Kestrel, no SignalR, no port. InProcCommandEmitter
applies Commands straight to it, so a performer script now drives a full session
inside a test.

This is the in-memory half of the fidelity swap: the same scenario runs through
either this or HttpCommandEmitter unchanged. Keeping it in its own project keeps
the Backend dependency out of the SimKit CLI."
```

---

## Stage exit criteria

- `~/.dotnet/dotnet test Nuotti.sln` passes. Baseline before this plan was 467 tests, 0 failures, across 8 assemblies — the count may only go up.
- `Nuotti.Projector.Presentation` has no Avalonia reference, enforced by a test.
- `ICommandEmitter` has two implementations, both tested.
- `IHubClient.OnGameStateChanged` takes `Func<GameStateSnapshot, Task>`, and no sync-over-async remains on the receive path (`ProjectorActor` included).
- A scenario run with `ImmediateTimeProvider` consumes no wall-clock time in latency or chaos, enforced by a test.
- `Nuotti.SimKit.csproj` still references only `Nuotti.Contracts`.

Stage 2 (`SimWorld`, `InProcHubClientFactory`, lanes) starts from here and gets its own plan.
