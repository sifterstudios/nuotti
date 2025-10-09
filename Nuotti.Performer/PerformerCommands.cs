using MudBlazor;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Performer.Services;
using System.Net;
namespace Nuotti.Performer;

public sealed class PerformerCommands
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ISnackbar _snackbar;
    private readonly PerformerUiState _state;
    private readonly CommandHistoryService _history;

    public PerformerCommands(IHttpClientFactory httpFactory, ISnackbar snackbar, PerformerUiState state, CommandHistoryService history)
    {
        _httpFactory = httpFactory;
        _snackbar = snackbar;
        _state = state;
        _history = history;
    }

    HttpClient CreateClient()
    {
        var http = _httpFactory.CreateClient();
        if (_state.BackendBaseUri is null) throw new InvalidOperationException("Backend not set");
        http.BaseAddress = _state.BackendBaseUri;
        return http;
    }

    public async Task StartSetAsync(CancellationToken ct = default)
    {
        EnsureSession();
        var cmd = new StartGame
        {
            SessionCode = _state.SessionCode!,
            IssuedByRole = Role.Performer,
            IssuedById = "performer-ui"
        };
        await SendAsync("start-game", cmd, ct);
    }

    public async Task EndSongAsync(SongId songId, CancellationToken ct = default)
    {
        EnsureSession();
        var cmd = new EndSong(songId)
        {
            SessionCode = _state.SessionCode!,
            IssuedByRole = Role.Performer,
            IssuedById = "performer-ui"
        };
        await SendAsync("end-song", cmd, ct);
    }

    public async Task LockAnswersAsync(CancellationToken ct = default)
    {
        EnsureSession();
        var cmd = new LockAnswers
        {
            SessionCode = _state.SessionCode!,
            IssuedByRole = Role.Performer,
            IssuedById = "performer-ui"
        };
        await SendAsync("lock-answers", cmd, ct);
    }

    public async Task RevealAsync(SongRef songRef, int correctChoiceIndex, CancellationToken ct = default)
    {
        EnsureSession();
        var cmd = new RevealAnswer(songRef, correctChoiceIndex)
        {
            SessionCode = _state.SessionCode!,
            IssuedByRole = Role.Performer,
            IssuedById = "performer-ui"
        };
        await SendAsync("reveal-answer", cmd, ct);
    }

    public async Task NextSongAsync(SongId songId, CancellationToken ct = default)
    {
        EnsureSession();
        var cmd = new NextRound(songId)
        {
            SessionCode = _state.SessionCode!,
            IssuedByRole = Role.Performer,
            IssuedById = "performer-ui"
        };
        await SendAsync("next-round", cmd, ct);
    }

    public async Task PlaySongAsync(SongId songId, CancellationToken ct = default)
    {
        EnsureSession();
        var cmd = new PlaySong(songId)
        {
            SessionCode = _state.SessionCode!,
            IssuedByRole = Role.Performer,
            IssuedById = "performer-ui"
        };
        await SendAsync("play-song", cmd, ct);
    }

    public async Task GiveHintAsync(Hint hint, CancellationToken ct = default)
    {
        EnsureSession();
        var cmd = new GiveHint(hint)
        {
            SessionCode = _state.SessionCode!,
            IssuedByRole = Role.Performer,
            IssuedById = "performer-ui"
        };
        await SendAsync("give-hint", cmd, ct);
    }

    void EnsureSession()
    {
        if (string.IsNullOrWhiteSpace(_state.SessionCode)) throw new InvalidOperationException("Session not set");
    }

    async Task SendAsync(string route, CommandBase cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_state.SessionCode)) throw new InvalidOperationException("Session not set");
        var http = CreateClient();

        var url = $"/v1/message/phase/{route}/{Uri.EscapeDataString(_state.SessionCode!)}";
        var resp = await http.PostAsJsonAsync<CommandBase>(url, cmd, ContractsJson.RestOptions, ct);
        if (!resp.IsSuccessStatusCode)
        {
            // Try parse problem
            try
            {
                var prob = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions, ct);
                if (prob is not null)
                {
                    _history.RecordFailure(cmd, prob);
                    _snackbar.Add($"{prob.Title} ({prob.Reason})", Severity.Error);
                    return;
                }
            }
            catch { /* ignore parse errors */ }
            _history.RecordFailure(cmd, null);
            _snackbar.Add($"Command failed: {(int)resp.StatusCode}", Severity.Error);
        }
        else if (resp.StatusCode == HttpStatusCode.Accepted)
        {
            _history.RecordSuccess(cmd);
            _snackbar.Add("Accepted", Severity.Success);
        }
    }
}
