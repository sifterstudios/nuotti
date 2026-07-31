using System.Text.Json;
using Npgsql;

namespace Nuotti.Backend.SessionSnapshots;

public sealed class PostgresSessionSetlistSnapshotStore(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null)
    : ISessionSetlistSnapshotStore
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly SemaphoreSlim _gate = new(1, 1);
    volatile bool _initialized;

    public async Task<SessionSetlistSnapshot?> GetAsync(string workspaceId, string sessionCode,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand(
            "SELECT snapshot::text FROM nuotti_session_setlist_snapshot WHERE workspace_id=$1 AND session_code=$2");
        command.Parameters.AddWithValue(workspaceId); command.Parameters.AddWithValue(sessionCode);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? JsonSerializer.Deserialize<SessionSetlistSnapshot>(json, Json) : null;
    }

    public async Task<SessionSetlistSnapshot> CreateAsync(string workspaceId, string sessionCode,
        IReadOnlyList<SessionSetlistItem> songs, ScoringPolicySnapshot scoringPolicy,
        IReadOnlyList<SnapshotAsset> assets, IReadOnlyList<string> acceptedWarnings, string userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var snapshot = new SessionSetlistSnapshot($"snap_{Guid.NewGuid():N}", workspaceId, sessionCode, 1,
            songs.ToArray(), scoringPolicy, assets.ToArray(), acceptedWarnings.Order(StringComparer.Ordinal).ToArray(),
            userId, _time.GetUtcNow());
        await using var command = dataSource.CreateCommand("""
            INSERT INTO nuotti_session_setlist_snapshot(workspace_id,session_code,snapshot)
            VALUES ($1,$2,$3::jsonb) ON CONFLICT(workspace_id,session_code) DO NOTHING
            """);
        command.Parameters.AddWithValue(workspaceId); command.Parameters.AddWithValue(sessionCode);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(snapshot, Json));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Session Setlist Snapshot already exists.");
        return snapshot;
    }

    async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var command = dataSource.CreateCommand("""
                CREATE TABLE IF NOT EXISTS nuotti_session_setlist_snapshot(
                    workspace_id text NOT NULL, session_code text NOT NULL, snapshot jsonb NOT NULL,
                    created_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(workspace_id,session_code));
                """);
            await command.ExecuteNonQueryAsync(ct); _initialized = true;
        }
        finally { _gate.Release(); }
    }
}
