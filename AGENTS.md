## Agent skills

### Issue tracker

Issues and PRDs are tracked in this repository's GitHub Issues. See `docs/agents/issue-tracker.md`.

### Triage labels

Use the five default triage labels. See `docs/agents/triage-labels.md`.

### Domain docs

This repository uses the single-context domain-doc layout. See `docs/agents/domain.md`.

## Cursor Cloud specific instructions

Environment is prepared by the startup update script; the notes below are durable, non-obvious
gotchas for developing here.

### Toolchain

- .NET SDK is pinned by `global.json` to `10.0.100-rc.1.25451.107` (a preview). It is installed
  under `~/.dotnet` and put on `PATH` (and `DOTNET_ROOT`) via `~/.bashrc`, so `dotnet` works in
  interactive shells. Node 22 / npm are preinstalled; `web/` uses **npm** (has `package-lock.json`).

### Running services (run projects individually; do NOT rely on the Aspire AppHost here)

- **Backend** (REST + SignalR, the core of the product): `dotnet run --project Nuotti.Backend`
  serves `http://localhost:5240` in `Development`. With no connection strings it transparently
  falls back to **in-memory** stores/event bus — no Postgres/Redis/Azure Storage required.
- **Web** (SvelteKit static frontend) in `web/`: `npm run dev` (Vite; default port 5173).
- The Aspire AppHost `Nuotti/Nuotti.csproj` orchestrates everything but requires Docker plus
  Postgres/Redis/an Azure Storage emulator **and** an Avalonia desktop Projector, so it is not
  suitable for headless cloud runs. Start the individual projects instead.
- Quick end-to-end sanity check (create session → upload manifest → push question → read state):
  see `tools/smoke-test.sh`, but note it pushes a question with `issuedByRole: 2` (Audience),
  which the backend now rejects with `403 "Only Performer may execute this command."` Use
  `issuedByRole: "Performer"` (Role enum: Performer=0, Projector=1, Audience=2, Engine=3).

### Tests

- Run test projects directly, e.g. `dotnet test tests/Nuotti.UnitTests/Nuotti.UnitTests.csproj`.
  Do **not** pass `--settings:.runsettings`: it references a legacy `TestSettings.testsettings`
  through an unexpanded `$(MSBuildProjectDirectory)`, which the .NET 10 RC test platform treats as
  embedded test settings and aborts the run (0 tests). Only coverage collection is lost this way.

### Known caveats (pre-existing; not environment issues)

- `Nuotti.SimKit.InProc` (and `Nuotti.SimKit.InProc.Tests`) do not compile: `InProcCommandEmitter.cs`
  and `InProcHubClient.cs` reference `Outcome` without `using Nuotti.Contracts.V1.Protocol;`. This is
  isolated — Backend/Contracts/Audience/Performer/Projector/AudioEngine/SimKit and the CI test
  projects all build and test fine.
- `dotnet format --verify-no-changes` reports whitespace/line-ending diffs on Linux checkouts:
  `.editorconfig` mandates `end_of_line = crlf` for `*.cs`, but there is no `.gitattributes`, so
  files are checked out with LF. This is a line-ending artifact, not real formatting drift.
- Web ESLint/Prettier are not runnable as configured: the required tools and plugins (`eslint`,
  `prettier`, `@typescript-eslint/*`, `eslint-plugin-svelte`, `svelte-eslint-parser`,
  `prettier-plugin-svelte`) are **not** declared in `web/package.json`, so `npx eslint`/`npx prettier`
  pull bare latest versions and fail (flat-config / missing-plugin errors).
