using System.Text.Json;
using Npgsql;

namespace Nuotti.Backend.ShowAgents;

/// <summary>Durable, replica-safe Show Agent pairing and lease store.</summary>
public sealed class PostgresShowAgentAccessStore(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null)
    : IShowAgentAccessStore
{
    readonly SemaphoreSlim _initializeGate = new(1, 1);
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    volatile bool _initialized;

    public Task<ShowAgentPairingCode> IssuePairingCodeAsync(string workspaceId, string sessionCode, string issuedBy,
        CancellationToken cancellationToken = default) => MutateAsync(state =>
    {
        string code;
        do code = ShowAgentTokens.PairingCode(); while (state.Pairings.ContainsKey(ShowAgentTokens.Hash(code)));
        var expires = _time.GetUtcNow().AddMinutes(10);
        state.Pairings[ShowAgentTokens.Hash(code)] = new ShowAgentPairingDocument
        { WorkspaceId = workspaceId, SessionCode = sessionCode, IssuedBy = issuedBy, ExpiresAt = expires };
        return new ShowAgentPairingCode(code, expires);
    }, cancellationToken);

    public Task<PairedShowAgent?> PairAsync(string code, string name, CancellationToken cancellationToken = default) =>
        MutateAsync<PairedShowAgent?>(state =>
        {
            RegisterPrefixAttempt(state, code);
            var hash = ShowAgentTokens.Hash(code);
            if (!state.Pairings.TryGetValue(hash, out var pairing) || pairing.Used || pairing.ExpiresAt <= _time.GetUtcNow())
                return null;
            pairing.Used = true;
            var credential = ShowAgentTokens.NewSecret();
            var scope = Scope(pairing.WorkspaceId, pairing.SessionCode);
            var agent = new ShowAgentDocument
            {
                Id = $"agent_{Guid.NewGuid():N}",
                Name = name.Trim(),
                WorkspaceId = pairing.WorkspaceId,
                SessionCode = pairing.SessionCode,
                CredentialHash = ShowAgentTokens.Hash(credential),
                CommandStartSequence = state.LastSequenceByScope.GetValueOrDefault(scope)
            };
            state.Agents[agent.Id] = agent;
            if (!state.AgentByScope.TryGetValue(scope, out var ids)) state.AgentByScope[scope] = ids = [];
            ids.Add(agent.Id);
            var (token, lease) = IssueToken(state, agent);
            return new PairedShowAgent(agent.Id, credential, token, lease.ExpiresAt);
        }, cancellationToken);

    public Task<(string Token, ShowAgentLease Lease)?> IssueAccessTokenAsync(string credential,
        CancellationToken cancellationToken = default) => MutateAsync<(string, ShowAgentLease)?>(state =>
    {
        var hash = ShowAgentTokens.Hash(credential);
        var agent = state.Agents.Values.FirstOrDefault(candidate => candidate.CredentialHash == hash && !candidate.Revoked);
        return agent is null ? null : IssueToken(state, agent);
    }, cancellationToken);

    public Task<ShowAgentLease?> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default) =>
        ReadAsync<ShowAgentLease?>(state =>
        {
            if (!state.Tokens.TryGetValue(ShowAgentTokens.Hash(accessToken), out var token)
                || token.ExpiresAt <= _time.GetUtcNow() || !state.Agents.TryGetValue(token.AgentId, out var agent)
                || agent.Revoked) return null;
            return new(agent.Id, agent.WorkspaceId, agent.SessionCode, token.ExpiresAt);
        }, cancellationToken);

    public Task<bool> ReportStatusAsync(ShowAgentLease lease, ShowAgentConnectionState stateValue, string? detail,
        CancellationToken cancellationToken = default) => MutateAsync(state =>
    {
        if (!state.Agents.TryGetValue(lease.AgentId, out var agent) || agent.Revoked) return false;
        agent.State = stateValue; agent.Detail = detail; agent.LastSeenAt = _time.GetUtcNow();
        return true;
    }, cancellationToken);

    public Task<IReadOnlyList<ShowAgentStatus>> ListStatusesAsync(string workspaceId, string sessionCode,
        CancellationToken cancellationToken = default) => ReadAsync<IReadOnlyList<ShowAgentStatus>>(state =>
    {
        if (!state.AgentByScope.TryGetValue(Scope(workspaceId, sessionCode), out var ids) || ids.Count == 0)
            return Array.Empty<ShowAgentStatus>();
        return ids.Select(id =>
        {
            var agent = state.Agents[id];
            return new ShowAgentStatus(agent.Id, agent.Name, workspaceId, sessionCode,
                agent.State, agent.Detail, agent.LastSeenAt, agent.Revoked);
        }).ToArray();
    }, cancellationToken);

    public Task<bool> RevokeAsync(string workspaceId, string sessionCode, CancellationToken cancellationToken = default) =>
        MutateAsync(state =>
        {
            if (!state.AgentByScope.TryGetValue(Scope(workspaceId, sessionCode), out var ids))
                return false;
            var revokedAny = false;
            foreach (var id in ids)
            {
                if (state.Agents[id].Revoked) continue;
                state.Agents[id].Revoked = true;
                revokedAny = true;
            }
            return revokedAny;
        }, cancellationToken);

    public Task AppendCommandAsync(string workspaceId, string sessionCode, string messageType, object payload,
        CancellationToken cancellationToken = default) => MutateAsync<object?>(state =>
    {
        var scope = Scope(workspaceId, sessionCode);
        var commands = state.Commands.GetValueOrDefault(scope);
        if (commands is null) state.Commands[scope] = commands = [];
        var sequence = state.LastSequenceByScope.GetValueOrDefault(scope) + 1;
        state.LastSequenceByScope[scope] = sequence;
        commands.Add(new ShowAgentCommandDocument
        {
            Sequence = sequence,
            MessageType = messageType,
            Payload = JsonSerializer.SerializeToElement(payload, payload.GetType())
        });
        // Compact offline playback to the latest desired Play/Stop state.
        // Prepare and other control messages must survive until acknowledged.
        if (messageType is "PlayTrack" or "StopTrack")
            commands.RemoveAll(c => c.MessageType is "PlayTrack" or "StopTrack" && c.Sequence < sequence);
        return null;
    }, cancellationToken);

    public Task<IReadOnlyList<ShowAgentCommand>?> ReadCommandsAsync(ShowAgentLease lease, long afterSequence,
        CancellationToken cancellationToken = default) => MutateAsync<IReadOnlyList<ShowAgentCommand>?>(state =>
    {
        if (!state.Agents.TryGetValue(lease.AgentId, out var agent) || agent.Revoked) return null;
        var acknowledged = Math.Max(afterSequence, agent.CommandStartSequence);
        var commands = state.Commands.GetValueOrDefault(Scope(lease.WorkspaceId, lease.SessionCode));
        if (commands is null) return [];
        commands.RemoveAll(command => command.Sequence <= acknowledged);
        return commands.Take(100).Select(command =>
            new ShowAgentCommand(command.Sequence, command.MessageType, command.Payload)).ToArray();
    }, cancellationToken);

    (string Token, ShowAgentLease Lease) IssueToken(ShowAgentAccessDocument state, ShowAgentDocument agent)
    {
        var raw = ShowAgentTokens.NewSecret();
        var expires = _time.GetUtcNow().AddMinutes(5);
        state.Tokens[ShowAgentTokens.Hash(raw)] = new ShowAgentTokenDocument { AgentId = agent.Id, ExpiresAt = expires };
        return (raw, new(agent.Id, agent.WorkspaceId, agent.SessionCode, expires));
    }

    async Task<T> ReadAsync<T>(Func<ShowAgentAccessDocument, T> read, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var command = dataSource.CreateCommand("SELECT state::text FROM nuotti_show_agent_access WHERE singleton=true");
        return read(JsonSerializer.Deserialize<ShowAgentAccessDocument>((string)(await command.ExecuteScalarAsync(ct))!)!);
    }

    async Task<T> MutateAsync<T>(Func<ShowAgentAccessDocument, T> mutate, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var load = new NpgsqlCommand(
            "SELECT state::text FROM nuotti_show_agent_access WHERE singleton=true FOR UPDATE", connection, transaction);
        var state = JsonSerializer.Deserialize<ShowAgentAccessDocument>((string)(await load.ExecuteScalarAsync(ct))!)!;
        Prune(state);
        var result = mutate(state);
        await using var save = new NpgsqlCommand(
            "UPDATE nuotti_show_agent_access SET state=$1::jsonb, updated_at=now() WHERE singleton=true", connection, transaction);
        save.Parameters.AddWithValue(JsonSerializer.Serialize(state));
        await save.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initializeGate.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var command = dataSource.CreateCommand("""
                CREATE TABLE IF NOT EXISTS nuotti_show_agent_access (
                    singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
                    state jsonb NOT NULL,
                    updated_at timestamptz NOT NULL DEFAULT now());
                INSERT INTO nuotti_show_agent_access(singleton, state)
                VALUES (true, '{"Pairings":{},"Agents":{},"AgentByScope":{},"Tokens":{},"Commands":{}}'::jsonb)
                ON CONFLICT (singleton) DO NOTHING;
                """);
            await command.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally { _initializeGate.Release(); }
    }

    static string Scope(string workspaceId, string sessionCode) => $"{workspaceId}\n{sessionCode}";

    void Prune(ShowAgentAccessDocument state)
    {
        var now = _time.GetUtcNow();
        foreach (var key in state.Pairings.Where(item => item.Value.Used || item.Value.ExpiresAt <= now).Select(item => item.Key).ToArray())
            state.Pairings.Remove(key);
        foreach (var key in state.Tokens.Where(item => item.Value.ExpiresAt <= now).Select(item => item.Key).ToArray())
            state.Tokens.Remove(key);
        foreach (var key in state.AttemptsByPrefix.Where(item => item.Value.StartedAt.AddMinutes(1) <= now).Select(item => item.Key).ToArray())
            state.AttemptsByPrefix.Remove(key);
    }

    void RegisterPrefixAttempt(ShowAgentAccessDocument state, string code)
    {
        var prefix = code.Length >= 2 ? code[..2] : "invalid";
        var now = _time.GetUtcNow();
        var global = state.GlobalAttempts;
        if (global is null || global.StartedAt.AddMinutes(1) <= now)
            global = new ShowAgentAttemptDocument { StartedAt = now };
        if (global.Count >= 100) throw new ShowAgentPairingThrottledException();
        global.Count++;
        state.GlobalAttempts = global;
        var window = state.AttemptsByPrefix.GetValueOrDefault(prefix);
        if (window is null || window.StartedAt.AddMinutes(1) <= now)
            window = new ShowAgentAttemptDocument { StartedAt = now };
        if (window.Count >= 20) throw new ShowAgentPairingThrottledException();
        window.Count++;
        state.AttemptsByPrefix[prefix] = window;
    }
}

