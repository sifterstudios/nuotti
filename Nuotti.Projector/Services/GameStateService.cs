using System;
using System.Collections.Generic;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;

namespace Nuotti.Projector.Services;

/// <summary>
/// Holds the Projector's view of the session. The snapshot from the Backend is the state — there is
/// no local mirror of it, and no local copy of the reducer's rules.
/// </summary>
public class GameStateService
{
    GameStateSnapshot _currentState = GameReducer.Initial(string.Empty);
    string _lastSnapshotHash = string.Empty;
    ContentSafetyService? _contentSafetyService;

    public event Action<GameStateSnapshot>? StateChanged;

    public void SetContentSafetyService(ContentSafetyService contentSafetyService)
    {
        _contentSafetyService = contentSafetyService;
    }

    public GameStateSnapshot CurrentState => _currentState;

    public void UpdateFromSnapshot(GameStateSnapshot snapshot)
    {
        // Skip duplicate broadcasts of identical state.
        var snapshotHash = CreateSnapshotHash(snapshot);
        if (snapshotHash == _lastSnapshotHash) return;

        _currentState = ApplyContentSafety(snapshot);
        _lastSnapshotHash = snapshotHash;
        StateChanged?.Invoke(_currentState);
    }

    /// <summary>
    /// Applies an event from the Backend using the same reducer the Backend used. The Backend does
    /// not push a snapshot per answer — that would be quadratic in audience size — so live tallies
    /// come from replaying the event locally, not from a hand-written increment.
    /// </summary>
    public void Apply(AnswerSubmitted answer)
    {
        var (next, error) = GameReducer.Reduce(_currentState, answer);
        if (error is not null) return;
        if (ReferenceEquals(next, _currentState)) return;

        _currentState = next;
        _lastSnapshotHash = CreateSnapshotHash(next);
        StateChanged?.Invoke(_currentState);
    }

    public bool ShouldShowPhase(Phase phase)
    {
        return phase switch
        {
            Phase.Idle => false, // Don't show idle state
            _ => true
        };
    }

    public string GetPhaseDisplayName(Phase phase)
    {
        return phase switch
        {
            Phase.Lobby => "Waiting for players...",
            Phase.Start => "Get ready!",
            Phase.Hint => "Hint",
            Phase.Guessing => "Submit your answers!",
            Phase.Lock => "Time's up!",
            Phase.Reveal => "The answer is...",
            Phase.Play => "Now playing",
            Phase.Intermission => "Scoreboard",
            Phase.Finished => "Game Over!",
            _ => phase.ToString()
        };
    }

    // Unit separator (0x1F, a C0 control code): cannot occur in a choice string, so joining with
    // it can't collide the way joining with a comma could (e.g. ["a,b"] vs ["a", "b"]).
    const char ChoiceSeparator = (char)0x1F;

    static string CreateSnapshotHash(GameStateSnapshot snapshot)
    {
        // Choice contents matter, not just their count: a performer can correct a question's
        // options before opening answers, keeping the same phase/song/hint/tally shape.
        var hashInput = $"{snapshot.Phase}|{snapshot.SongIndex}|{snapshot.HintIndex}|{snapshot.Tallies.Count}|{string.Join(",", snapshot.Tallies)}|{snapshot.Choices.Count}|{string.Join(ChoiceSeparator, snapshot.Choices)}";
        return hashInput.GetHashCode().ToString();
    }

    // F18 - Content safety checks
    GameStateSnapshot ApplyContentSafety(GameStateSnapshot state)
    {
        if (_contentSafetyService == null)
            return state;

        var sessionResult = _contentSafetyService.SanitizeText(state.SessionCode, ContentType.General);
        if (sessionResult.WasModified)
        {
            Console.WriteLine($"[content-safety] Session code sanitized: {sessionResult.Warnings}");
        }

        var safeSong = state.CurrentSong;
        if (state.CurrentSong != null)
        {
            var titleResult = _contentSafetyService.SanitizeSongTitle(state.CurrentSong.Title);
            var artistResult = _contentSafetyService.SanitizeArtistName(state.CurrentSong.Artist);

            if (titleResult.WasModified || artistResult.WasModified)
            {
                Console.WriteLine($"[content-safety] Song info sanitized - Title: {titleResult.Warnings}, Artist: {artistResult.Warnings}");
            }

            safeSong = state.CurrentSong with
            {
                Title = titleResult.SafeContent,
                Artist = artistResult.SafeContent
            };
        }

        var safeChoices = new List<string>();
        for (var i = 0; i < state.Choices.Count; i++)
        {
            var choiceResult = _contentSafetyService.SanitizeChoice(state.Choices[i], i);
            if (choiceResult.WasModified)
            {
                Console.WriteLine($"[content-safety] Choice {i + 1} sanitized: {choiceResult.Warnings}");
            }
            safeChoices.Add(choiceResult.SafeContent);
        }

        var safeScores = new Dictionary<string, int>();
        foreach (var kvp in state.Scores)
        {
            var playerResult = _contentSafetyService.SanitizePlayerName(kvp.Key);
            if (playerResult.WasModified)
            {
                Console.WriteLine($"[content-safety] Player name sanitized: {playerResult.Warnings}");
            }
            safeScores[playerResult.SafeContent] = kvp.Value;
        }

        return state with
        {
            SessionCode = sessionResult.SafeContent,
            CurrentSong = safeSong,
            Choices = safeChoices,
            Scores = safeScores
        };
    }
}
