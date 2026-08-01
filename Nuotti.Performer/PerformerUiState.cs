using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;
namespace Nuotti.Performer;

public sealed class PerformerUiState
{
    readonly IHttpClientFactory _httpFactory;

    /// <summary>
    /// The session state, held once. The game-state properties below read through to this rather
    /// than keeping their own copies, so a Contracts change needs no hand-mapping here.
    /// </summary>
    GameStateSnapshot _snapshot = GameReducer.Initial(string.Empty);

    public PerformerUiState(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public event Action? Changed;

    public bool Connected { get; private set; }
    public string? SessionCode { get; private set; }
    public Uri? BackendBaseUri { get; private set; }

    /// <summary>True while recovery is in flight; Performer controls must wait.</summary>
    public bool IsReconciling { get; private set; }

    /// <summary>Plain-language impact after the last reconciliation.</summary>
    public string? RecoveryImpact { get; private set; }

    /// <summary>Recommended next action after recovery.</summary>
    public string? RecoveryAction { get; private set; }

    /// <summary>Controls may fire only when connected and not reconciling.</summary>
    public bool ControlsReady => Connected && !IsReconciling;

    /// <summary>The snapshot itself, for callers that want to pass it on whole.</summary>
    public GameStateSnapshot Snapshot => _snapshot;

    // Game state, read through to the snapshot
    public Phase Phase => _snapshot.Phase;
    public int SongIndex => _snapshot.SongIndex;
    public int HintIndex => _snapshot.HintIndex;
    public int NextHintIndex => _snapshot.CurrentHintNumber();
    public SongRef? CurrentSong => _snapshot.CurrentSong;
    public IReadOnlyList<string> Choices => _snapshot.Choices;
    public IReadOnlyList<int> Tallies => _snapshot.Tallies;
    public IReadOnlyDictionary<string, int> Scores => _snapshot.Scores;

    // Performer-local state, not part of the session snapshot
    public int? SelectedCorrectIndex { get; private set; }
    public IReadOnlyDictionary<string, int> BaselineScores { get; private set; } = new Dictionary<string, int>();
    /// <summary>Backing/click AssetRevisionId from the Session Setlist Snapshot for Start.</summary>
    public string? VenueAssetRevisionId { get; private set; }

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
        if (!connected)
        {
            IsReconciling = true;
            RecoveryImpact = "Connection lost.";
            RecoveryAction = "Wait for reconciliation before sending commands.";
        }
        Changed?.Invoke();
    }

    public void BeginReconciliation()
    {
        IsReconciling = true;
        RecoveryImpact = "Reconnecting…";
        RecoveryAction = "Controls are paused until the Session catches up.";
        Changed?.Invoke();
    }

    public void CompleteReconciliation(Nuotti.Contracts.V1.Recovery.SessionReconcileResult result)
    {
        UpdateGameState(result.Snapshot);
        IsReconciling = false;
        RecoveryImpact = result.ImpactSummary;
        RecoveryAction = result.RecommendedAction;
        Changed?.Invoke();
    }

    public void UpdateGameState(GameStateSnapshot snapshot)
    {
        var songChanged = snapshot.SongIndex != _snapshot.SongIndex
                          || snapshot.CurrentSong?.Id != _snapshot.CurrentSong?.Id;

        // When the song changes, the previous cumulative scores become the baseline for deltas.
        if (songChanged)
        {
            BaselineScores = _snapshot.Scores;
            SelectedCorrectIndex = null;
        }

        _snapshot = snapshot;

        // keep the session if not set
        if (!string.IsNullOrWhiteSpace(snapshot.SessionCode))
            SessionCode ??= snapshot.SessionCode;
        Changed?.Invoke();
    }

    /// <summary>
    /// Applies an event from the Backend with the same reducer the Backend used. The Backend does not
    /// push a snapshot per answer, so this is how the Performer's tallies stay live during Guessing —
    /// before this, PerformerClient never subscribed to AnswerSubmitted at all and the tallies could
    /// not move until the next phase change.
    /// </summary>
    public void Apply(AnswerSubmitted answer)
    {
        var (next, error) = GameReducer.Reduce(_snapshot, answer);
        if (error is not null || ReferenceEquals(next, _snapshot)) return;

        _snapshot = next;
        Changed?.Invoke();
    }

    /// <summary>
    /// Optimistically advances the hint locally so the UI responds immediately; the Backend's
    /// GameStateChanged broadcast is authoritative and will overwrite it.
    /// </summary>
    public void IncrementHintIndex()
    {
        _snapshot = _snapshot with { HintIndex = _snapshot.HintIndex + 1 };
        Changed?.Invoke();
    }

    public HttpClient CreateClient()
    {
        if (BackendBaseUri is null) throw new InvalidOperationException("Backend not set");
        var http = _httpFactory.CreateClient();
        http.BaseAddress = BackendBaseUri;
        return http;
    }

    public async Task RefreshCountsAsync(CancellationToken ct = default)
    {
        if (BackendBaseUri is null || string.IsNullOrWhiteSpace(SessionCode)) return;
        try
        {
            var http = CreateClient();
            var resp = await http.GetFromJsonAsync<RoleCountsDto>($"/api/sessions/{Uri.EscapeDataString(SessionCode!)}/counts", ct);
            if (resp is not null)
            {
                ProjectorCount = resp.projector;
                EngineCount = resp.engine;
                AudienceCount = resp.audiences;
                Connected = true;
                Changed?.Invoke();
            }
        }
        catch
        {
            Connected = false;
            Changed?.Invoke();
        }
    }

    public void SetSelectedCorrectIndex(int? index)
    {
        SelectedCorrectIndex = index;
        Changed?.Invoke();
    }

    public void SetVenueAssetRevisionId(string? assetRevisionId)
    {
        VenueAssetRevisionId = string.IsNullOrWhiteSpace(assetRevisionId) ? null : assetRevisionId.Trim();
        Changed?.Invoke();
    }

    public IEnumerable<(string id, int points, int delta)> GetOrderedScoreboard(int topN = 10)
    {
        // Sort by points desc, then by id ascending for deterministic ties
        var ordered = Scores
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp =>
            {
                var prev = BaselineScores.TryGetValue(kvp.Key, out var p) ? p : 0;
                return (kvp.Key, kvp.Value, kvp.Value - prev);
            })
            .Take(topN);
        return ordered;
    }

    public sealed record RoleCountsDto(int performer, int projector, int engine, int audiences);
}
