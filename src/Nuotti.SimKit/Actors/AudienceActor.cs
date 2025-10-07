using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.Actors;

public sealed class AudienceActor : BaseActor
{
    readonly string _name;
    readonly AudienceOptions _options;
    readonly Random _random;
    int _lastAnsweredSongIndex = -1;
    int? _scheduledForSongIndex;
    readonly object _gate = new();

    public AudienceActor(IHubClientFactory hubClientFactory, Uri baseUri, string session, string name, AudienceOptions? options = null)
        : base(hubClientFactory, baseUri, session)
    {
        _name = name;
        _options = options ?? new AudienceOptions();
        _random = _options.RandomSeed.HasValue ? new Random(_options.RandomSeed.Value) : Random.Shared;
    }

    protected override string Role => "audience";
    protected override string? DisplayName => _name;

    /// <summary>
    /// React to a game state snapshot. If in Guessing phase, schedule exactly one randomized SubmitAnswer for this round.
    /// This method is idempotent per song index: multiple calls for the same round will not produce more than one answer.
    /// </summary>
    public async Task OnStateAsync(Nuotti.Contracts.V1.Model.GameStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is null) return;
        if (snapshot.Phase != Nuotti.Contracts.V1.Enum.Phase.Guessing) return;
        if (snapshot.Choices is null || snapshot.Choices.Count == 0) return;
        if (Client is null) return; // not started yet

        // ensure only one scheduled/answered per song index
        int songIndex = snapshot.SongIndex;
        lock (_gate)
        {
            if (_lastAnsweredSongIndex == songIndex) return; // already answered
            if (_scheduledForSongIndex == songIndex) return; // already scheduled
            _scheduledForSongIndex = songIndex;
        }

        // Apply drop rate: skip answering entirely for this round with given probability
        if (_random.NextDouble() < _options.DropRate)
        {
            lock (_gate)
            {
                _lastAnsweredSongIndex = songIndex; // mark as completed even if dropped
                _scheduledForSongIndex = null;
            }
            return;
        }

        // Random delay within [MinDelay, MaxDelay]
        var min = _options.MinDelay < TimeSpan.Zero ? TimeSpan.Zero : _options.MinDelay;
        var max = _options.MaxDelay < min ? min : _options.MaxDelay;
        var delayRangeMs = (max - min).TotalMilliseconds;
        var offsetMs = delayRangeMs <= 0 ? 0 : _random.NextDouble() * delayRangeMs;
        var delay = min + TimeSpan.FromMilliseconds(offsetMs);

        try
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // canceled; clear schedule marker
            lock (_gate) { if (_scheduledForSongIndex == songIndex) _scheduledForSongIndex = null; }
            throw;
        }
        if (cancellationToken.IsCancellationRequested)
        {
            lock (_gate) { if (_scheduledForSongIndex == songIndex) _scheduledForSongIndex = null; }
            return;
        }

        // Choose answer index: we don't know the correct index here; pick uniformly among choices
        var choiceCount = snapshot.Choices.Count;
        var choiceIndex = _random.Next(0, choiceCount);

        await Client.SubmitAnswerAsync(SessionCode, choiceIndex, cancellationToken);

        lock (_gate)
        {
            _lastAnsweredSongIndex = songIndex;
            _scheduledForSongIndex = null;
        }
    }
}