using System;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Services;

public class GameStateService
{
    private GameState _currentState = new();
    
    public event Action<GameState>? StateChanged;
    
    public GameState CurrentState => _currentState;
    
    public void UpdateFromSnapshot(GameStateSnapshot snapshot)
    {
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
        StateChanged?.Invoke(_currentState);
    }
    
    public void UpdateTally(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= _currentState.Tallies.Count) return;
        
        var tallies = _currentState.Tallies.ToArray();
        tallies[choiceIndex]++;
        
        _currentState = _currentState with { Tallies = tallies };
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
}
