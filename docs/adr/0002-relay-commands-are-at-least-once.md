# ADR 0002 — Relay commands stay at-least-once

Date: 2026-07-27
Status: Accepted

## Context

`SessionCommandProcessor` applies every `CommandBase` command, and idempotency via
`IIdempotencyStore` is one of the stages behind its interface. Applying that stage
uniformly to all commands is free.

The relay commands — `PlayTrack`, `StopTrack`, `QuestionPushed` — carry a `CommandId` but
have never been checked against the idempotency store. They change no game state; they are
forwarded to a SignalR group.

## Decision

Relay commands skip the idempotency stage. They remain at-least-once. State-changing
commands remain at-most-once via `CommandId`.

## Rationale

A duplicate-suppressed relay command is silently swallowed. The realistic case is a client
re-sending `PlayTrack` with the same `CommandId` after a dropped connection: with
idempotency applied it returns `202 Accepted` and nothing plays, and the failure is
invisible from the caller's side.

Playback is the worst place in this system for a swallowed retry — a Performer pressing
play and hearing silence has no recourse and no error to act on. Re-broadcasting a play
command that already arrived is harmless by comparison: the Engine restarts the same track.

For state-changing commands the trade-off inverts, which is why idempotency stays there:
applying `NextRound` twice advances the game twice.

## Consequences

- The command switch inside the processor carries an explicit per-command flag for whether
  the idempotency stage runs, rather than applying it unconditionally.
- Duplicate relay commands reach clients more than once. Clients must tolerate that;
  `PlayTrack` and `StopTrack` are already idempotent in effect at the Engine.
- A future architecture review will likely suggest "apply idempotency uniformly". This ADR
  is the answer.

## Amendment — 2026-07-29

`QuestionPushed` now also produces a `QuestionOffered` event, so it does change game state; the
sentence above no longer describes it. The decision is unchanged: `QuestionPushed` still skips
the idempotency stage, so a client re-sending it after a dropped connection reaches the reducer
twice for the same question. `GameReducer`'s `QuestionOffered` case is what keeps that harmless:
it compares the incoming choices against the ones already on the snapshot and is a no-op when
they match, only replacing `Choices` and re-zeroing `Tallies` when they genuinely differ. A
duplicate relay is harmless because the reducer treats a repeat of the same question as a no-op,
not because the event was assumed to be idempotent by construction. `PlayTrack` and `StopTrack`
are untouched and remain pure relays.
