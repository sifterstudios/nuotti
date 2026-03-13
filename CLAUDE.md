# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Nuotti** is a real-time interactive quiz and show platform (.NET 10 / C# 13). It has multiple UIs served simultaneously: a Blazor WASM audience app, a Blazor Server performer/host app, an Avalonia desktop projector, a SvelteKit static site, and a console audio engine.

## Commands

### .NET (Backend, Audience, Performer, Projector, etc.)

```bash
dotnet build Nuotti.sln
dotnet test                                          # all test projects
dotnet test tests/Nuotti.UnitTests/                 # specific project
dotnet test tests/Nuotti.IntegrationTests/
dotnet test tests/Nuotti.E2E/
dotnet test --filter "FullyQualifiedName~MyTestName" # single test
dotnet format --verify-no-changes                    # lint/format check
dotnet format                                        # auto-fix formatting
```

### Web (SvelteKit — `web/` directory)

```bash
cd web && npm install
npm run dev      # dev server
npm run build    # production build
npm run lint     # ESLint
npm run format   # Prettier
```

### Local Docker (PowerShell scripts in `tools/`)

```bash
pwsh tools/up-local.ps1    # build images and start all services
pwsh tools/down-local.ps1  # stop and clean up
```

Local service ports: Backend `5210`, Audience `5280`, Web `5380`.

## Architecture

### Services & Projects

| Project | Type | Role |
|---|---|---|
| `Nuotti.Backend` | ASP.NET Core | REST API + SignalR hubs (`QuizHub`, `GameHub`, `LogHub`) |
| `Nuotti.Contracts` | Class library | Shared DTOs, events, reducers, design tokens — **no dependencies** |
| `Nuotti.Audience` | Blazor WASM | Participant-facing UI |
| `Nuotti.Performer` | Blazor Server | Host/presenter control panel |
| `Nuotti.Projector` | Avalonia 11 | Native desktop display app |
| `Nuotti.AudioEngine` | Console app | PortAudio-based audio playback |
| `Nuotti.SimKit` | Console app | CLI automation/simulation tool |
| `ServiceDefaults` | Class library | Shared Serilog + OpenTelemetry setup |
| `web/` | SvelteKit | Static marketing/landing site |

### Communication & State

- **SignalR** (real-time): all clients connect to `GameHub` / `QuizHub` for live state sync
- **JSON serialization**: two conventions — camelCase for REST (`ContractsJson.RestOptions`), PascalCase for SignalR hubs (`ContractsJson.HubOptions`)
- **In-memory stores**: `ISessionStore` and `IGameStateStore` — not persisted, designed for single-node operation
- **Event bus**: `InMemoryEventBus` in Backend with typed subscribers; domain events live in `Nuotti.Contracts/V1/Event/`

### Contracts (`Nuotti.Contracts`)

The contracts library is the hub of inter-service communication. It is versioned (MAJOR.MINOR.PATCH):
- MAJOR bump = breaking change; MINOR = backward-compatible addition
- All message types, enums, reducers, and the design system live here
- Reducers under `V1/Reducer/` and `V1/Eventing/` process domain events into state

### Testing

Tests use **xUnit** + **Verify** (snapshot testing). Integration tests use `Microsoft.AspNetCore.Mvc.Testing` for in-process hosting. SignalR tests use FakeClient/FakeClientProxy helpers.

```
tests/Nuotti.UnitTests/            # pure logic, contracts, reducers
tests/Nuotti.IntegrationTests/     # service-level, in-process API
tests/Nuotti.E2E/                  # Playwright full-system flows
Nuotti.Backend.Tests/              # API + hub tests
Nuotti.Contracts.Tests/            # serialization + reducer tests
```

### CI/CD

- **test.yml**: runs dotnet test, dotnet format, ESLint/Prettier on every push/PR to main
- **build-and-push.yml**: builds Docker images for Backend, Audience, and Web; pushes to GHCR (`ghcr.io/sifterstudios/nuotti-*`)
- Self-hosted runner on Unraid; images deployed via Docker Compose

## Key Conventions

- `global.json` pins .NET 10 RC — use `rollForward: latestMajor` with `allowPrerelease: true`
- All projects reference `ServiceDefaults` for consistent logging and telemetry setup
- Options pattern (`NuottiOptions`) for app configuration with `NUOTTI_` env var prefix in deployment
- Design tokens and theming live in `Nuotti.Contracts/V1/Design/`; see `Nuotti.Audience/README.md` and `Nuotti.Projector/README_THEMING.md` for theming docs
- Structured audit logs and runtime feature flags are part of the ops layer — see `docs/ops-runbook.md`
- Flaky tests are tracked in `docs/FLAKY_TESTS.md` and marked with `[Trait("Flaky", "true")]`
