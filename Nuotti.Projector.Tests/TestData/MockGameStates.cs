using System;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Projector.Tests.TestData;

public static class MockGameStates
{
    public static GameStateSnapshot CreateLobbyState(int playerCount = 3)
    {
        return new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Lobby,
            songIndex: 0,
            currentSong: null,
            catalog: CreateMockCatalog(),
            choices: new List<string>(),
            hintIndex: 0,
            tallies: new List<int>(),
            scores: new Dictionary<string, int>(),
            songStartedAtUtc: null
        );
    }
    
    public static GameStateSnapshot CreateReadyState()
    {
        return new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Start,
            songIndex: 0,
            currentSong: CreateMockSong(),
            catalog: CreateMockCatalog(),
            choices: CreateMockChoices(),
            hintIndex: 0,
            tallies: new int[] { 0, 0, 0, 0 },
            scores: CreateMockScores(),
            songStartedAtUtc: DateTime.UtcNow
        );
    }
    
    public static GameStateSnapshot CreateSongIntroState()
    {
        return new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Play,
            songIndex: 0,
            currentSong: CreateMockSong(),
            catalog: CreateMockCatalog(),
            choices: CreateMockChoices(),
            hintIndex: 0,
            tallies: new int[] { 0, 0, 0, 0 },
            scores: CreateMockScores(),
            songStartedAtUtc: DateTime.UtcNow
        );
    }
    
    public static GameStateSnapshot CreateHintState(int hintCount = 2)
    {
        return new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Hint,
            songIndex: 0,
            currentSong: CreateMockSong(),
            catalog: CreateMockCatalog(),
            choices: CreateMockChoices(),
            hintIndex: hintCount - 1,
            tallies: new int[] { 0, 0, 0, 0 },
            scores: CreateMockScores(),
            songStartedAtUtc: DateTime.UtcNow.AddSeconds(-30)
        );
    }
    
    public static GameStateSnapshot CreateGuessingState()
    {
        return new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Guessing,
            songIndex: 0,
            currentSong: CreateMockSong(),
            catalog: CreateMockCatalog(),
            choices: CreateMockChoices(),
            hintIndex: 2,
            tallies: new int[] { 2, 1, 0, 1 },
            scores: CreateMockScores(),
            songStartedAtUtc: DateTime.UtcNow.AddSeconds(-45)
        );
    }
    
    public static GameStateSnapshot CreateRevealState()
    {
        return new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Reveal,
            songIndex: 0,
            currentSong: CreateMockSong(),
            catalog: CreateMockCatalog(),
            choices: CreateMockChoices(),
            hintIndex: 3,
            tallies: new int[] { 3, 1, 0, 0 },
            scores: CreateMockScores(),
            songStartedAtUtc: DateTime.UtcNow.AddMinutes(-1)
        );
    }
    
    public static GameStateSnapshot CreateIntermissionState()
    {
        return new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Intermission,
            songIndex: 1,
            currentSong: null,
            catalog: CreateMockCatalog(),
            choices: new List<string>(),
            hintIndex: 0,
            tallies: new List<int>(),
            scores: CreateMockScores(),
            songStartedAtUtc: null
        );
    }
    
    public static GameStateSnapshot CreateFinishedState()
    {
        return new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Finished,
            songIndex: 3,
            currentSong: null,
            catalog: CreateMockCatalog(),
            choices: new List<string>(),
            hintIndex: 0,
            tallies: new List<int>(),
            scores: CreateMockScores(),
            songStartedAtUtc: null
        );
    }
    
    private static SongRef CreateMockSong()
    {
        return new SongRef(new SongId("song-1"), "Bohemian Rhapsody", "Queen");
    }
    
    private static List<SongRef> CreateMockCatalog()
    {
        return new List<SongRef>
        {
            new SongRef(new SongId("song-1"), "Bohemian Rhapsody", "Queen"),
            new SongRef(new SongId("song-2"), "Hotel California", "Eagles"),
            new SongRef(new SongId("song-3"), "Stairway to Heaven", "Led Zeppelin"),
            new SongRef(new SongId("song-4"), "Sweet Child O' Mine", "Guns N' Roses")
        };
    }
    
    private static List<string> CreateMockChoices()
    {
        return new List<string>
        {
            "1974",
            "1975", // Correct answer
            "1976", 
            "1977"
        };
    }
    
    private static Dictionary<string, int> CreateMockScores()
    {
        return new Dictionary<string, int>
        {
            { "player-alice", 850 },
            { "player-bob", 720 },
            { "player-charlie", 650 },
            { "player-diana", 580 }
        };
    }
}
