using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.AudioEngine;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Projector.Services;

/// <summary>
/// Venue Session connection: pair/token gate → projector hub → engine host → credentialled resync.
/// MainWindow binds outcomes to overlays; this module owns when to start, hold, or re-pair.
/// </summary>
public sealed class VenueConnectionLifecycle : IAsyncDisposable
{
    public const int DefaultMaxResyncAttempts = 5;

    readonly HubConnection _hub;
    readonly VenueDevicePairingClient _pairing;
    readonly ReconnectService _reconnect;
    readonly string _backendBaseUrl;
    readonly Func<string> _sessionCode;
    readonly Action<string> _setSessionCode;
    readonly int _maxResyncAttempts;
    readonly VenueConnectionCallbacks _ui;

    VenueEngineHost? _engineHost;
    int _resyncAttempts;

    public VenueConnectionLifecycle(
        HubConnection hub,
        VenueDevicePairingClient pairing,
        ReconnectService reconnect,
        string backendBaseUrl,
        Func<string> sessionCode,
        Action<string> setSessionCode,
        VenueConnectionCallbacks ui,
        int maxResyncAttempts = DefaultMaxResyncAttempts)
    {
        _hub = hub;
        _pairing = pairing;
        _reconnect = reconnect;
        _backendBaseUrl = backendBaseUrl;
        _sessionCode = sessionCode;
        _setSessionCode = setSessionCode;
        _ui = ui;
        _maxResyncAttempts = Math.Max(1, maxResyncAttempts);
    }

    public bool IsEngineStarted => _engineHost?.IsStarted == true;

    /// <summary>True when a pairing credential exists (or NUOTTI_PAIRINGCODE succeeds).</summary>
    public async Task<bool> EnsurePairedAsync(CancellationToken ct = default)
    {
        if (_pairing.Current is not null) return true;

        var preseeded = Environment.GetEnvironmentVariable("NUOTTI_PAIRINGCODE");
        if (!string.IsNullOrWhiteSpace(preseeded) && await TryPairAsync(preseeded.Trim(), ct)) return true;

        _ui.NeedPairing("Enter the eight-digit code from the Performer app to pair this Venue machine.");
        return false;
    }

    public async Task<bool> TryPairAsync(string code, CancellationToken ct = default)
    {
        var name = Environment.GetEnvironmentVariable("NUOTTI_DEVICENAME") ?? Environment.MachineName;
        var paired = await _pairing.PairAsync(code, name, ct);
        if (paired is null) return false;

        _setSessionCode(paired.SessionCode);
        _ui.SessionCodeChanged(paired.SessionCode);
        _ui.Log($"[pair] Paired to session={paired.SessionCode}");
        return true;
    }

    /// <summary>Pair → token gate → hub start → engine. Returns false when held for pairing or lease.</summary>
    public async Task<bool> StartAsync(CancellationToken ct = default)
    {
        if (!await EnsurePairedAsync(ct)) return false;
        if (!await EnsureLeaseOrHoldAsync(ct)) return false;

        await _hub.StartAsync(ct);
        _ui.Log("[hub] start ok");
        _ui.HubStarted(_hub.ConnectionId);
        _ui.Log($"[hub] joined as projector to session={_sessionCode()}");
        await StartEngineHostAsync(ct);
        _resyncAttempts = 0;
        return true;
    }

