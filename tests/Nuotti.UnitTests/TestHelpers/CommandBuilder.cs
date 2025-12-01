using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.UnitTests.TestHelpers;

/// <summary>
/// Fluent builder for creating test commands with sensible defaults.
/// </summary>
public class CommandBuilder
{
    private string _sessionCode = "TEST-SESSION";
    private Role _role = Role.Performer;
    private string _issuedById = "test-actor";
    private Guid _commandId = Guid.NewGuid();
    private DateTime _issuedAtUtc = DateTime.UtcNow;

    public CommandBuilder WithSessionCode(string sessionCode)
    {
        _sessionCode = sessionCode;
        return this;
    }

    public CommandBuilder WithRole(Role role)
    {
        _role = role;
        return this;
    }

    public CommandBuilder WithIssuedById(string issuedById)
    {
        _issuedById = issuedById;
        return this;
    }

    public CommandBuilder WithCommandId(Guid commandId)
    {
        _commandId = commandId;
        return this;
    }

    public CommandBuilder WithIssuedAtUtc(DateTime issuedAtUtc)
    {
        _issuedAtUtc = issuedAtUtc;
        return this;
    }

    public StartGame BuildStartGame()
    {
        return new StartGame
        {
            SessionCode = _sessionCode,
            IssuedByRole = _role,
            IssuedById = _issuedById,
            CommandId = _commandId,
            IssuedAtUtc = _issuedAtUtc
        };
    }

    public NextRound BuildNextRound(SongId songId)
    {
        return new NextRound(songId)
        {
            SessionCode = _sessionCode,
            IssuedByRole = _role,
            IssuedById = _issuedById,
            CommandId = _commandId,
            IssuedAtUtc = _issuedAtUtc
        };
    }

    public GiveHint BuildGiveHint(Hint hint)
    {
        return new GiveHint(hint)
        {
            SessionCode = _sessionCode,
            IssuedByRole = _role,
            IssuedById = _issuedById,
            CommandId = _commandId,
            IssuedAtUtc = _issuedAtUtc
        };
    }

    public LockAnswers BuildLockAnswers()
    {
        return new LockAnswers
        {
            SessionCode = _sessionCode,
            IssuedByRole = _role,
            IssuedById = _issuedById,
            CommandId = _commandId,
            IssuedAtUtc = _issuedAtUtc
        };
    }

    public RevealAnswer BuildRevealAnswer(SongRef songRef, int correctChoiceIndex)
    {
        return new RevealAnswer(songRef, correctChoiceIndex)
        {
            SessionCode = _sessionCode,
            IssuedByRole = _role,
            IssuedById = _issuedById,
            CommandId = _commandId,
            IssuedAtUtc = _issuedAtUtc
        };
    }

    public PlaySong BuildPlaySong(SongId songId)
    {
        return new PlaySong(songId)
        {
            SessionCode = _sessionCode,
            IssuedByRole = _role,
            IssuedById = _issuedById,
            CommandId = _commandId,
            IssuedAtUtc = _issuedAtUtc
        };
    }

    public EndSong BuildEndSong(SongId songId)
    {
        return new EndSong(songId)
        {
            SessionCode = _sessionCode,
            IssuedByRole = _role,
            IssuedById = _issuedById,
            CommandId = _commandId,
            IssuedAtUtc = _issuedAtUtc
        };
    }

    public SubmitAnswer BuildSubmitAnswer(SongId songId)
    {
        return new SubmitAnswer(songId)
        {
            SessionCode = _sessionCode,
            IssuedByRole = _role,
            IssuedById = _issuedById,
            CommandId = _commandId,
            IssuedAtUtc = _issuedAtUtc
        };
    }
}

