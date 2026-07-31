# Nuotti Projector Sync Lab

Disposable browser experiment for issue #245. It demonstrates that sparse planned/measured
Playback anchors can drive frame-by-frame Projector visuals and line-timed LRC locally.

From the repository root:

```powershell
dotnet run --project tools/Nuotti.ProjectorSyncLab --urls http://127.0.0.1:8090
```

Open `http://127.0.0.1:8090`, run the 750 ms planned start, then inject a drift size. Start a
fresh run before evaluating another size so each 40/100/200 ms case begins from zero bias.
The expected modes are ignore at 40 ms, gradual convergence at 100 ms, and snap at 200 ms.
The .NET process acts as the disposable Engine anchor source. It owns an independent monotonic
clock, emits planned/measured anchors over server-sent events about twice per second, and correlates
them to UTC. The browser displays target-to-measured start error, tap-to-start, rolling steady-state
p95 error, correction recovery, and whether the 750 ms lead satisfies both start targets.

The source's measured start is a process scheduling boundary, not a second ASIO proof. Combine its
browser/transport measurements with the physical ASIO evidence from issue #244; the lab must not
label its scheduling result as target-to-ASIO timing.

This is deliberately not a production transport or Projector UI. The production-facing pure
rules are in `Nuotti.Projector.Presentation/Playback` and tested in `Nuotti.Projector.Tests`. The
small JavaScript mirror is non-authoritative and exists only to exercise browser scheduling.
