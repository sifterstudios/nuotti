namespace Nuotti.Backend.ShowAgents;

public sealed class InMemoryShowAgentAccessStore(TimeProvider? timeProvider = null) : IShowAgentAccessStore
{
    sealed record Pairing(string WorkspaceId, string SessionCode, string IssuedBy, DateTimeOffset ExpiresAt, bool Used = false);
    sealed class Agent
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string WorkspaceId { get; init; }
        public required string SessionCode { get; init; }
        public required string CredentialHash { get; init; }
        public long CommandStartSequence { get; init; }
        public bool Revoked { get; set; }
        public ShowAgentConnectionState State { get; set; } = ShowAgentConnectionState.Offline;
        public string? Detail { get; set; }
        public DateTimeOffset? LastSeenAt { get; set; }
    }
    sealed record Token(string AgentId, DateTimeOffset ExpiresAt);
    sealed record AttemptWindow(DateTimeOffset StartedAt, int Count);

    readonly object _gate = new();
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly Dictionary<string, Pairing> _pairings = [];
    readonly Dictionary<string, Agent> _agents = [];
    readonly Dictionary<string, List<string>> _agentIdsByScope = [];
    readonly Dictionary<string, Token> _tokens = [];
    readonly Dictionary<string, List<ShowAgentCommand>> _commands = [];
    readonly Dictionary<string, long> _lastSequenceByScope = [];
    readonly Dictionary<string, AttemptWindow> _attemptsByPrefix = [];
    AttemptWindow? _globalAttempts;

    public Task<ShowAgentPairingCode> IssuePairingCodeAsync(string workspaceId, string sessionCode, string issuedBy,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            PruneExpired();
            string code;
            do code = ShowAgentTokens.PairingCode(); while (_pairings.ContainsKey(ShowAgentTokens.Hash(code)));
            var expires = _time.GetUtcNow().AddMinutes(10);
            _pairings[ShowAgentTokens.Hash(code)] = new(workspaceId, sessionCode, issuedBy, expires);
            return Task.FromResult(new ShowAgentPairingCode(code, expires));
        }
    }

    public Task<PairedShowAgent?> PairAsync(string code, string name, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            PruneExpired();
            RegisterPrefixAttempt(code);
            var hash = ShowAgentTokens.Hash(code);
            if (!_pairings.TryGetValue(hash, out var pairing) || pairing.Used || pairing.ExpiresAt <= _time.GetUtcNow())
                return Task.FromResult<PairedShowAgent?>(null);
            _pairings[hash] = pairing with { Used = true };
            var credential = ShowAgentTokens.NewSecret();
            var scope = Scope(pairing.WorkspaceId, pairing.SessionCode);
            var agent = new Agent
            {
                Id = $"agent_{Guid.NewGuid():N}",
                Name = name.Trim(),
                WorkspaceId = pairing.WorkspaceId,
                SessionCode = pairing.SessionCode,
                CredentialHash = ShowAgentTokens.Hash(credential),
                CommandStartSequence = _lastSequenceByScope.GetValueOrDefault(scope)
            };
            _agents[agent.Id] = agent;
            if (!_agentIdsByScope.TryGetValue(scope, out var ids)) _agentIdsByScope[scope] = ids = [];
            ids.Add(agent.Id);
            var (token, lease) = IssueToken(agent);
            return Task.FromResult<PairedShowAgent?>(new(agent.Id, credential, token, lease.ExpiresAt));
        }
    }

    public Task<(string Token, ShowAgentLease Lease)?> IssueAccessTokenAsync(string credential, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            PruneExpired();
            var hash = ShowAgentTokens.Hash(credential);
            var agent = _agents.Values.FirstOrDefault(candidate => candidate.CredentialHash == hash && !candidate.Revoked);
            return Task.FromResult<(string, ShowAgentLease)?>(agent is null ? null : IssueToken(agent));
        }
    }

    public Task<ShowAgentLease?> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_tokens.TryGetValue(ShowAgentTokens.Hash(accessToken), out var token) || token.ExpiresAt <= _time.GetUtcNow()
                || !_agents.TryGetValue(token.AgentId, out var agent) || agent.Revoked) return Task.FromResult<ShowAgentLease?>(null);
            return Task.FromResult<ShowAgentLease?>(new(agent.Id, agent.WorkspaceId, agent.SessionCode, token.ExpiresAt));
        }
    }

    public Task<bool> ReportStatusAsync(ShowAgentLease lease, ShowAgentConnectionState state, string? detail,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_agents.TryGetValue(lease.AgentId, out var agent) || agent.Revoked) return Task.FromResult(false);
            agent.State = state; agent.Detail = detail; agent.LastSeenAt = _time.GetUtcNow();
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<ShowAgentStatus>> ListStatusesAsync(string workspaceId, string sessionCode,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_agentIdsByScope.TryGetValue(Scope(workspaceId, sessionCode), out var ids) || ids.Count == 0)
                return Task.FromResult<IReadOnlyList<ShowAgentStatus>>([]);
            return Task.FromResult<IReadOnlyList<ShowAgentStatus>>(ids.Select(id =>
            {
                var agent = _agents[id];
                return new ShowAgentStatus(agent.Id, agent.Name, workspaceId, sessionCode,
                    agent.State, agent.Detail, agent.LastSeenAt, agent.Revoked);
            }).ToArray());
        }
    }

    public Task<bool> RevokeAsync(string workspaceId, string sessionCode, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_agentIdsByScope.TryGetValue(Scope(workspaceId, sessionCode), out var ids))
                return Task.FromResult(false);
            var revokedAny = false;
            foreach (var id in ids)
            {
                if (_agents[id].Revoked) continue;
                _agents[id].Revoked = true;
                revokedAny = true;
            }
            return Task.FromResult(revokedAny);
        }
    }

    public Task AppendCommandAsync(string workspaceId, string sessionCode, string messageType, object payload,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var key = Scope(workspaceId, sessionCode);
            var list = _commands.GetValueOrDefault(key);
            if (list is null) _commands[key] = list = [];
            var sequence = _lastSequenceByScope.GetValueOrDefault(key) + 1;
            _lastSequenceByScope[key] = sequence;
            list.Add(new ShowAgentCommand(sequence, messageType, payload));
            // Playback commands describe desired state. While an Agent is offline only the newest
            // Play/Stop intent is meaningful; replaying intermediate tracks would be harmful.
            // Prepare and other control messages must survive until acknowledged.
            if (messageType is "PlayTrack" or "StopTrack")
            {
                var olderPlayback = list.Where(c => c.MessageType is "PlayTrack" or "StopTrack"
                    && c.Sequence < sequence).ToArray();
                foreach (var stale in olderPlayback) list.Remove(stale);
            }
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<ShowAgentCommand>?> ReadCommandsAsync(ShowAgentLease lease, long afterSequence,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_agents.TryGetValue(lease.AgentId, out var agent) || agent.Revoked) return Task.FromResult<IReadOnlyList<ShowAgentCommand>?>(null);
            var acknowledged = Math.Max(afterSequence, agent.CommandStartSequence);
            var commands = _commands.GetValueOrDefault(Scope(lease.WorkspaceId, lease.SessionCode));
            if (commands is null) return Task.FromResult<IReadOnlyList<ShowAgentCommand>?>([]);
            commands.RemoveAll(command => command.Sequence <= acknowledged);
            return Task.FromResult<IReadOnlyList<ShowAgentCommand>?>(commands.Take(100).ToArray());
        }
    }

    (string Token, ShowAgentLease Lease) IssueToken(Agent agent)
    {
        var raw = ShowAgentTokens.NewSecret();
        var expires = _time.GetUtcNow().AddMinutes(5);
        _tokens[ShowAgentTokens.Hash(raw)] = new(agent.Id, expires);
        return (raw, new(agent.Id, agent.WorkspaceId, agent.SessionCode, expires));
    }

    static string Scope(string workspaceId, string sessionCode) => $"{workspaceId}\n{sessionCode}";

    void PruneExpired()
    {
        var now = _time.GetUtcNow();
        foreach (var key in _pairings.Where(item => item.Value.Used || item.Value.ExpiresAt <= now).Select(item => item.Key).ToArray())
            _pairings.Remove(key);
        foreach (var key in _tokens.Where(item => item.Value.ExpiresAt <= now).Select(item => item.Key).ToArray())
            _tokens.Remove(key);
    }

    void RegisterPrefixAttempt(string code)
    {
        var prefix = code.Length >= 2 ? code[..2] : "invalid";
        var now = _time.GetUtcNow();
        var global = _globalAttempts;
        if (global is null || global.StartedAt.AddMinutes(1) <= now) global = new(now, 0);
        if (global.Count >= 100) throw new ShowAgentPairingThrottledException();
        _globalAttempts = global with { Count = global.Count + 1 };
        var window = _attemptsByPrefix.GetValueOrDefault(prefix);
        if (window is null || window.StartedAt.AddMinutes(1) <= now) window = new(now, 0);
        if (window.Count >= 20) throw new ShowAgentPairingThrottledException();
        _attemptsByPrefix[prefix] = window with { Count = window.Count + 1 };
    }
}
