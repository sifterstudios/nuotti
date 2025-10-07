using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;

namespace Nuotti.Backend.Idempotency;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly TimeProvider _time;
    private readonly TimeSpan _ttl;
    private readonly int _maxPerSession;

    private readonly ConcurrentDictionary<string, SessionEntry> _bySession = new();

    public InMemoryIdempotencyStore(IOptions<NuottiOptions> options, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _ttl = TimeSpan.FromSeconds(Math.Max(1, options.Value.IdempotencyTtlSeconds));
        _maxPerSession = Math.Max(1, options.Value.IdempotencyMaxPerSession);
    }

    public bool TryRegister(string session, Guid commandId)
    {
        var now = _time.GetUtcNow();
        var entry = _bySession.GetOrAdd(session, _ => new SessionEntry(_maxPerSession));
        lock (entry.Lock)
        {
            // Drop expired items first
            entry.PruneExpired(now, _ttl);
            if (entry.Seen.ContainsKey(commandId))
            {
                var seenAt = entry.Seen[commandId];
                if (now - seenAt <= _ttl)
                {
                    return false; // duplicate within TTL
                }
                // expired; treat as new
                entry.Remove(commandId);
            }
            entry.Add(commandId, now);
            return true;
        }
    }

    private sealed class SessionEntry
    {
        public object Lock { get; } = new object();
        public Dictionary<Guid, DateTimeOffset> Seen { get; } = new Dictionary<Guid, DateTimeOffset>();
        private readonly Queue<Guid> _order;
        private readonly int _capacity;

        public SessionEntry(int capacity)
        {
            _capacity = capacity;
            _order = new Queue<Guid>(capacity);
        }

        public void Add(Guid id, DateTimeOffset ts)
        {
            Seen[id] = ts;
            _order.Enqueue(id);
            while (_order.Count > _capacity)
            {
                var old = _order.Dequeue();
                Seen.Remove(old);
            }
        }

        public void Remove(Guid id)
        {
            // Remove from Seen; _order retains old ids but will be naturally popped as capacity exceeds.
            Seen.Remove(id);
        }

        public void PruneExpired(DateTimeOffset now, TimeSpan ttl)
        {
            if (Seen.Count == 0) return;
            // Since we maintain a FIFO queue, we can dequeue while expired.
            int safety = _order.Count; // prevent infinite loop if out-of-sync
            while (_order.Count > 0 && safety-- > 0)
            {
                var first = _order.Peek();
                if (Seen.TryGetValue(first, out var ts))
                {
                    if (now - ts > ttl)
                    {
                        _order.Dequeue();
                        Seen.Remove(first);
                        continue;
                    }
                }
                // not expired or missing; stop
                break;
            }
        }
    }
}
