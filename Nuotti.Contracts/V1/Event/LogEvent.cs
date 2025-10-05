namespace Nuotti.Contracts.V1.Event;

public record LogEvent(
    DateTimeOffset Timestamp,
    string Level,
    string Source,
    string Message,
    string? ConnectionId = null,
    string? Session = null,
    string? Role = null
);
