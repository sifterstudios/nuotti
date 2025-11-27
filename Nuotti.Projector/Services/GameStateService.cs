using System;
using System.Linq;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Services;

public class GameStateService
{
    private GameState _currentState = new();
    private string _lastSnapshotHash = string.Empty;
    
    public event Action<GameState>? StateChanged;
    
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
        
        _currentState = newState;
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
}
