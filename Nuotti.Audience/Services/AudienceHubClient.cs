using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.Audience.Services;

public class AudienceHubClient : IAsyncDisposable
{
    private readonly NavigationManager _nav;
    private readonly HttpClient _http;

    private HubConnection? _connection;

    public string? BackendBaseUrl { get; private set; }
    public string? SessionCode { get; private set; }
    public string? AudienceName { get; private set; }

    // Latest state
    public QuestionPushed? CurrentQuestion { get; private set; }

    public event Action<QuestionPushed>? QuestionPushed;
    public event Action<PlayTrack>? PlayTrack;
    public event Action<JoinedAudience>? JoinedAudience;
    public event Action<AnswerSubmitted>? AnswerSubmitted;

    public AudienceHubClient(NavigationManager nav, HttpClient http)
    {
        _nav = nav;
        _http = http;
        BackendBaseUrl = InferBackendBaseUrl();
    }

    private string InferBackendBaseUrl()
    {
        // Allow override via query string `backend` (e.g., ?backend=https%3A%2F%2Flocalhost%3A5192)
        var uri = new Uri(_nav.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
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
        if (_connection is { State: HubConnectionState.Connected }) return;
        if (_connection is null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(new Uri(BackendBaseUrl!), "/hub"))
                .WithAutomaticReconnect()
                .Build();

            _connection.On<QuestionPushed>("QuestionPushed", q =>
            {
                CurrentQuestion = q;
                QuestionPushed?.Invoke(q);
            });

            _connection.On<PlayTrack>("PlayTrack", p =>
            {
                PlayTrack?.Invoke(p);
            });

            _connection.On<JoinedAudience>("JoinedAudience", j =>
            {
                JoinedAudience?.Invoke(j);
            });

            _connection.On<AnswerSubmitted>("AnswerSubmitted", a =>
            {
                AnswerSubmitted?.Invoke(a);
            });
        }

        if (_connection.State == HubConnectionState.Disconnected)
        {
            await _connection.StartAsync();
        }
    }

    public async Task CreateOrJoinAsync(string sessionCode, string? audienceName = null)
    {
        await EnsureConnectedAsync();
        SessionCode = sessionCode;
        AudienceName = audienceName;

        if (!string.IsNullOrWhiteSpace(audienceName))
        {
            await _connection!.InvokeAsync("CreateOrJoinWithName", sessionCode, audienceName);
        }
        else
        {
            await _connection!.InvokeAsync("CreateOrJoin", sessionCode);
        }
    }

    public async Task SubmitAnswerAsync(int choiceIndex)
    {
        if (string.IsNullOrWhiteSpace(SessionCode)) return;
        await EnsureConnectedAsync();
        await _connection!.InvokeAsync("SubmitAnswer", SessionCode!, choiceIndex);
    }

    public async Task RequestPlayAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(SessionCode)) return;
        if (string.IsNullOrWhiteSpace(fileUrl)) return;
        await EnsureConnectedAsync();
        await _connection!.InvokeAsync("RequestPlay", SessionCode!, new PlayTrack(fileUrl));
    }

    public ValueTask DisposeAsync()
    {
        return _connection?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
