using System.Text.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.Contracts.Tests.V1.Protocol;

public sealed class SessionProtocolTests
{
    [Fact]
    public void Command_identifies_one_idempotent_workspace_scoped_intent()
    {
        var command = new StartGame
        {
            CommandId = Guid.Parse("00000000-0000-0000-0000-000000000101"),
            SessionCode = "SHOW1234",
            IssuedByRole = Role.Performer,
            IssuedById = "performer-1"
        };

        var message = new SessionCommand<StartGame>(
            SessionProtocolVersion.Current,
            "workspace-1",
            command,
            ExpectedControlGeneration: new ControlGeneration(7));

        Assert.Equal(command.CommandId, message.CommandId);
        Assert.Equal("SHOW1234", message.SessionCode);
        Assert.Equal(7, message.ExpectedControlGeneration.Value);
    }

    [Fact]
    public void Event_and_cursor_express_durable_ordering_and_replay()
    {
        var @event = new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            EventId = Guid.Parse("00000000-0000-0000-0000-000000000201"),
            CorrelationId = Guid.Parse("00000000-0000-0000-0000-000000000202"),
            CausedByCommandId = Guid.Parse("00000000-0000-0000-0000-000000000203"),
            SessionCode = "SHOW1234",
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start
        };

        var message = new SessionEvent<GamePhaseChanged>(
            SessionProtocolVersion.Current,
            "workspace-1",
            Sequence: new SessionSequence(42),
            @event);

        Assert.Equal(42, message.Cursor.Sequence.Value);
        Assert.Equal("workspace-1", message.Cursor.WorkspaceId);
        Assert.Equal("SHOW1234", message.Cursor.SessionCode);
        Assert.Equal(@event.EventId, message.EventId);
    }

    [Theory]
    [InlineData(Outcome.Applied)]
    [InlineData(Outcome.Duplicate)]
    [InlineData(Outcome.Rejected)]
    public void Command_result_has_an_explicit_outcome(Outcome outcome)
    {
        var result = new SessionCommandResult(
            SessionProtocolVersion.Current,
            Guid.NewGuid(),
            outcome,
            Cursor: null,
            Problem: outcome == Outcome.Rejected
                ? NuottiProblem.Conflict("Rejected", "No mutation occurred", ReasonCode.InvalidStateTransition)
                : null);

        Assert.Equal(outcome, result.Outcome);
    }

    [Fact]
    public void Snapshot_declares_reader_compatibility_and_resume_cursor()
    {
        var snapshot = new SessionSnapshot<GameStateSnapshot>(
            WriterVersion: new SessionProtocolVersion(1, 3),
            MinimumReaderVersion: new SessionProtocolVersion(1, 1),
            WorkspaceId: "workspace-1",
            SessionCode: "SHOW1234",
            LastSequence: new SessionSequence(99),
            ControlGeneration: new ControlGeneration(7),
            State: new GameStateSnapshot("SHOW1234", Phase.Guessing, 0));

        Assert.True(snapshot.CanBeReadBy(new SessionProtocolVersion(1, 1)));
        Assert.True(snapshot.CanBeReadBy(new SessionProtocolVersion(1, 9)));
        Assert.False(snapshot.CanBeReadBy(new SessionProtocolVersion(1, 0)));
        Assert.False(snapshot.CanBeReadBy(new SessionProtocolVersion(2, 0)));
        Assert.Equal(99, snapshot.Cursor.Sequence.Value);
    }

    [Fact]
    public void New_protocol_contracts_round_trip_with_rest_options()
    {
        var cursor = new SessionCursor("workspace-1", "SHOW1234", new SessionSequence(12));

        var json = JsonSerializer.Serialize(cursor, ContractsJson.RestOptions);
        var restored = JsonSerializer.Deserialize<SessionCursor>(json, ContractsJson.RestOptions);

        Assert.Equal(cursor, restored);
    }

    [Fact]
    public void Cursor_cannot_move_backwards_or_repeat_a_sequence()
    {
        var cursor = new SessionCursor("workspace-1", "SHOW1234", new SessionSequence(12));

        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.AdvanceTo(new SessionSequence(12)));
        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.AdvanceTo(new SessionSequence(11)));
        Assert.Equal(13, cursor.AdvanceTo(new SessionSequence(13)).Sequence.Value);
    }

    [Fact]
    public void Sequence_and_control_generation_reject_negative_values_and_advance_monotonically()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionSequence(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ControlGeneration(-1));
        Assert.Equal(8, new ControlGeneration(7).Next().Value);
        Assert.Equal(43, new SessionSequence(42).Next().Value);
    }

    [Fact]
    public void Outcome_and_64_bit_counters_use_lossless_web_wire_shapes()
    {
        const long BeyondJavaScriptSafeInteger = 9_007_199_254_740_993;
        var result = new SessionCommandResult(
            SessionProtocolVersion.Current,
            Guid.Parse("00000000-0000-0000-0000-000000000301"),
            Outcome.Applied,
            new SessionCursor("workspace-1", "SHOW1234", new SessionSequence(BeyondJavaScriptSafeInteger)),
            Problem: null);

        var json = JsonSerializer.Serialize(result, ContractsJson.RestOptions);
        var restored = JsonSerializer.Deserialize<SessionCommandResult>(json, ContractsJson.RestOptions);

        Assert.Contains("\"outcome\":\"Applied\"", json);
        Assert.Contains($"\"sequence\":\"{BeyondJavaScriptSafeInteger}\"", json);
        Assert.Equal(BeyondJavaScriptSafeInteger, restored!.Cursor!.Sequence.Value);

        var generationJson = JsonSerializer.Serialize(
            new ControlGeneration(BeyondJavaScriptSafeInteger), ContractsJson.RestOptions);
        Assert.Equal($"\"{BeyondJavaScriptSafeInteger}\"", generationJson);
    }
}
