namespace Nuotti.Contracts.V1.Protocol;

/// <summary>
/// Identifies the wire contract used by a durable Session message.
/// Minor revisions are additive; a different major revision is incompatible.
/// </summary>
public readonly record struct SessionProtocolVersion
{
    public static SessionProtocolVersion Current { get; } = new(1, 0);

    public int Major { get; }
    public int Minor { get; }

    public SessionProtocolVersion(int major, int minor)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(major, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        Major = major;
        Minor = minor;
    }

    public bool IsAtLeast(SessionProtocolVersion minimum)
        => Major == minimum.Major && Minor >= minimum.Minor;
}
