# Nuotti ASIO HIL harness

Disposable Stage 0 tool for ticket #244. It emits one three-channel ASIO stream:

- outputs 1–2: opposed-polarity backing markers after the configured offset;
- output 3: click marker from timeline frame zero;
- all markers derive from one frame counter.

List drivers:

```powershell
dotnet run --project tools/Nuotti.AsioHil -- --list
```

Run one hardware configuration:

```powershell
dotnet run --project tools/Nuotti.AsioHil -- --driver "DRIVER NAME" --confirm-output --expected-buffer 256 --report evidence/interface-a-256.json
```

For a two-output interface, run the same shared-timeline proof with mono backing on output 1 and click on output 2:

```powershell
dotnet run --project tools/Nuotti.AsioHil -- --driver "Focusrite USB ASIO" --mode mono --confirm-output --expected-buffer 256 --report focusrite-mono-256.json
```

The report records this explicitly as `MonoBackingAndClick`; it is useful HIL evidence but does not claim the eventual stereo-backing-plus-click routing has been proven.

The tool refuses to energize outputs without the explicit `--confirm-output` switch. Markers use a conservative -18 dBFS amplitude, but verify physical routing and monitor levels before running.

Configure the ASIO buffer in the vendor control panel first. Loop physical outputs 1–3 into a multichannel recorder. Repeat for buffer sizes 64, 128, 256, and 512 on at least two interfaces. The JSON report records driver, OS, sample rate, actual buffer, planned start, first callback, stop time, and the still-required physical-capture status. A completed emission exits with code 3 (`physical-capture-required`); generating JSON alone is deliberately not a pass.
