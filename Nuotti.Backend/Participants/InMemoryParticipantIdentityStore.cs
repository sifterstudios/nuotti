namespace Nuotti.Backend.Participants;

/// <summary>
/// Anonymous, device-bound Audience identity scoped to one Session.
/// </summary>
public sealed record Participant(
    string ParticipantId,
    string SessionCode,
    string DisplayName,
    bool NameIsModerated);

public interface IParticipantIdentityStore
{
    Participant JoinOrRestore(string sessionCode, string deviceSecret, string? displayName);
    bool TryGet(string sessionCode, string participantId, out Participant? participant);
    bool TryModerateName(string sessionCode, string participantId, string moderatedName, out Participant? participant);
}

public static class ParticipantNameRules
{
    static readonly HashSet<string> Profanity = new(StringComparer.OrdinalIgnoreCase)
    {
        "damn", "hell", "crap", "stupid", "idiot", "moron", "dumb", "suck", "hate"
    };

    public static string NormalizeOrThrow(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        var trimmed = displayName.Trim();
        if (trimmed.Length < 2)
            throw new ArgumentException("Name must be at least 2 characters long.", nameof(displayName));
        if (trimmed.Length > 20)
            throw new ArgumentException("Name must be 20 characters or less.", nameof(displayName));

        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => Profanity.Contains(w)))
            throw new ArgumentException("Name contains inappropriate content.", nameof(displayName));

        return trimmed;
    }
}

public sealed class InMemoryParticipantIdentityStore : IParticipantIdentityStore
{
    readonly object _gate = new();
    readonly Dictionary<(string Session, string DeviceSecret), Participant> _byDevice = new();
    readonly Dictionary<(string Session, string ParticipantId), Participant> _byId = new();

    public Participant JoinOrRestore(string sessionCode, string deviceSecret, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(sessionCode))
            throw new ArgumentException("Session code is required.", nameof(sessionCode));
        if (string.IsNullOrWhiteSpace(deviceSecret))
            throw new ArgumentException("Device secret is required.", nameof(deviceSecret));

        lock (_gate)
        {
            var key = (sessionCode, deviceSecret);
            if (_byDevice.TryGetValue(key, out var existing))
            {
                if (string.IsNullOrWhiteSpace(displayName) || existing.NameIsModerated)
                    return existing;

                var renamed = existing with { DisplayName = ParticipantNameRules.NormalizeOrThrow(displayName) };
                _byDevice[key] = renamed;
                _byId[(sessionCode, renamed.ParticipantId)] = renamed;
                return renamed;
            }

            var name = string.IsNullOrWhiteSpace(displayName)
                ? "Guest"
                : ParticipantNameRules.NormalizeOrThrow(displayName);
            var participant = new Participant($"part_{Guid.NewGuid():N}", sessionCode, name, NameIsModerated: false);
            _byDevice[key] = participant;
            _byId[(sessionCode, participant.ParticipantId)] = participant;
            return participant;
        }
    }

    public bool TryGet(string sessionCode, string participantId, out Participant? participant)
    {
        lock (_gate)
        {
            if (_byId.TryGetValue((sessionCode, participantId), out var found))
            {
                participant = found;
                return true;
            }

            participant = null;
            return false;
        }
    }

    public bool TryModerateName(string sessionCode, string participantId, string moderatedName, out Participant? participant)
    {
        lock (_gate)
        {
            if (!_byId.TryGetValue((sessionCode, participantId), out var existing))
            {
                participant = null;
                return false;
            }

            var name = ParticipantNameRules.NormalizeOrThrow(moderatedName);
            var updated = existing with { DisplayName = name, NameIsModerated = true };
            _byId[(sessionCode, participantId)] = updated;
            foreach (var kvp in _byDevice.Where(k => k.Value.ParticipantId == participantId && k.Key.Session == sessionCode).ToList())
                _byDevice[kvp.Key] = updated;
            participant = updated;
            return true;
        }
    }
}
