using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.Performer;

public sealed class PerformerUiState
{
    readonly IHttpClientFactory _httpFactory;

    public PerformerUiState(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public event Action? Changed;

    public bool Connected { get; private set; }
    public string? SessionCode { get; private set; }
    public Uri? BackendBaseUri { get; private set; }

    // Game state bits
    public Phase Phase { get; private set; } = Phase.Idle;
    public int SongIndex { get; private set; }
    public int HintIndex { get; private set; }
    public int NextHintIndex => HintIndex + 1;
    public SongRef? CurrentSong { get; private set; }
    public IReadOnlyList<string> Choices { get; private set; } = Array.Empty<string>();
    public int? SelectedCorrectIndex { get; private set; }

    // Role counts
    public int ProjectorCount { get; private set; }
    public int EngineCount { get; private set; }
    public int AudienceCount { get; private set; }

    public void SetSession(string session, Uri backend)
    {
        SessionCode = session;
        BackendBaseUri = backend;
        Changed?.Invoke();
    }

    public void SetConnection(bool connected)
    {
        Connected = connected;
        Changed?.Invoke();
    }

    public void UpdateGameState(GameStateSnapshot snapshot)
    {
        var songChanged = snapshot.SongIndex != SongIndex || snapshot.CurrentSong?.Id != CurrentSong?.Id;
        Phase = snapshot.Phase;
        SongIndex = snapshot.SongIndex;
        HintIndex = snapshot.HintIndex;
        CurrentSong = snapshot.CurrentSong;
        Choices = snapshot.Choices;
        if (songChanged)
        {
            SelectedCorrectIndex = null;
        }
        // keep the session if not set
        if (!string.IsNullOrWhiteSpace(snapshot.SessionCode))
            SessionCode ??= snapshot.SessionCode;
        Changed?.Invoke();
    }

    public void IncrementHintIndex()
    {
        HintIndex++;
        Changed?.Invoke();
    }

    public async Task RefreshCountsAsync(CancellationToken ct = default)
    {
        if (BackendBaseUri is null || string.IsNullOrWhiteSpace(SessionCode)) return;
        try
        {
            var http = _httpFactory.CreateClient();
            http.BaseAddress = BackendBaseUri;
            var resp = await http.GetFromJsonAsync<RoleCountsDto>($"/api/sessions/{Uri.EscapeDataString(SessionCode!)}/counts", ct);
            if (resp is not null)
            {
                ProjectorCount = resp.projector;
                EngineCount = resp.engine;
                AudienceCount = resp.audiences;
                Changed?.Invoke();
            }
        }
        catch
        {
            // ignore network failures; UI will rely on Connected flag
        }
    }

    public void SetSelectedCorrectIndex(int? index)
    {
        SelectedCorrectIndex = index;
        Changed?.Invoke();
    }

    public sealed record RoleCountsDto(int performer, int projector, int engine, int audiences);
}
