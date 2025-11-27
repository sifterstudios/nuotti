using System;
using System.Collections.Generic;
using System.Linq;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Services;

public class GameStateService
{
    private GameState _currentState = new();
    private string _lastSnapshotHash = string.Empty;
    private ContentSafetyService? _contentSafetyService;
    
    public event Action<GameState>? StateChanged;
    
    public void SetContentSafetyService(ContentSafetyService contentSafetyService)
    {
        _contentSafetyService = contentSafetyService;
    }
    
    public GameState CurrentState => _currentState;
    
    public void UpdateFromSnapshot(GameStateSnapshot snapshot)
    {
        // Create a hash of the snapshot to detect duplicates
        var snapshotHash = CreateSnapshotHash(snapshot);
        
        // Skip if this is a duplicate event
        if (snapshotHash == _lastSnapshotHash)
        {
            return;
        }
        
        var newState = new GameState
        {
            Phase = snapshot.Phase,
            SessionCode = snapshot.SessionCode,
            SongIndex = snapshot.SongIndex,
            CurrentSong = snapshot.CurrentSong,
            Choices = snapshot.Choices,
            HintIndex = snapshot.HintIndex,
            Tallies = snapshot.Tallies,
            Scores = snapshot.Scores,
            Catalog = snapshot.Catalog,
            SongStartedAtUtc = snapshot.SongStartedAtUtc
        };
        
        // F18 - Apply content safety checks before updating state
        var safeState = ApplyContentSafety(newState);
        
        _currentState = safeState;
        _lastSnapshotHash = snapshotHash;
        StateChanged?.Invoke(_currentState);
    }
    
    public void UpdateTally(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= _currentState.Tallies.Count) return;
        
        var tallies = _currentState.Tallies.ToArray();
        tallies[choiceIndex]++;
        
        var updatedState = _currentState.Copy();
        updatedState.Tallies = tallies;
        _currentState = updatedState;
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
    
    private string CreateSnapshotHash(GameStateSnapshot snapshot)
    {
        // Create a simple hash based on key state properties
        var hashInput = $"{snapshot.Phase}|{snapshot.SongIndex}|{snapshot.HintIndex}|{snapshot.Tallies.Count}|{string.Join(",", snapshot.Tallies)}|{snapshot.Choices.Count}";
        return hashInput.GetHashCode().ToString();
    }
    
    // F18 - Content safety checks
    private GameState ApplyContentSafety(GameState state)
    {
        if (_contentSafetyService == null)
            return state;
        
        var safeState = state.Copy();
        
        // Sanitize session code (should be safe but check anyway)
        var sessionResult = _contentSafetyService.SanitizeText(state.SessionCode, ContentType.General);
        if (sessionResult.WasModified)
        {
            Console.WriteLine($"[content-safety] Session code sanitized: {sessionResult.Warnings}");
        }
        safeState.SessionCode = sessionResult.SafeContent;
        
        // Sanitize current song information
        if (state.CurrentSong != null)
        {
            var titleResult = _contentSafetyService.SanitizeSongTitle(state.CurrentSong.Title);
            var artistResult = _contentSafetyService.SanitizeArtistName(state.CurrentSong.Artist);
            
            if (titleResult.WasModified || artistResult.WasModified)
            {
                Console.WriteLine($"[content-safety] Song info sanitized - Title: {titleResult.Warnings}, Artist: {artistResult.Warnings}");
            }
            
            // Create a safe copy of the current song with sanitized data
            safeState.CurrentSong = state.CurrentSong with 
            { 
                Title = titleResult.SafeContent,
                Artist = artistResult.SafeContent 
            };
        }
        
        // Sanitize choices
        var safeChoices = new List<string>();
        for (int i = 0; i < state.Choices.Count; i++)
        {
            var choiceResult = _contentSafetyService.SanitizeChoice(state.Choices[i], i);
            if (choiceResult.WasModified)
            {
                Console.WriteLine($"[content-safety] Choice {i + 1} sanitized: {choiceResult.Warnings}");
            }
            safeChoices.Add(choiceResult.SafeContent);
        }
        safeState.Choices = safeChoices;
        
        // Sanitize player names in scores (keys are player names)
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
        safeState.Scores = safeScores;
        
        return safeState;
    }
}
