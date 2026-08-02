# Production packaging boundary

Nuotti has one development composition and two production deliverables.

## Local development

`dotnet run --project Nuotti` starts PostgreSQL, Redis, Azurite blob storage, Backend,
Audience, Performer, the current Projector adapter, and the Show Agent/Audio Engine. Aspire owns
service discovery, health, logs, traces, and startup ordering. PostgreSQL and Azurite use named
volumes so local proof data survives container recreation. A running Docker-compatible container
runtime is required for the three infrastructure resources.

## Cloud package

The Aspire publish manifest contains Backend, Audience, Performer, PostgreSQL, Redis, and blob
storage. The Windows Show Agent and current local Projector adapter are excluded. Production selects
managed implementations for the resource contracts; local Aspire uses containers and Azurite.

```powershell
dotnet run --project Nuotti -- --publisher manifest --output-path ../.artifacts/aspire-manifest.json
pwsh tools/verify-production-packaging.ps1 -Manifest .artifacts/aspire-manifest.json
```

## Venue package

The band-facing venue deliverable is **Nuotti Venue** (`Nuotti.Projector`): one Windows or macOS
app that pairs with a single Performer code and runs the stage display plus an in-process audio
engine. See `docs/superpowers/specs/2026-08-02-venue-one-app-design.md`.

```powershell
dotnet publish Nuotti.Projector/Nuotti.Projector.csproj -c Release -r win-x64 --self-contained true -o .artifacts/venue-win
dotnet publish Nuotti.Projector/Nuotti.Projector.csproj -c Release -r osx-arm64 --self-contained true -o .artifacts/venue-osx-arm64
dotnet publish Nuotti.Projector/Nuotti.Projector.csproj -c Release -r osx-x64 --self-contained true -o .artifacts/venue-osx-x64
```

`Nuotti.AudioEngine` remains a separate `win-x64` publish for development and debugging; it is not
the band path. Linux Venue packaging and split-machine Projector/Engine installs are backlog.

```powershell
dotnet publish Nuotti.AudioEngine/Nuotti.AudioEngine.csproj -c Release -r win-x64 --self-contained true -o .artifacts/show-agent
```

The executable remains named `Nuotti.AudioEngine`; the `show-agent` Aspire resource records the
production boundary until the stage-grade coordinator in #258 lands.

## Deployment proof

Before #246 supplies deployed evidence, run two Backend replicas and record these checks:

1. write a proof record, replace its Backend replica, and read the same record;
2. upload and download/hash-verify an object through a short-lived direct storage grant;
3. connect clients through separate replicas and verify Session fan-out through the backplane.

Record cloud account, region, build, topology, and trace IDs on the ticket. Local emulator success
is necessary evidence, but not a substitute for this deployed proof.

The `/infrastructure-proof` endpoints exist only when `Nuotti:InfrastructureProofEnabled=true` and
all resource connection strings are present. Never enable that unauthenticated probe on a public
production deployment; use an isolated test environment and disable it after evidence capture.
