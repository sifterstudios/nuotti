using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using System.Diagnostics;
namespace Nuotti.Audience.Services;

public class AudienceHubClient : IAsyncDisposable
{
    readonly NavigationManager _nav;
    readonly HttpClient _http;
    readonly IConfiguration _configuration;

    HubConnection? _connection;
    HubConnection? _logConnection;

    public string? BackendBaseUrl { get; }
    public string? SessionCode { get; private set; }
    public string? AudienceName { get; private set; }

    public QuestionPushed? CurrentQuestion { get; private set; }
    public GameStateSnapshot? CurrentGameState { get; private set; }
    
    // Track participants in the session
    private readonly List<string> _participants = new();
    public IReadOnlyList<string> Participants => _participants.AsReadOnly();

    public event Action<QuestionPushed>? QuestionPushed;
    public event Action<PlayTrack>? PlayTrack;
    public event Action<JoinedAudience>? JoinedAudience;
    public event Action<AnswerSubmitted>? AnswerSubmitted;
    public event Action<NuottiProblem>? ProblemReceived;
    public event Action<GameStateSnapshot>? GameStateChanged;
    public event Action? ParticipantsChanged;
    public NuottiProblem? LastProblem { get; private set; }

    public AudienceHubClient(NavigationManager nav, HttpClient http, IConfiguration configuration)
    {
        _nav = nav;
        _http = http;
        _configuration = configuration;
        BackendBaseUrl = ResolveBackendBaseUrl();
    }

    string ResolveBackendBaseUrl()
    {
        // For Blazor WASM, check configuration for backend URL
        // This can be set via appsettings.json or environment-specific config
        var backendUrl = _configuration["services:backend:https:0"] 
                        ?? _configuration["services:backend:http:0"]
                        ?? _configuration["BackendUrl"];
        
        if (!string.IsNullOrWhiteSpace(backendUrl))
        {
            Log($"[Audience] Using backend URL from configuration: {backendUrl}");
            return backendUrl.TrimEnd('/');
        }
        
        // Fallback to same origin as the static site host
        // This works when WASM app and backend are behind the same reverse proxy
        var uri = new Uri(_nav.Uri);
        var fallbackUrl = $"{uri.Scheme}://{uri.Authority}";
        Log($"[Audience] Using fallback backend URL (same origin): {fallbackUrl}");
        return fallbackUrl;
    }
    
    void Log(string message)
    {
        Debug.WriteLine(message);
        _ = PublishLogAsync("Debug", "Audience", message);
    }

    public HubConnectionState GetConnectionState()
    {
        return _connection?.State ?? HubConnectionState.Disconnected;
    }

    public async Task EnsureConnectedAsync()
    {
        if (_connection is { State: HubConnectionState.Connected })
        {
            Log("[Audience] Already connected");
            return;
        }
        if (_connection is null)
        {
            Log($"[Audience] Creating HubConnection to {BackendBaseUrl}/hub");
            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(new Uri(BackendBaseUrl!), "/hub"))
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            // Set up reconnection event handlers
            _connection.Reconnecting += OnReconnecting;
            _connection.Reconnected += OnReconnected;
            _connection.Closed += OnConnectionClosed;

            _connection.On<QuestionPushed>("QuestionPushed", q =>
            {
                Log($"[Audience] QuestionPushed: {q.Text}");
                CurrentQuestion = q;
                QuestionPushed?.Invoke(q);
            });

            _connection.On<PlayTrack>("PlayTrack", p =>
            {
                Log($"[Audience] PlayTrack: {p.FileUrl}");
                PlayTrack?.Invoke(p);
            });

            _connection.On<JoinedAudience>("JoinedAudience", j =>
            {
                Log($"[Audience] JoinedAudience: {j.ConnectionId} {j.Name}");
                
                // Add participant to the list if not already present
                var name = string.IsNullOrWhiteSpace(j.Name) ? $"Guest {j.ConnectionId.Substring(0, 4)}" : j.Name;
                if (!_participants.Contains(name))
                {
                    _participants.Add(name);
                    ParticipantsChanged?.Invoke();
                }
                
                JoinedAudience?.Invoke(j);
            });

            _connection.On<AnswerSubmitted>("AnswerSubmitted", a =>
            {
                Log($"[Audience] AnswerSubmitted: choiceIndex={a.ChoiceIndex}");
                AnswerSubmitted?.Invoke(a);
            });

            _connection.On<GameStateSnapshot>("GameStateChanged", state =>
            {
                Log($"[Audience] GameStateChanged: phase={state.Phase}, songIndex={state.SongIndex}");
                CurrentGameState = state;
                GameStateChanged?.Invoke(state);
            });
        }

