using System.Text.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Serialization;

public class RoundTripTests
{
    public static IEnumerable<object[]> GetCommands()
    {
        var sessionCode = "TEST-SESSION";
        var commandId = Guid.NewGuid();
        var songId = new SongId("song-001");
        var songRef = new SongRef(songId, "Test Song", "Test Artist");
        var hint = new Hint(0, "Hint text", "Performer instructions", songId);

        yield return new object[] { new StartGame { SessionCode = sessionCode, IssuedByRole = Role.Performer, IssuedById = "perf-1", CommandId = commandId } };
        yield return new object[] { new NextRound(songId) { SessionCode = sessionCode, IssuedByRole = Role.Performer, IssuedById = "perf-1", CommandId = commandId } };
        yield return new object[] { new GiveHint(hint) { SessionCode = sessionCode, IssuedByRole = Role.Performer, IssuedById = "perf-1", CommandId = commandId } };
        yield return new object[] { new LockAnswers { SessionCode = sessionCode, IssuedByRole = Role.Performer, IssuedById = "perf-1", CommandId = commandId } };
        yield return new object[] { new RevealAnswer(songRef, 1) { SessionCode = sessionCode, IssuedByRole = Role.Performer, IssuedById = "perf-1", CommandId = commandId } };
        yield return new object[] { new PlaySong(songId) { SessionCode = sessionCode, IssuedByRole = Role.Performer, IssuedById = "perf-1", CommandId = commandId } };
        yield return new object[] { new EndSong(songId) { SessionCode = sessionCode, IssuedByRole = Role.Performer, IssuedById = "perf-1", CommandId = commandId } };
        yield return new object[] { new SubmitAnswer(songId) { SessionCode = sessionCode, IssuedByRole = Role.Audience, IssuedById = "aud-1", CommandId = commandId } };
        yield return new object[] { new QuestionPushed("What is the answer?", ["A", "B", "C"]) { SessionCode = sessionCode, IssuedByRole = Role.Performer, IssuedById = "perf-1", CommandId = commandId } };
    }

    public static IEnumerable<object[]> GetEvents()
    {
        var sessionCode = "TEST-SESSION";
        var correlationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        yield return new object[] { new GamePhaseChanged(Phase.Lobby, Phase.Start) { SessionCode = sessionCode, CorrelationId = correlationId, CausedByCommandId = commandId, EventId = eventId, CurrentPhase = Phase.Lobby, NewPhase = Phase.Start } };
        yield return new object[] { new AnswerSubmitted("aud-1", 2) { SessionCode = sessionCode, CorrelationId = correlationId, CausedByCommandId = commandId, EventId = eventId, AudienceId = "aud-1", ChoiceIndex = 2 } };
        yield return new object[] { new CorrectAnswerRevealed(1) { SessionCode = sessionCode, CorrelationId = correlationId, CausedByCommandId = commandId, EventId = eventId, CorrectChoiceIndex = 1 } };
    }

    public static IEnumerable<object[]> GetModels()
    {
        var songId = new SongId("song-001");
        var songRef = new SongRef(songId, "Test Song", "Test Artist");

        yield return new object[] { songId };
        yield return new object[] { songRef };
        yield return new object[] { new Hint(0, "Hint text", "Performer instructions", songId) };
        yield return new object[] { new GameStateSnapshot("TEST-SESSION", Phase.Guessing, 1, songRef, catalog: [songRef], choices: ["A", "B", "C"], hintIndex: 0, tallies: [5, 3, 2], scores: new Dictionary<string, int> { ["p1"] = 10, ["p2"] = 5 }) };
        yield return new object[] { new NuottiProblem("Test Title", 400, "Test detail", ReasonCode.InvalidStateTransition, "field", Guid.NewGuid()) };
    }

    [Theory]
    [MemberData(nameof(GetCommands))]
    public void Command_RoundTrip_RestOptions(CommandBase command)
    {
        RoundTripTest(command, ContractsJson.RestOptions);
    }

    [Theory]
    [MemberData(nameof(GetCommands))]
    public void Command_RoundTrip_HubOptions(CommandBase command)
    {
        RoundTripTest(command, ContractsJson.HubOptions);
    }

    [Theory]
    [MemberData(nameof(GetEvents))]
    public void Event_RoundTrip_RestOptions(EventBase evt)
    {
        RoundTripTest(evt, ContractsJson.RestOptions);
    }

