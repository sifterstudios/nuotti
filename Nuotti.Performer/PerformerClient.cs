using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using System.Diagnostics;
namespace Nuotti.Performer;

public sealed class PerformerClient : IAsyncDisposable
{
    readonly Uri _backendBaseUri;
    readonly string _sessionCode;
    HubConnection? _hub;

    public Func<HttpMessageHandler, HttpMessageHandler>? HttpMessageHandlerDecorator { get; set; }

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public event Action<bool>? ConnectedChanged;
    public event Action<NuottiProblem>? ProblemReceived;
    public event Action<GameStateSnapshot>? GameStateChanged;
    public event Action<AnswerSubmitted>? AnswerSubmitted;

    public PerformerClient(Uri backendBaseUri, string sessionCode)
    {
        _backendBaseUri = backendBaseUri;
        _sessionCode = sessionCode;
    }

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (_hub is null)
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(new Uri(_backendBaseUri, "/hub"), options =>
                {
                    if (HttpMessageHandlerDecorator is not null)
                    {
                        options.HttpMessageHandlerFactory = inner => HttpMessageHandlerDecorator(inner ?? new HttpClientHandler());
                    }
                })
                .WithAutomaticReconnect()
                .Build();

            _hub.Reconnected += async _ =>
            {
                ConnectedChanged?.Invoke(IsConnected);
                try
                {
                    await _hub.InvokeAsync("Join", _sessionCode, "performer", null, null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PerformerClient] rejoin after reconnect failed: {ex.Message}");
                }
            };
            _hub.Reconnecting += _ => { ConnectedChanged?.Invoke(IsConnected); return Task.CompletedTask; };
            _hub.Closed += _ => { ConnectedChanged?.Invoke(IsConnected); return Task.CompletedTask; };

            _hub.On<NuottiProblem>("Problem", p =>
            {
                ProblemReceived?.Invoke(p);
            });
            _hub.On<GameStateSnapshot>("GameStateChanged", s =>
            {
                GameStateChanged?.Invoke(s);
            });
            // The Backend broadcasts no snapshot per answer, so without this the Performer's tallies
            // could not move during Guessing. Subscribers replay the event through GameReducer.
            _hub.On<AnswerSubmitted>("AnswerSubmitted", a =>
            {
                AnswerSubmitted?.Invoke(a);
            });
        }
        if (_hub.State == HubConnectionState.Disconnected)
        {
            await _hub.StartAsync(cancellationToken);
            ConnectedChanged?.Invoke(IsConnected);
            await _hub.InvokeAsync("Join", _sessionCode, "performer", null, null, cancellationToken);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_hub is { State: HubConnectionState.Connected })
        {
            await _hub.StopAsync(cancellationToken);
            ConnectedChanged?.Invoke(IsConnected);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            try { await _hub.DisposeAsync(); } catch { /* ignore */ }
        }
    }

    [Conditional("DEBUG")]
    void Log(string msg) => Debug.WriteLine($"[PerformerClient] {msg}");
}