        if (_connection.State == HubConnectionState.Disconnected)
        {
            Log("[Audience] Starting HubConnection...");
            await _connection.StartAsync();
            Log("[Audience] HubConnection started");
        }
    }

    private Task OnReconnecting(Exception? exception)
    {
        Log($"[Audience] Connection lost, attempting to reconnect: {exception?.Message}");
        return Task.CompletedTask;
    }

    private async Task OnReconnected(string? connectionId)
    {
        Log($"[Audience] Reconnected with connection ID: {connectionId}");
        
        // Restore session state after reconnection
        if (!string.IsNullOrEmpty(SessionCode))
        {
            try
            {
                // Rejoin the session
                await _connection!.InvokeAsync("Join", SessionCode, "audience", AudienceName);
                
                // Fetch current game state
                await FetchGameStateAsync();
                
                Log($"[Audience] Session state restored for: {SessionCode}");
            }
            catch (Exception ex)
            {
                Log($"[Audience] Failed to restore session state: {ex.Message}");
            }
        }
    }

    private Task OnConnectionClosed(Exception? exception)
    {
        Log($"[Audience] Connection closed: {exception?.Message}");
        return Task.CompletedTask;
    }

    public async Task CreateOrJoinAsync(string sessionCode, string? audienceName = null)
    {
        await EnsureConnectedAsync();
        SessionCode = sessionCode;
        AudienceName = audienceName;
        
        // Add ourselves to the participants list
        var displayName = string.IsNullOrWhiteSpace(audienceName) ? "You" : audienceName;
        if (!_participants.Contains(displayName))
        {
            _participants.Add(displayName);
            ParticipantsChanged?.Invoke();
        }
        
        Log($"[Audience] Invoking Join: session={sessionCode} name={audienceName}");
        await _connection!.InvokeAsync("Join", sessionCode, "audience", audienceName);
    }

    public async Task SubmitAnswerAsync(int choiceIndex)
    {
        if (string.IsNullOrWhiteSpace(SessionCode))
        {
            Log("[Audience] SubmitAnswer skipped: no session");
            return;
        }
        await EnsureConnectedAsync();
        Log($"[Audience] Submitting answer: session={SessionCode} choiceIndex={choiceIndex}");
        await _connection!.InvokeAsync("SubmitAnswer", SessionCode!, choiceIndex);
    }

    public async Task RequestPlayAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(SessionCode))
        {
            Log("[Audience] RequestPlay skipped: no session");
            return;
        }
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            Log("[Audience] RequestPlay skipped: empty fileUrl");
            return;
        }
        await EnsureConnectedAsync();
        Log($"[Audience] RequestPlay: session={SessionCode} url={fileUrl}");
        await _connection!.InvokeAsync("RequestPlay", SessionCode!, new PlayTrack(fileUrl)
        {
            SessionCode = SessionCode!,
            IssuedByRole = Role.Audience,
            IssuedById = AudienceName ?? "anonymous"
        });
    }

    public async Task<GameStateSnapshot?> FetchGameStateAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionCode))
        {
            Log("[Audience] FetchGameState skipped: no session");
            return null;
        }

        try
        {
            Log($"[Audience] Fetching game state for session: {SessionCode}");
            var response = await _http.GetAsync($"{BackendBaseUrl}/status/{SessionCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var gameState = System.Text.Json.JsonSerializer.Deserialize<GameStateSnapshot>(json, 
                    new System.Text.Json.JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
                    });
                
                if (gameState != null)
                {
                    CurrentGameState = gameState;
                    GameStateChanged?.Invoke(gameState);
                }
                
                return gameState;
            }
        }
        catch (Exception ex)
        {
            Log($"[Audience] Failed to fetch game state: {ex.Message}");
        }

        return null;
    }

    async Task PublishLogAsync(string level, string source, string message)
    {
        try
        {
            await EnsureLogConnectedAsync();
            if (_logConnection is { State: HubConnectionState.Connected })
            {
                var e = new LogEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    Level: level,
                    Source: source,
                    Message: message,
                    ConnectionId: null,
                    Session: SessionCode,
                    Role: "audience"
                );
                await _logConnection.InvokeAsync("Publish", e);
            }
        }
        catch
        {
            // Ignore logging failures in UI
        }
    }

    async Task EnsureLogConnectedAsync()
    {
        if (_logConnection == null)
        {
            _logConnection = new HubConnectionBuilder()
                .WithUrl(new Uri(new Uri(BackendBaseUrl!), "/log"))
                .WithAutomaticReconnect()
                .Build();
        }
        if (_logConnection.State == HubConnectionState.Disconnected)
        {
            await _logConnection.StartAsync();
        }
    }

    public ValueTask DisposeAsync()
    {
        Log("[Audience] Disposing HubConnection");
        return _connection?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}