internal sealed class ShowAgentAccessDocument
{
    public Dictionary<string, ShowAgentPairingDocument> Pairings { get; set; } = [];
    public Dictionary<string, ShowAgentDocument> Agents { get; set; } = [];
    /// <summary>Agent ids for each workspace/session scope. Values are lists so Projector and Engine can both stay paired.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(AgentByScopeConverter))]
    public Dictionary<string, List<string>> AgentByScope { get; set; } = [];
    public Dictionary<string, ShowAgentTokenDocument> Tokens { get; set; } = [];
    public Dictionary<string, List<ShowAgentCommandDocument>> Commands { get; set; } = [];
    public Dictionary<string, long> LastSequenceByScope { get; set; } = [];
    public Dictionary<string, ShowAgentAttemptDocument> AttemptsByPrefix { get; set; } = [];
    public ShowAgentAttemptDocument? GlobalAttempts { get; set; }
}
internal sealed class ShowAgentPairingDocument
{
    public string WorkspaceId { get; set; } = ""; public string SessionCode { get; set; } = "";
    public string IssuedBy { get; set; } = ""; public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
}
internal sealed class ShowAgentDocument
{
    public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string WorkspaceId { get; set; } = "";
    public string SessionCode { get; set; } = ""; public string CredentialHash { get; set; } = ""; public bool Revoked { get; set; }
    public long CommandStartSequence { get; set; }
    public ShowAgentConnectionState State { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
}
internal sealed class ShowAgentTokenDocument { public string AgentId { get; set; } = ""; public DateTimeOffset ExpiresAt { get; set; } }
internal sealed class ShowAgentCommandDocument
{
    public long Sequence { get; set; }
    public string MessageType { get; set; } = ""; public JsonElement Payload { get; set; }
}
internal sealed class ShowAgentAttemptDocument { public DateTimeOffset StartedAt { get; set; } public int Count { get; set; } }

/// <summary>
/// Accepts the legacy single-id-per-scope shape <c>"scope":"agent_id"</c> and the multi-agent
/// shape <c>"scope":["agent_a","agent_b"]</c> so an existing Postgres blob keeps loading.
/// </summary>
internal sealed class AgentByScopeConverter : System.Text.Json.Serialization.JsonConverter<Dictionary<string, List<string>>>
{
    public override Dictionary<string, List<string>> Read(
        ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new Dictionary<string, List<string>>();
        if (reader.TokenType != System.Text.Json.JsonTokenType.StartObject)
            throw new JsonException("AgentByScope must be an object.");
        while (reader.Read() && reader.TokenType != System.Text.Json.JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            reader.Read();
            if (reader.TokenType == System.Text.Json.JsonTokenType.String)
                result[key] = [reader.GetString()!];
            else if (reader.TokenType == System.Text.Json.JsonTokenType.StartArray)
            {
                var ids = new List<string>();
                while (reader.Read() && reader.TokenType != System.Text.Json.JsonTokenType.EndArray)
                    ids.Add(reader.GetString()!);
                result[key] = ids;
            }
            else throw new JsonException("AgentByScope values must be a string or string array.");
        }
        return result;
    }

    public override void Write(
        System.Text.Json.Utf8JsonWriter writer, Dictionary<string, List<string>> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, ids) in value)
        {
            writer.WritePropertyName(key);
            writer.WriteStartArray();
            foreach (var id in ids) writer.WriteStringValue(id);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}
