using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;

namespace Nuotti.UnitTests.TestHelpers;

/// <summary>
/// Fluent builder for creating test events with sensible defaults.
/// </summary>
public class EventBuilder
{
    private string _sessionCode = "TEST-SESSION";
    private Guid _correlationId = Guid.NewGuid();
    private Guid _causedByCommandId = Guid.NewGuid();
    private Guid _eventId = Guid.NewGuid();
    private DateTime _emittedAtUtc = DateTime.UtcNow;

    public EventBuilder WithSessionCode(string sessionCode)
    {
        _sessionCode = sessionCode;
        return this;
    }

    public EventBuilder WithCorrelationId(Guid correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    public EventBuilder WithCausedByCommandId(Guid causedByCommandId)
    {
        _causedByCommandId = causedByCommandId;
        return this;
    }

    public EventBuilder WithEventId(Guid eventId)
    {
        _eventId = eventId;
        return this;
    }

    public EventBuilder WithEmittedAtUtc(DateTime emittedAtUtc)
    {
        _emittedAtUtc = emittedAtUtc;
        return this;
    }

    public GamePhaseChanged BuildGamePhaseChanged(Phase currentPhase, Phase newPhase)
    {
        return new GamePhaseChanged(currentPhase, newPhase)
        {
            SessionCode = _sessionCode,
            CorrelationId = _correlationId,
            CausedByCommandId = _causedByCommandId,
            EventId = _eventId,
            EmittedAtUtc = _emittedAtUtc,
            CurrentPhase = currentPhase,
            NewPhase = newPhase
        };
    }

    public AnswerSubmitted BuildAnswerSubmitted(string audienceId, int choiceIndex)
    {
        return new AnswerSubmitted(audienceId, choiceIndex)
        {
            SessionCode = _sessionCode,
            CorrelationId = _correlationId,
            CausedByCommandId = _causedByCommandId,
            EventId = _eventId,
            EmittedAtUtc = _emittedAtUtc,
            AudienceId = audienceId,
            ChoiceIndex = choiceIndex
        };
    }

    public CorrectAnswerRevealed BuildCorrectAnswerRevealed(int correctChoiceIndex)
    {
        return new CorrectAnswerRevealed(correctChoiceIndex)
        {
            SessionCode = _sessionCode,
            CorrelationId = _correlationId,
            CausedByCommandId = _causedByCommandId,
            EventId = _eventId,
            EmittedAtUtc = _emittedAtUtc,
            CorrectChoiceIndex = correctChoiceIndex
        };
    }
}

