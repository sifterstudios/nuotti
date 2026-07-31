using System.Text.Json;
using Npgsql;
using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.Backend.Persistence;

/// <summary>PostgreSQL adapter for the atomic Session state/outcome/event/outbox transaction.</summary>
public sealed class PostgresDurableSessionCommitStore(NpgsqlDataSource dataSource) : IDurableSessionCommitStore
{
    readonly SemaphoreSlim _initializeGate = new(1, 1);
    volatile bool _initialized;

    async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initializeGate.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var command = dataSource.CreateCommand("""
                CREATE TABLE IF NOT EXISTS nuotti_session_state (
                    workspace_id text NOT NULL,
                    session_code text NOT NULL,
                    snapshot jsonb NOT NULL,
                    last_sequence bigint NOT NULL,
                    control_generation bigint NOT NULL DEFAULT 0,
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    PRIMARY KEY (workspace_id, session_code));
                CREATE TABLE IF NOT EXISTS nuotti_command_outcome (
                    workspace_id text NOT NULL,
                    session_code text NOT NULL,
                    command_id uuid NOT NULL,
                    outcome text NOT NULL,
                    state jsonb NULL,
                    problem jsonb NULL,
                    PRIMARY KEY (workspace_id, session_code, command_id));
                CREATE TABLE IF NOT EXISTS nuotti_session_event (
                    workspace_id text NOT NULL,
                    session_code text NOT NULL,
                    sequence bigint NOT NULL,
                    message_type text NOT NULL,
                    payload jsonb NOT NULL,
                    PRIMARY KEY (workspace_id, session_code, sequence));
                CREATE TABLE IF NOT EXISTS nuotti_outbox (
                    workspace_id text NOT NULL,
                    session_code text NOT NULL,
                    sequence bigint NOT NULL,
                    message_type text NOT NULL,
                    payload jsonb NOT NULL,
                    delivered_at timestamptz NULL,
                    claim_owner uuid NULL,
                    claim_until timestamptz NULL,
                    PRIMARY KEY (workspace_id, session_code, sequence));
                CREATE INDEX IF NOT EXISTS ix_nuotti_outbox_pending
                    ON nuotti_outbox (workspace_id, session_code, sequence) WHERE delivered_at IS NULL;
                """);
            await command.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally { _initializeGate.Release(); }
    }

    public async Task<DurableSessionRecord?> LoadAsync(string workspaceId, string sessionCode, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand(
            "SELECT snapshot::text, last_sequence, control_generation FROM nuotti_session_state WHERE workspace_id=$1 AND session_code=$2");
        command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(sessionCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new DurableSessionRecord(workspaceId,
            JsonSerializer.Deserialize<GameStateSnapshot>(reader.GetString(0), ContractsJson.RestOptions)!,
            new SessionSequence(reader.GetInt64(1)),
            new ControlGeneration(reader.GetInt64(2)));
    }

    public async Task<CommandResult?> FindOutcomeAsync(string workspaceId, string sessionCode, Guid commandId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand(
            "SELECT outcome, state::text, problem::text FROM nuotti_command_outcome WHERE workspace_id=$1 AND session_code=$2 AND command_id=$3");
        command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(sessionCode);
        command.Parameters.AddWithValue(commandId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadResult(reader) : null;
    }

    public async Task<DurableCommit> CommitAsync(
        string workspaceId, string sessionCode, Guid commandId, SessionSequence expectedSequence,
        GameStateSnapshot snapshot, IReadOnlyList<object> messages,
        DurableCommitPrecondition precondition = DurableCommitPrecondition.Any,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Serialize all commits for one Session, including concurrent first writes where no state
        // row exists yet. This makes the outcome lookup and sequence allocation one critical section.
        await using (var sessionLock = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended($1 || ':' || $2, 0))", connection, transaction))
        {
            sessionLock.Parameters.AddWithValue(workspaceId);
            sessionLock.Parameters.AddWithValue(sessionCode);
            await sessionLock.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var prior = new NpgsqlCommand(
            "SELECT outcome, state::text, problem::text FROM nuotti_command_outcome WHERE workspace_id=$1 AND session_code=$2 AND command_id=$3",
            connection, transaction))
        {
            prior.Parameters.AddWithValue(workspaceId);
            prior.Parameters.AddWithValue(sessionCode);
            prior.Parameters.AddWithValue(commandId);
            await using var reader = await prior.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var result = ReadResult(reader);
                await reader.DisposeAsync();
                await transaction.RollbackAsync(cancellationToken);
                return new DurableCommit(result, [], WasDuplicate: true);
            }
        }

        long lastSequence;
        var exists = false;
        await using (var current = new NpgsqlCommand(
            "SELECT last_sequence FROM nuotti_session_state WHERE workspace_id=$1 AND session_code=$2", connection, transaction))
        {
            current.Parameters.AddWithValue(workspaceId);
            current.Parameters.AddWithValue(sessionCode);
            var value = await current.ExecuteScalarAsync(cancellationToken);
            exists = value is not null;
            lastSequence = exists ? Convert.ToInt64(value) : 0;
        }
        if (precondition == DurableCommitPrecondition.SessionMustNotExist && exists)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new DurableCommit(CommandResult.Rejected(NuottiProblem.Conflict(
                "Session already exists", $"Session '{sessionCode}' has already been created.",
                Nuotti.Contracts.V1.Enum.ReasonCode.InvalidStateTransition)), [], false);
        }
        if (lastSequence != expectedSequence.Value)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new DurableCommit(CommandResult.Duplicate(), [], false, WasStale: true);
        }
        await using (var lockState = new NpgsqlCommand("""
            INSERT INTO nuotti_session_state(workspace_id, session_code, snapshot, last_sequence, control_generation)
            VALUES ($1, $2, $3::jsonb, 0, 0) ON CONFLICT (workspace_id, session_code) DO NOTHING;
            """, connection, transaction))
        {
            lockState.Parameters.AddWithValue(workspaceId);
            lockState.Parameters.AddWithValue(sessionCode);
            lockState.Parameters.AddWithValue(JsonSerializer.Serialize(snapshot, ContractsJson.RestOptions));
            await lockState.ExecuteNonQueryAsync(cancellationToken);
        }

        var committed = new List<DurableOutboxMessage>(messages.Count);
        foreach (var message in messages)
        {
            lastSequence = checked(lastSequence + 1);
            var serialized = SessionMessagePublisher.SerializeDurable(message);
            await using var append = new NpgsqlCommand("""
                INSERT INTO nuotti_session_event(workspace_id, session_code, sequence, message_type, payload)
                VALUES ($1, $2, $3, $4, $5::jsonb);
                INSERT INTO nuotti_outbox(workspace_id, session_code, sequence, message_type, payload)
                VALUES ($1, $2, $3, $4, $5::jsonb);
                """, connection, transaction);
            append.Parameters.AddWithValue(workspaceId);
            append.Parameters.AddWithValue(sessionCode);
            append.Parameters.AddWithValue(lastSequence);
            append.Parameters.AddWithValue(serialized.Type);
            append.Parameters.AddWithValue(serialized.Payload);
            await append.ExecuteNonQueryAsync(cancellationToken);
            committed.Add(new DurableOutboxMessage(workspaceId, sessionCode, new SessionSequence(lastSequence), serialized.Type, serialized.Payload));
        }

        var stateJson = JsonSerializer.Serialize(snapshot, ContractsJson.RestOptions);
        await using (var finish = new NpgsqlCommand("""
            UPDATE nuotti_session_state SET snapshot=$3::jsonb, last_sequence=$4, updated_at=now()
            WHERE workspace_id=$1 AND session_code=$2;
            INSERT INTO nuotti_command_outcome(workspace_id, session_code, command_id, outcome, state)
            VALUES ($1, $2, $5, 'Applied', $3::jsonb);
            """, connection, transaction))
        {
            finish.Parameters.AddWithValue(workspaceId);
            finish.Parameters.AddWithValue(sessionCode);
            finish.Parameters.AddWithValue(stateJson);
            finish.Parameters.AddWithValue(lastSequence);
            finish.Parameters.AddWithValue(commandId);
            await finish.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new DurableCommit(CommandResult.Applied(snapshot), committed, WasDuplicate: false);
    }

    public async Task<IReadOnlyList<DurableOutboxMessage>> ClaimPendingAsync(
        Guid owner, TimeSpan lease, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            WITH candidates AS (
                SELECT candidate.workspace_id, candidate.session_code, candidate.sequence
                FROM nuotti_outbox candidate
                WHERE candidate.delivered_at IS NULL
                  AND (candidate.claim_until IS NULL OR candidate.claim_until < now())
                  AND NOT EXISTS (
                      SELECT 1 FROM nuotti_outbox earlier
                      WHERE earlier.workspace_id=candidate.workspace_id
                        AND earlier.session_code=candidate.session_code
                        AND earlier.delivered_at IS NULL
                        AND earlier.sequence < candidate.sequence)
                ORDER BY candidate.workspace_id, candidate.session_code, candidate.sequence
                FOR UPDATE SKIP LOCKED LIMIT $1)
            UPDATE nuotti_outbox target
            SET claim_owner=$2, claim_until=now()+$3
            FROM candidates
            WHERE target.workspace_id=candidates.workspace_id
              AND target.session_code=candidates.session_code
              AND target.sequence=candidates.sequence
            RETURNING target.workspace_id, target.session_code, target.sequence,
                      target.message_type, target.payload::text
            """);
        command.Parameters.AddWithValue(limit);
        command.Parameters.AddWithValue(owner);
        command.Parameters.AddWithValue(lease);
        return await ReadMessagesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<DurableOutboxMessage>> ReadAfterAsync(
        string workspaceId, string sessionCode, SessionSequence cursor, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            SELECT workspace_id, session_code, sequence, message_type, payload::text FROM nuotti_session_event
            WHERE workspace_id=$1 AND session_code=$2 AND sequence>$3 ORDER BY sequence
            """);
        command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(sessionCode);
        command.Parameters.AddWithValue(cursor.Value);
        return await ReadMessagesAsync(command, cancellationToken);
    }

    public async Task MarkDeliveredAsync(DurableOutboxMessage message, Guid owner, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            UPDATE nuotti_outbox SET delivered_at=COALESCE(delivered_at, now()), claim_until=NULL
            WHERE workspace_id=$1 AND session_code=$2 AND sequence=$3 AND claim_owner=$4
            """);
        command.Parameters.AddWithValue(message.WorkspaceId);
        command.Parameters.AddWithValue(message.SessionCode);
        command.Parameters.AddWithValue(message.Sequence.Value);
        command.Parameters.AddWithValue(owner);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    static CommandResult ReadResult(NpgsqlDataReader reader)
    {
        var outcome = System.Enum.Parse<Outcome>(reader.GetString(0));
        var state = reader.IsDBNull(1) ? null : JsonSerializer.Deserialize<GameStateSnapshot>(reader.GetString(1), ContractsJson.RestOptions);
        var problem = reader.IsDBNull(2) ? null : JsonSerializer.Deserialize<NuottiProblem>(reader.GetString(2), ContractsJson.RestOptions);
        return new CommandResult(outcome, state, problem);
    }

    static async Task<IReadOnlyList<DurableOutboxMessage>> ReadMessagesAsync(NpgsqlCommand command, CancellationToken ct)
    {
        var messages = new List<DurableOutboxMessage>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            messages.Add(new DurableOutboxMessage(reader.GetString(0), reader.GetString(1),
                new SessionSequence(reader.GetInt64(2)), reader.GetString(3), reader.GetString(4)));
        return messages;
    }
}
