# ADR 0001 — No shared Blazor library until a second shared UI service exists

Date: 2026-07-27
Status: Accepted

## Context

`ThemeService` is duplicated between `Nuotti.Audience/Services/ThemeService.cs` and
`Nuotti.Performer/Services/ThemeService.cs` — roughly 175 lines each, differing by 171 diff
lines, with the Performer copy raising `OnThemeChanged` from its property setters and the
Audience copy not doing so.

The obvious fix is a shared Razor class library referenced by both apps. It was considered
and rejected for now.

It cannot live in `Nuotti.Contracts` — Contracts is referenced by `Nuotti.Projector`,
`Nuotti.AudioEngine` and `Nuotti.SimKit`, and `ThemeService` depends on `IJSRuntime`, so
placing it there would drag Blazor into the console and desktop hosts.

## Decision

Keep the two copies. Align their behaviour instead: port the Performer version's
change-notification setters into the Audience copy so both behave identically.

Create `Nuotti.Web.Shared` when — and only when — a **second** service genuinely needs to
be shared between the two Blazor apps.

## Rationale

Two call sites are not two adapters. A seam is justified when something varies across it;
here nothing does, so a shared project would be a hypothetical seam plus a solution entry,
two project references and an extra build step, all for one file.

A survey of both apps found exactly one duplicated file. That is not yet a pattern.

## Consequences

- The fork can recur. Mitigated by this ADR and by the `CONTEXT.md` note.
- When the second shared service appears, creating the library moves two files instead of
  one — a trivially larger change, and by then the seam is real.
