namespace Nuotti.Backend.Trials;

/// <summary>
/// An event band's request to join the exclusive Nuotti trial.
/// </summary>
public sealed record TrialApplication(
    string Id,
    string BandName,
    string ContactName,
    string Email,
    string City,
    string AudienceSize,
    string? Note,
    DateTimeOffset SubmittedAtUtc);

public sealed record TrialApplicationRequest(
    string BandName,
    string ContactName,
    string Email,
    string City,
    string AudienceSize,
    string? Note = null);

public interface ITrialApplicationStore
{
    Task<TrialApplication> SubmitAsync(TrialApplicationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrialApplication>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Process-local trial waitlist. Sufficient for Development and for Production until a durable
/// waitlist adapter is wired; applications survive for the lifetime of the Backend process.
/// </summary>
public sealed class InMemoryTrialApplicationStore : ITrialApplicationStore
{
    readonly object _gate = new();
    readonly List<TrialApplication> _applications = [];
    readonly Dictionary<string, TrialApplication> _byEmail = new(StringComparer.OrdinalIgnoreCase);

    static readonly HashSet<string> AllowedAudienceSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        "under-50",
        "50-150",
        "150-400",
        "400-plus"
    };

    public Task<TrialApplication> SubmitAsync(TrialApplicationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = Normalize(request);

        lock (_gate)
        {
            if (_byEmail.TryGetValue(normalized.Email, out var existing))
            {
                // Re-submits with the same email refresh the profile rather than stacking duplicates.
                var updated = existing with
                {
                    BandName = normalized.BandName,
                    ContactName = normalized.ContactName,
                    City = normalized.City,
                    AudienceSize = normalized.AudienceSize,
                    Note = normalized.Note,
                    SubmittedAtUtc = DateTimeOffset.UtcNow
                };
                var index = _applications.FindIndex(a => a.Id == existing.Id);
                if (index >= 0) _applications[index] = updated;
                _byEmail[normalized.Email] = updated;
                return Task.FromResult(updated);
            }

            var created = new TrialApplication(
                Id: Guid.NewGuid().ToString("N"),
                BandName: normalized.BandName,
                ContactName: normalized.ContactName,
                Email: normalized.Email,
                City: normalized.City,
                AudienceSize: normalized.AudienceSize,
                Note: normalized.Note,
                SubmittedAtUtc: DateTimeOffset.UtcNow);
            _applications.Add(created);
            _byEmail[created.Email] = created;
            return Task.FromResult(created);
        }
    }

    public Task<IReadOnlyList<TrialApplication>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
            return Task.FromResult<IReadOnlyList<TrialApplication>>(_applications.ToArray());
    }

    public static TrialApplicationRequest Normalize(TrialApplicationRequest request)
    {
        var bandName = (request.BandName ?? string.Empty).Trim();
        var contactName = (request.ContactName ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim();
        var city = (request.City ?? string.Empty).Trim();
        var audienceSize = (request.AudienceSize ?? string.Empty).Trim();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (bandName.Length is < 1 or > 120)
            throw new ArgumentException("Band name must be between 1 and 120 characters.", nameof(request));
        if (contactName.Length is < 1 or > 120)
            throw new ArgumentException("Contact name must be between 1 and 120 characters.", nameof(request));
        if (city.Length is < 1 or > 120)
            throw new ArgumentException("City must be between 1 and 120 characters.", nameof(request));
        if (note is { Length: > 1000 })
            throw new ArgumentException("Note must be at most 1000 characters.", nameof(request));
        if (!AllowedAudienceSizes.Contains(audienceSize))
            throw new ArgumentException("Audience size is not a recognised option.", nameof(request));
        if (!IsValidEmail(email))
            throw new ArgumentException("A valid email address is required.", nameof(request));

        return new TrialApplicationRequest(bandName, contactName, email, city, audienceSize, note);
    }

    static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 320) return false;
        try
        {
            var address = new System.Net.Mail.MailAddress(value);
            return address.Address.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
