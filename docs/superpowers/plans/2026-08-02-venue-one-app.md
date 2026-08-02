# Nuotti Venue one-app Implementation Plan

> **For agentic workers:** Implement task-by-task with TDD at the listed seams.

**Goal:** Evolve `Nuotti.Projector` into a Venue shell that pairs once via UI, then runs projector display and an in-process audio engine from the same show-agent credential.

**Architecture:** Extract `VenueEngineHost` from AudioEngine (hub as Engine + shared token provider). After Projector pairing succeeds, start that host beside the existing projector hub connection (`deviceRole=projector`). One credential, two hub roles. No new backend lease type.

**Tech Stack:** .NET 10, Avalonia, SignalR Client, PortAudio/`SystemPlayer`, xUnit

## Global Constraints

- Platforms v1: Windows + macOS (Linux backlog)
- Split-machine not supported in v1
- Band-facing copy must not mention `--pair-code` / `NUOTTI_PAIR_CODE`
- #258 ASIO coordinator out of scope
- Assembly may remain `Nuotti.Projector`

### Task 1: VenueEngineHost

**Files:**
- Create: `Nuotti.AudioEngine/VenueEngineHost.cs`
- Test: `Nuotti.AudioEngine.Tests/VenueEngineHostTests.cs`
- Modify: make status/problem sinks usable from the host (same assembly)

**Produces:** `VenueEngineHost.StartAsync` / `StopAsync` / `DisposeAsync` taking `backendBaseUrl`, `sessionCode`, `Func<Task<string?>> accessTokenProvider`

- [ ] Failing test: host StartAsync builds hub URL without projector role and registers play handlers (injectable connection factory or verify started flag + dispose)
- [ ] Implement host: create player via existing backends, HubConnection with AccessTokenProvider, handlers for PlayTrack/TrackPlayRequested/TrackStopped, heartbeat, StartAsync
- [ ] Commit

### Task 2: Wire into Projector

**Files:**
- Modify: `Nuotti.Projector/Nuotti.Projector.csproj` (ProjectReference AudioEngine)
- Modify: `Nuotti.Projector/MainWindow.axaml.cs` (start/stop host on pair / revoke)
- Modify: `Nuotti.Projector/MainWindow.axaml` (Venue naming + pairing copy)
- Test: `Nuotti.Projector.Tests` orchestration stub if feasible; else rely on Engine host tests + manual

- [ ] After successful pair and on paired startup, `StartEngineHostAsync`
- [ ] On revoke / unpaired overlay, stop host
- [ ] Commit

### Task 3: Copy + docs

**Files:**
- Modify: `Nuotti.Performer/Shared/VenueDevicePairingPanel.razor`
- Modify: `docs/production-packaging.md`
- Modify: design success criteria as done notes if needed

- [ ] Performer text: one code for Nuotti Venue app
- [ ] Packaging: Venue is the band path on win/mac
- [ ] Commit
