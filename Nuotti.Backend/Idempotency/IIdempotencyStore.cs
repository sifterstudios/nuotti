using System;

namespace Nuotti.Backend.Idempotency;

public interface IIdempotencyStore
{
    /// <summary>
    /// Try to register a command for a given session.
    /// Returns true if this is the first time we see this command within TTL; false if it's a duplicate (no-op).
    /// </summary>
    bool TryRegister(string session, Guid commandId);
}