    [Theory]
    [MemberData(nameof(GetEvents))]
    public void Event_RoundTrip_HubOptions(EventBase evt)
    {
        RoundTripTest(evt, ContractsJson.HubOptions);
    }

    [Theory]
    [MemberData(nameof(GetModels))]
    public void Model_RoundTrip_RestOptions(object model)
    {
        RoundTripTest(model, ContractsJson.RestOptions);
    }

    [Theory]
    [MemberData(nameof(GetModels))]
    public void Model_RoundTrip_HubOptions(object model)
    {
        RoundTripTest(model, ContractsJson.HubOptions);
    }

    private static void RoundTripTest<T>(T original, JsonSerializerOptions options)
    {
        // Serialize
        var json1 = JsonSerializer.Serialize(original, options);
        Assert.NotNull(json1);
        Assert.NotEmpty(json1);

        // Deserialize
        var deserialized = JsonSerializer.Deserialize<T>(json1, options);
        Assert.NotNull(deserialized);

        // Serialize again
        var json2 = JsonSerializer.Serialize(deserialized, options);
        Assert.NotNull(json2);

        // Both JSON strings should be equal (round-trip equality)
        Assert.Equal(json1, json2);

        // Deep equality check using JSON comparison
        var obj1 = JsonSerializer.Deserialize<JsonElement>(json1, options);
        var obj2 = JsonSerializer.Deserialize<JsonElement>(json2, options);
        Assert.Equal(obj1.ToString(), obj2.ToString());
    }

    [Fact]
    public Task Command_PropertyNames_RestOptions_Snapshot()
    {
        var command = new StartGame
        {
            SessionCode = "TEST-SESSION",
            IssuedByRole = Role.Performer,
            IssuedById = "perf-1",
            CommandId = Guid.Parse("00000000-0000-0000-0000-000000000001")
        };
        var json = JsonSerializer.Serialize(command, ContractsJson.RestOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }

    [Fact]
    public Task Command_PropertyNames_HubOptions_Snapshot()
    {
        var command = new StartGame
        {
            SessionCode = "TEST-SESSION",
            IssuedByRole = Role.Performer,
            IssuedById = "perf-1",
            CommandId = Guid.Parse("00000000-0000-0000-0000-000000000001")
        };
        var json = JsonSerializer.Serialize(command, ContractsJson.HubOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }

    [Fact]
    public Task Event_PropertyNames_RestOptions_Snapshot()
    {
        var evt = new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            SessionCode = "TEST-SESSION",
            CorrelationId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CausedByCommandId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            EventId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start
        };
        var json = JsonSerializer.Serialize(evt, ContractsJson.RestOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }

    [Fact]
    public Task Event_PropertyNames_HubOptions_Snapshot()
    {
        var evt = new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            SessionCode = "TEST-SESSION",
            CorrelationId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CausedByCommandId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            EventId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start
        };
        var json = JsonSerializer.Serialize(evt, ContractsJson.HubOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }

    [Fact]
    public Task GameStateSnapshot_PropertyNames_RestOptions_Snapshot()
    {
        var songId = new SongId("song-001");
        var songRef = new SongRef(songId, "Test Song", "Test Artist");
        var snapshot = new GameStateSnapshot(
            "TEST-SESSION",
            Phase.Guessing,
            1,
            songRef,
            catalog: [songRef],
            choices: ["Option A", "Option B", "Option C"],
            hintIndex: 0,
            tallies: [5, 3, 2],
            scores: new Dictionary<string, int> { ["player-1"] = 10, ["player-2"] = 8 }
        );
        var json = JsonSerializer.Serialize(snapshot, ContractsJson.RestOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }

    [Fact]
    public Task GameStateSnapshot_PropertyNames_HubOptions_Snapshot()
    {
        var songId = new SongId("song-001");
        var songRef = new SongRef(songId, "Test Song", "Test Artist");
        var snapshot = new GameStateSnapshot(
            "TEST-SESSION",
            Phase.Guessing,
            1,
            songRef,
            catalog: [songRef],
            choices: ["Option A", "Option B", "Option C"],
            hintIndex: 0,
            tallies: [5, 3, 2],
            scores: new Dictionary<string, int> { ["player-1"] = 10, ["player-2"] = 8 }
        );
        var json = JsonSerializer.Serialize(snapshot, ContractsJson.HubOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}

