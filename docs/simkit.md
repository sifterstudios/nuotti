# Nuotti SimKit

SimKit is a lightweight command-line tool to simulate Nuotti clients and orchestration scenarios against a Nuotti Backend. It is designed to help you:

- Smoke-test a local backend quickly
- Try common load/chaos presets
- Prototype or script your own scenarios
- Collect simple run logs/metrics for inspection

This document covers:

- [Quickstart](#quickstart)
- [Presets](#presets)
- [Writing scenarios](#writing-scenarios)
- [Reading reports](#reading-reports)
- [Examples](#examples)
- [CI usage](#ci-usage)

> Note: The current implementation provides a validated CLI surface and scaffolding output. Full simulation behavior will evolve; the CLI contracts below are stable and covered by tests.

## Quickstart

Prerequisites:
- .NET SDK (as in [README.md](../README.md))

Build once:
- dotnet build Nuotti.sln -c Release

Run the CLI help (no backend required):
- dotnet run --project Nuotti.SimKit/Nuotti.SimKit.csproj -- --help

Run against a local backend (replace the URL/port to match your setup):
- dotnet run --project Nuotti.SimKit/Nuotti.SimKit.csproj -- run --backend http://localhost:5240 --session dev --preset baseline --instant

Flags summary:
- --backend <url> required for run
- --session <code> required for run
- --preset baseline | load | chaos
- --audiences <n>
- --jitter <ms>
- --disconnect-rate <0..1>
- --speed <x>
- --instant

See complete usage by running: dotnet run --project Nuotti.SimKit/Nuotti.SimKit.csproj -- --help

## Presets

Built-in presets configure a sensible mix of actors and timing. You can start with a preset and override individual knobs.

- baseline: minimal activity to verify end-to-end flow
- load: higher audience count and message rate (for local stress testing)
- chaos: injects disconnects and jitter for resilience testing

Override examples:
- Baseline with 25 audiences: --preset baseline --audiences 25
- Chaos with reduced jitter: --preset chaos --jitter 40
- Load with slower timing: --preset load --speed 0.5

## Writing scenarios

There are two ways to shape behavior:

1) Preset + overrides (recommended for most contributors):
   - Start from --preset baseline|load|chaos
   - Add overrides such as --audiences, --jitter, --disconnect-rate, --speed or --instant

2) Code-level scenarios (advanced):
   - Explore the Nuotti.SimKit project to add orchestrators and actors.
   - Relevant starting points:
     - Nuotti.SimKit/Program.cs (CLI definitions and argument parsing)
     - Nuotti.SimKit/Actors (orchestration scaffolding)
     - Nuotti.SimKit/Hub (hub client abstractions and latency injection)
     - Nuotti.SimKit/Time (timing primitives)
   - Add tests under Nuotti.SimKit.Tests to validate behavior deterministically.

Tips:
- Keep PRs small and lean on presets and overrides first.
- For integration with a live backend, prefer --instant during development to shorten cycles.

## Reading reports

When you run SimKit, it writes human-readable output to stdout. In CI we typically redirect this to an artifacts folder. You can do the same locally:

- PowerShell example (Windows):
  - New-Item -ItemType Directory -Force -Path sim-artifacts | Out-Null
  - dotnet run --project Nuotti.SimKit/Nuotti.SimKit.csproj -- run --backend http://localhost:5240 --session dev --preset baseline --instant *> sim-artifacts/sim-run.log

What to look for in the log:
- Effective configuration line (preset, speed/instant, overrides)
- Any warnings or errors
- Run summary (future versions will include structured metrics in JSON alongside logs)

## Examples

Common tasks:
- Show help: dotnet run --project Nuotti.SimKit/Nuotti.SimKit.csproj -- --help
- Baseline, instant: dotnet run --project Nuotti.SimKit/Nuotti.SimKit.csproj -- run --backend http://localhost:5240 --session dev --preset baseline --instant
- Load test with 100 audiences: dotnet run --project Nuotti.SimKit/Nuotti.SimKit.csproj -- run --backend http://localhost:5240 --session dev --preset load --audiences 100
- Chaos with 5% disconnect rate: dotnet run --project Nuotti.SimKit/Nuotti.SimKit.csproj -- run --backend http://localhost:5240 --session dev --preset chaos --disconnect-rate 0.05

## CI usage

We run a headless baseline in CI to prevent regressions. See the workflow for an end-to-end example of environment setup and artifact collection:
- .github/workflows/simkit-baseline.yml

That job builds, runs tests, and executes:
- dotnet run --project Nuotti.SimKit/Nuotti.SimKit.csproj -c Release -- run --backend http://localhost:5240 --session dev --preset baseline --instant

Resulting logs and test results are stored as artifacts for inspection.
