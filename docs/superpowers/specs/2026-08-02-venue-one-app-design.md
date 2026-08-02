# Nuotti Venue — one app for projector + engine

**Date:** 2026-08-02  
**Status:** Approved for implementation planning  
**Related:** Performer pairing panel (`076b2e4`); packaging notes in `docs/production-packaging.md`; playback coordinator remains #258 (out of scope)

## Problem

Venue pairing today is a two-app, two-code story: Projector has a pairing UI; Audio Engine / Show Agent only accepts `--pair-code` / `NUOTTI_PAIR_CODE`. A band at an event should not run CLI tools or type two codes.

## Goals

- **One Windows/macOS app** the band launches for the show.
- **One pairing code**, entered **once** in a UI, admits the machine to the Performer session.
- After pairing, the same process runs **projector display** and **audio engine**.
- Linux Venue package and split-machine (projector on one box, engine on another) stay **backlog**.

## Non-goals (v1)

- Stage-grade ASIO coordinator and shared-timeline work (#258).
- Linux packaging.
- Keeping a supported band-facing path for separate Projector + Engine binaries.
- A new backend “dual-role lease” credential type.

## Decision summary

| Decision | Choice |
|---|---|
| App shape | Evolve `Nuotti.Projector` into the Venue shell (Approach 1) |
| Platforms | Windows + macOS |
| Split machines | Not in v1 |
| Pairing | One show-agent code → one stored credential |
| Hub roles | Two connections, same credential: `deviceRole=projector` and `deviceRole=engine` |
| Audio | Existing PortAudio / `SystemPlayer` paths inside an in-process Engine host |

## Product shape

- User-facing name: **Nuotti Venue** (window title / pairing copy). Assembly may remain `Nuotti.Projector` in v1 to avoid rename churn; a later rename is optional.
- Band path: install Venue → open app → enter code from Performer → show runs.
- Performer “Pair venue devices” copy points at the Venue app only (no CLI instructions for bands).

## Pairing flow

1. Performer (workspace + connected session) generates an eight-digit code (existing endpoint).
2. If Venue has no stored credential (or refresh fails as revoked), show the pairing overlay.
3. Operator enters the code once.
4. Venue redeems via `POST /v1/show-agent/pair`, then `POST /v1/show-agent/token`, and persists `VenueDeviceCredential` (agent id, credential, workspace, session).
5. On success:
   - Start/refresh hub connection as **Projector** (`/hub?deviceRole=projector`) with the shared access-token provider.
   - Start an in-process **Engine host** that opens a second hub connection as **Engine** (`deviceRole=engine`) using the **same** token provider.
6. Performer revoke deletes live agents for the session; Venue treats failed token refresh / revoke signal as unpaired and returns both roles to the pairing screen.

Credential store path may stay under `Nuotti.Projector` for v1 or move to `Nuotti.Venue`; either is acceptable if documented.

## Runtime architecture

```
┌──────────────────────────────────────────┐
│  Nuotti Venue (Avalonia process)         │
│                                          │
│  Pairing overlay (unpaired only)         │
│                                          │
│  ┌────────────────┐  ┌─────────────────┐ │
│  │ Display UI     │  │ Engine host     │ │
│  │ hub: Projector │  │ hub: Engine     │ │
│  └───────┬────────┘  └────────┬────────┘ │
│          │  shared lease/token │          │
└──────────┴─────────────────────┴──────────┘
                     │
              api.<domain> /hub
```

### Engine host extraction

- Carve a library-friendly host out of `Nuotti.AudioEngine` (connect hub, status, playback coordinator / existing sinks) that Venue can start/stop without going through `Program.Main`.
- Keep `Nuotti.AudioEngine` executable as a **dev/debug** entry point if useful; it is not the band-facing packaging path for v1.

### Why one credential / two connections is enough

`ConnectionPrincipalResolver` already maps a show-agent lease to Projector vs Engine from the `deviceRole` query parameter. Venue does not need a new lease type in v1.

## Packaging

- Publish self-contained Venue for `win-x64` and `osx-arm64` / `osx-x64` as appropriate.
- Cloud compose remains unchanged (Venue is local-only).
- Update `docs/production-packaging.md` and Performer pairing help text to describe Venue, not dual CLI pairing.

## Testing seams

1. **Engine host** — can start with a fake hub / token provider without Avalonia.
2. **Venue pairing orchestration** — after successful pair, both projector and engine connection factories are asked for tokens (unit/integration with stubs).
3. **Existing** show-agent journey + Projector pairing client tests remain green.
4. Manual: Generate code in Performer → enter in Venue on Win or Mac → ProjectorCount and EngineCount both go non-zero.

## Risks

- **Process crash coupling:** audio fault can take down display; accepted for v1 one-app simplicity (isolation is a later option).
- **Mac audio quality:** PortAudio/`SystemPlayer` paths must be verified on macOS; stage ASIO remains Windows/#258.
- **Two hub connections:** counts and capability checks must treat both as the same machine; no product requirement to show two “devices” in Performer beyond projector/engine chips.

## Success criteria

- [ ] Band runs one Venue binary on Windows or macOS.
- [ ] One Performer-generated code pairs the machine once via UI.
- [ ] After pair, hub shows both projector and engine presence for the session.
- [ ] Revoke returns Venue to the pairing screen.
- [ ] Band-facing docs/UI no longer instruct `--pair-code` / `NUOTTI_PAIR_CODE`.
