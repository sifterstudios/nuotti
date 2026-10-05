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
- **Web** (SvelteKit static frontend / marketing + trial) in `web/`: `npm run dev` (Vite; default
  port 5173). This is **not** the Audience participant app — that is Blazor WASM
  `Nuotti.Audience`.
- The Aspire AppHost `Nuotti/Nuotti.csproj` orchestrates everything but requires Docker plus
  Postgres/Redis/an Azure Storage emulator **and** an Avalonia desktop Projector, so it is not
  suitable for headless cloud runs. Start the individual projects instead.
- Quick end-to-end sanity check (create session → upload manifest → push question → read state):
  `./tools/smoke-test.sh` (or `pwsh tools/smoke-test.ps1`). Push-question must use
  `issuedByRole: 0` / `"Performer"` (Role enum: Performer=0, Projector=1, Audience=2, Engine=3).
  Audience (`2`) is rejected with `403 "Only Performer may execute this command."`

### Tests

- Run test projects directly, e.g. `dotnet test tests/Nuotti.UnitTests/Nuotti.UnitTests.csproj`.
- CI uses `--settings:.runsettings` for coverage. That file is safe: the legacy MSTest
  `TestSettings.testsettings` block was removed. Prefer matching CI locally when collecting
  coverage: `dotnet test --settings:.runsettings --collect:"XPlat Code Coverage"`.
- Frontend lint (from `web/`): `npm run lint` and `npm run format:check`. Tools are pinned
  `devDependencies` with ESLint 9 flat config (`eslint.config.js`). Do not use bare
  `npx eslint` / `npx prettier` — they can pull a newer major and break.

### Line endings

- `.editorconfig` and `.gitattributes` both enforce **LF** for `*.cs` (and most text). CRLF is
  reserved for Windows scripts (`*.ps1`, `*.cmd`, `*.bat`). `dotnet format --verify-no-changes`
  should not report line-ending churn on a normal checkout.

### Doc / contract checks

- `tools/check-docs.ps1` and `tools/check-contracts.ps1` require PowerShell (`pwsh`). CI runs
  them when available; locally: `pwsh -File tools/check-docs.ps1` /
  `pwsh -File tools/check-contracts.ps1`.
