# .NET ASIO playback and hardware routing

Research for [#237](https://github.com/sifterstudios/nuotti/issues/237).

## Recommendation

Use stable NAudio 2.3.0 behind a Nuotti-owned audio-output adapter. The Engine should feed
one three-channel, 32-bit-float sample provider into one `NAudio.Asio.AsioOut` instance:

| Logical channel | ASIO output |
| --- | --- |
| Backing left | 0 |
| Backing right | 1 |
| Click/count-in | 2 |

Construct that provider from the stereo backing source and mono click source with
`MultiplexingSampleProvider`. Normalize or predecode both assets to the same sample rate
and a shared frame-zero timeline; represent any backing start offset as leading silence,
not as a second delayed player.

This gives the three outputs one provider read, one ASIO callback clock, and one driver
start. NAudio's `AsioOut.Play()` makes a single `driver.Start()` call, while its callback
reads one interleaved provider buffer and converts it into all selected driver output
buffers. `MultiplexingSampleProvider` is explicitly intended to route sources to different
sound-card outputs, requires a common sample rate, and reads every input on every callback
to keep them synchronized. ([ASIO guide](https://github.com/naudio/NAudio/blob/release/2.x/Docs/AsioPlayback.md),
[`AsioOut` source](https://github.com/naudio/NAudio/blob/release/2.x/NAudio.Asio/AsioOut.cs),
[`MultiplexingSampleProvider` source](https://github.com/naudio/NAudio/blob/release/2.x/NAudio.Core/Wave/SampleProviders/MultiplexingSampleProvider.cs))

NAudio 2.3.0 is the appropriate baseline: the project describes NAudio 2 as the stable
line while NAudio 3 remains prerelease, and 2.3.0 was released on 12 March 2026.
Keep NAudio behind Nuotti's adapter so a future stable NAudio 3 or another driver layer
does not leak through the Engine API. ([NAudio repository](https://github.com/naudio/NAudio),
[2.3.0 release](https://github.com/naudio/NAudio/releases/tag/v2.3.0))

## Playback position

The Engine's authoritative position should be a count of sample frames requested from the
single top-level provider—not wall-clock time and not SignalR command arrival. It should
periodically publish:

```text
(playbackEpoch, positionFrames, sampleRate, measuredAtMonotonic)
```

The Projector can extrapolate between updates and correct drift when a new update arrives.
The synchronization design must define whether `positionFrames` means frames submitted to
the driver or estimated at the DAC. If it promises the latter, calibrate using the
`PlaybackLatency` exposed by `AsioOut`. `AsioOut` also exposes `FramesPerBuffer`, which
allows the implementation to state the remaining callback-granularity uncertainty.
([`AsioOut` source](https://github.com/naudio/NAudio/blob/release/2.x/NAudio.Asio/AsioOut.cs))

For the MVP, the Performer command should reach the Show Agent, cause a local start, and
then publish the playback epoch and position. Do not schedule the audio start from a
remote timestamp.

## Preflight

The Show Agent should refuse live readiness until it has:

1. Enumerated installed drivers with `AsioOut.GetDriverNames()` and instantiated the
   selected driver.
2. Confirmed that the selected driver exposes at least three output channels.
3. Displayed channel names and allowed backing-left, backing-right, and click mappings to
   be verified.
4. Confirmed that the prepared sample rate is supported.
5. Initialized the stream and recorded `FramesPerBuffer` and `PlaybackLatency`.
6. Played test tones independently through each mapped output.
7. Played a short three-channel synchronization fixture.
8. Validated that all show assets use the prepared common format and timeline.

Driver names are suitable for user selection but should not be treated as durable hardware
identifiers; require revalidation when the available-driver or channel topology changes.
Expose the vendor control panel where useful. A driver-reset request, initialization
failure, missing device, failed fixture, or invalid assets must return the Engine to
not-ready. The official guide and implementation expose driver discovery, sample-rate
support, channel names/count, latency, buffer size, control-panel access, and reset
notifications. ([ASIO guide](https://github.com/naudio/NAudio/blob/release/2.x/Docs/AsioPlayback.md),
[`AsioOut` source](https://github.com/naudio/NAudio/blob/release/2.x/NAudio.Asio/AsioOut.cs))

## Risks and mitigations

- **Callback deadlines:** NAudio warns that very low ASIO buffers glitch if the signal
  chain cannot supply audio in time. Predecode show assets to PCM (or prove bounded decode
  time), preallocate buffers, and keep allocations, locks, disk/network I/O, and logging
  away from the callback. Test representative hardware under load.
- **Three-channel confidence:** `AsioOut`'s class comment says it is optimized for two
  output channels, although the implementation derives output count from the provider and
  allocates/converts every requested output. Gate adoption on a hardware spike covering
  representative interfaces and three-or-more-channel playback.
- **Physical routing:** `ChannelOffset` selects a contiguous starting range. For
  noncontiguous physical ports, emit enough logical output channels to reach the highest
  selected port and route or silence intermediate slots in the multiplexed provider.
- **End-of-stream behavior:** keep `AutoStop` disabled; its source comments warn it can
  hang. Detect provider completion and stop from the control thread.
- **Driver availability:** require the audio interface vendor's native ASIO driver for the
  supported production path. Do not promise ASIO4ALL compatibility.
- **Clock semantics:** specify submitted-versus-audible playback position and latency
  compensation in the synchronization protocol before Projector behavior is implemented.

## Alternatives considered

- **Direct Steinberg ASIO interop:** maximum control, but substantially more native
  interop, buffer conversion, lifecycle, and driver-compatibility work. Retain only as a
  fallback if the NAudio hardware spike uncovers blocking incompatibilities.
- **ManagedBass/BASSASIO:** viable native engine, but adds a separate native deployment
  and licensing surface without a clear benefit for this narrow player.
- **CSCore:** includes ASIO support, but offers a weaker maintenance signal than the
  actively released NAudio stable line.
- **NAudio 3 prerelease:** potentially useful later, but unsuitable as the MVP baseline
  until its API is stable. The adapter seam makes reevaluation inexpensive.

## Decision and validation gate

Proceed with a NAudio 2.3.0 prototype using one three-channel stream. Before treating the
choice as final, validate it on at least two representative Windows 11 ASIO interfaces,
including a nontrivial physical channel mapping, under realistic CPU load. Measure channel
alignment, callback underruns, restart/reset behavior, and the relationship between the
frame counter and audible output.
