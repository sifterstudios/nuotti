using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using System.Diagnostics;
using System.Web;
namespace Nuotti.Audience.Services;

public class AudienceHubClient : IAsyncDisposable
{
    readonly NavigationManager _nav;
    readonly HttpClient _http;

    HubConnection? _connection;
    HubConnection? _logConnection;

    public string? BackendBaseUrl { get; }
    public string? SessionCode { get; private set; }
    public string? AudienceName { get; private set; }

    public QuestionPushed? CurrentQuestion { get; private set; }

    public event Action<QuestionPushed>? QuestionPushed;
    public event Action<PlayTrack>? PlayTrack;
    public event Action<JoinedAudience>? JoinedAudience;
    public event Action<AnswerSubmitted>? AnswerSubmitted;
    public event Action<NuottiProblem>? ProblemReceived;
    public NuottiProblem? LastProblem { get; private set; }

    public AudienceHubClient(NavigationManager nav, HttpClient http)
    {
        _nav = nav;
        _http = http;
        BackendBaseUrl = InferBackendBaseUrl();
    }

    string InferBackendBaseUrl()
    {
        // Allow override via query string `backend` (e.g., ?backend=https%3A%2F%2Flocalhost%3A5192)
        var uri = new Uri(_nav.Uri);
        var query = HttpUtility.ParseQueryString(uri.Query);
        var fromQuery = query["backend"];
        if (!string.IsNullOrWhiteSpace(fromQuery))
        {
            return fromQuery!;
        }
        // Default to same origin as the static site host
        return $"{uri.Scheme}://{uri.Authority}";
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
                .WithAutomaticReconnect()
                .Build();

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
                JoinedAudience?.Invoke(j);
            });

            _connection.On<AnswerSubmitted>("AnswerSubmitted", a =>
            {
                Log($"[Audience] AnswerSubmitted: choiceIndex={a.ChoiceIndex}");
                AnswerSubmitted?.Invoke(a);
            });
        }

        if (_connection.State == HubConnectionState.Disconnected)
        {
            Log("[Audience] Starting HubConnection...");
            await _connection.StartAsync();
            Log("[Audience] HubConnection started");
        }
    }

    public async Task CreateOrJoinAsync(string sessionCode, string? audienceName = null)
    {
        await EnsureConnectedAsync();
        SessionCode = sessionCode;
        AudienceName = audienceName;
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

    void Log(string message)
    {
        Debug.WriteLine(message);
        _ = PublishLogAsync("Debug", "Audience", message);
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