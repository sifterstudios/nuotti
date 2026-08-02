using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.AudioEngine;

/// <summary>
/// Transport the Venue engine uses to join the hub as Engine. Production uses SignalR;
/// tests substitute a fake so the host can be proven without a live backend.
/// </summary>
public interface IVenueEngineTransport : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    void OnPlayTrack(Func<PlayTrack, Task> handler);
    void OnTrackPlayRequested(Func<string, Task> handler);
    void OnTrackStopped(Func<Task> handler);
    Task ReportStatusAsync(EngineStatusChanged status, CancellationToken cancellationToken = default);
}

/// <summary>SignalR transport: one hub connection as Engine, sharing the Venue access-token provider.</summary>
public sealed class SignalRVenueEngineTransport : IVenueEngineTransport
{
    readonly HubConnection _connection;
    readonly string _sessionCode;

    public SignalRVenueEngineTransport(
        string backendBaseUrl,
        string sessionCode,
        Func<Task<string?>> accessTokenProvider)
    {
        _sessionCode = sessionCode;
        var hubUrl = VenueEngineHost.BuildHubUrl(backendBaseUrl);
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options => options.AccessTokenProvider = accessTokenProvider)
            .WithAutomaticReconnect()
            .Build();
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
        => _connection.StartAsync(cancellationToken);

    public void OnPlayTrack(Func<PlayTrack, Task> handler)
        => _connection.On<PlayTrack>("PlayTrack", cmd => handler(cmd));

    public void OnTrackPlayRequested(Func<string, Task> handler)
        => _connection.On<string>("TrackPlayRequested", url => handler(url));

    public void OnTrackStopped(Func<Task> handler)
        => _connection.On("TrackStopped", () => handler());

    public Task ReportStatusAsync(EngineStatusChanged status, CancellationToken cancellationToken = default)
        => _connection.InvokeAsync("EngineStatusChanged", _sessionCode, status, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try { await _connection.DisposeAsync(); }
        catch { /* shutting down */ }
    }
}