    /// <summary>
    /// Reconnect after hub Closed: same token gate as Start, then catch-up snapshot.
    /// Bounded retries — unbounded recursion used to hammer a dead lease forever.
    /// </summary>
    public async Task<bool> ResyncAsync(CancellationToken ct = default)
    {
        if (_pairing.Current is null)
        {
            _ui.NeedPairing("This Venue machine is no longer paired to a session. Enter a new code.");
            return false;
        }

        if (_resyncAttempts >= _maxResyncAttempts)
        {
            _ui.Log($"[hub] giving up resync after {_maxResyncAttempts} attempts");
            _ui.ResyncFailed($"Gave up after {_maxResyncAttempts} reconnect attempts.");
            return false;
        }

        _resyncAttempts++;
        try
        {
            _ui.ResyncProgress("Reconnecting...", "Restoring connection...");

            if (!await EnsureLeaseOrHoldAsync(ct))
                return false;

            await _hub.StartAsync(ct);
            _ui.Log("[hub] reconnect start ok");
            _ui.HubStarted(_hub.ConnectionId);
            _ui.Log($"[hub] rejoined as projector to session={_sessionCode()}");
            await StartEngineHostAsync(ct);

            _ui.ResyncProgress("Reconnecting...", "Syncing latest state...");
            var latest = await _reconnect.FetchLatestStateAsync(_sessionCode());
            if (latest is not null)
                _ui.Log("[hub] state resynced successfully");
            else
                _ui.Log("[hub] state resync failed, continuing with current state");

            _ui.ResyncSucceeded(latest);
            _resyncAttempts = 0;
            _ui.Log("[hub] reconnected successfully");
            return true;
        }
        catch (Exception ex)
        {
            _ui.Log($"[hub] reconnect error: {ex.Message}");
            if (_resyncAttempts >= _maxResyncAttempts)
            {
                _ui.ResyncFailed($"Gave up after {_maxResyncAttempts} reconnect attempts: {ex.Message}");
                return false;
            }

            _ui.ResyncProgress("Reconnection Failed", "Will retry automatically...");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return await ResyncAsync(ct);
        }
    }

    public async Task StopEngineAsync()
    {
        if (_engineHost is null) return;
        try { await _engineHost.DisposeAsync(); }
        catch (Exception ex) { _ui.Log($"[engine] stop failed: {ex.Message}"); }
        _engineHost = null;
    }

    async Task<bool> EnsureLeaseOrHoldAsync(CancellationToken ct)
    {
        // Do not open the hub until a lease is in hand. StartAsync with a null AccessTokenProvider
        // result is what produces "no credential this session recognises" on the backend.
        if (await _pairing.GetAccessTokenAsync(ct) is not null) return true;

        _ui.Log("[hub] skipped start: no access token yet");
        if (_pairing.Current is null)
            _ui.NeedPairing("This Venue machine is no longer paired to a session. Enter a new code.");
        else
            _ui.ConnectionStatus("Waiting for API — lease refresh failed");
        return false;
    }

    async Task StartEngineHostAsync(CancellationToken ct)
    {
        if (_engineHost?.IsStarted == true) return;
        if (_pairing.Current is null) return;

        await StopEngineAsync();
        try
        {
            _engineHost = new VenueEngineHost(
                _backendBaseUrl,
                _sessionCode(),
                async () => await _pairing.GetAccessTokenAsync());
            await _engineHost.StartAsync(ct);
            _ui.Log("[engine] Venue audio engine connected");
            _ui.EngineStarted(true);
        }
        catch (Exception ex)
        {
            _ui.Log($"[engine] failed to start: {ex.Message}");
            _ui.EngineStarted(false);
            try { if (_engineHost is not null) await _engineHost.DisposeAsync(); }
            catch { /* shutting down */ }
            _engineHost = null;
        }
    }

    public async ValueTask DisposeAsync() => await StopEngineAsync();
}

/// <summary>UI / presentation callbacks for <see cref="VenueConnectionLifecycle"/>.</summary>
public sealed class VenueConnectionCallbacks
{
    public required Action<string> Log { get; init; }
    public required Action<string> NeedPairing { get; init; }
    public required Action<string> ConnectionStatus { get; init; }
    public required Action<string> SessionCodeChanged { get; init; }
    public required Action<string?> HubStarted { get; init; }
    public required Action<bool> EngineStarted { get; init; }
    public required Action<string, string> ResyncProgress { get; init; }
    public required Action<GameStateSnapshot?> ResyncSucceeded { get; init; }
    public required Action<string> ResyncFailed { get; init; }
}
