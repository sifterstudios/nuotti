using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;

namespace Nuotti.Backend.Sessions;

public sealed class InMemorySessionStore : ISessionStore, IDisposable
{
    readonly TimeProvider _time;
    readonly TimeSpan _idleTimeout;
    readonly TimeSpan _scanInterval;
    readonly ITimer _timer;

    readonly ConcurrentDictionary<string, SessionState> _sessions = new ConcurrentDictionary<string, SessionState>(); // session -> state
    readonly ConcurrentDictionary<string, (string session, string role)> _byConnection = new ConcurrentDictionary<string, (string session, string role)>(); // connId -> (session,role)

    public InMemorySessionStore(IOptions<NuottiOptions> options, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _idleTimeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.SessionIdleTimeoutSeconds));
        _scanInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.SessionEvictionIntervalSeconds));
        _timer = _time.CreateTimer(Scan, null, _scanInterval, _scanInterval);
    }

    public void Touch(string session, string role, string connectionId, string? audienceName = null)
    {
        var normalizedRole = role.ToLowerInvariant();
        var state = _sessions.GetOrAdd(session, _ => new SessionState(_time));
        state.Touch(normalizedRole, connectionId, audienceName);
        _byConnection[connectionId] = (session, normalizedRole);
    }

    public void Remove(string connectionId)
    {
        if (!_byConnection.TryRemove(connectionId, out var info)) return;
        if (_sessions.TryGetValue(info.session, out var state))
        {
            state.Remove(info.role, connectionId);
            if (state.IsEmpty)
            {
                _sessions.TryRemove(info.session, out _);
            }
        }
    }

    public RoleCounts GetCounts(string session)
    {
        if (_sessions.TryGetValue(session, out var state))
        {
            return state.GetCounts();
        }
        return new RoleCounts(0, 0, 0, 0);
    }

    public void EvictIdleNow() => Scan(null);

    void Scan(object? _)
    {
        var now = _time.GetUtcNow();
        foreach (var kvp in _sessions)
        {
            var sess = kvp.Key;
            var state = kvp.Value;
            if (now - state.LastSeen > _idleTimeout)
            {
                // Evict entire session and its connections
                _sessions.TryRemove(sess, out SessionState _removed);
                foreach (var conn in state.GetAllConnectionIds())
                {
                    _byConnection.TryRemove(conn, out (string session, string role) _info);
                }
            }
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    sealed class SessionState
    {
        readonly TimeProvider _time;
        public DateTimeOffset LastSeen { get; private set; }
        readonly ConcurrentDictionary<string, byte> _performer = new();
        readonly ConcurrentDictionary<string, byte> _projector = new();
        readonly ConcurrentDictionary<string, byte> _engine = new();
        readonly ConcurrentDictionary<string, (string name, byte marker)> _audiences = new();

        public SessionState(TimeProvider time)
        {
            _time = time;
            LastSeen = _time.GetUtcNow();
        }

        public void Touch(string role, string connId, string? audienceName)
        {
            LastSeen = _time.GetUtcNow();
            switch (role)
            {
                case "performer":
                    _performer[connId] = 1;
                    break;
                case "projector":
                    _projector[connId] = 1;
                    break;
                case "engine":
                    _engine[connId] = 1;
                    break;
                case "audience":
                    _audiences[connId] = (audienceName ?? string.Empty, 1);
                    break;
                default:
                    // treat unknown roles as audience for counting
                    _audiences[connId] = (audienceName ?? string.Empty, 1);
                    break;
            }
        }

        public void Remove(string role, string connId)
        {
            switch (role)
            {
                case "performer":
                    _performer.TryRemove(connId, out _);
                    break;
                case "projector":
                    _projector.TryRemove(connId, out _);
                    break;
                case "engine":
                    _engine.TryRemove(connId, out _);
                    break;
                case "audience":
                default:
                    _audiences.TryRemove(connId, out _);
                    break;
            }
            if (IsEmpty)
            {
                // last seen updated to mark possible eviction
                LastSeen = _time.GetUtcNow();
            }
        }

        public bool IsEmpty => _performer.IsEmpty && _projector.IsEmpty && _engine.IsEmpty && _audiences.IsEmpty;

        public RoleCounts GetCounts()
        {
            return new RoleCounts(
                Performer: _performer.Count,
                Projector: _projector.Count,
                Engine: _engine.Count,
                Audiences: _audiences.Count
            );
        }

        public IEnumerable<string> GetAllConnectionIds()
        {
            foreach (var k in _performer.Keys) yield return k;
            foreach (var k in _projector.Keys) yield return k;
            foreach (var k in _engine.Keys) yield return k;
            foreach (var k in _audiences.Keys) yield return k;
        }
    }
}