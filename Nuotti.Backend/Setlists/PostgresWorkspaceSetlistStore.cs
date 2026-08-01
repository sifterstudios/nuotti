using System.Text.Json;
using Npgsql;

namespace Nuotti.Backend.Setlists;

public sealed class PostgresWorkspaceSetlistStore(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null)
    : IWorkspaceSetlistStore
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    volatile bool _initialized;

    public async Task<WorkspaceSetlist?> GetAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            SELECT songs::text, updated_at, updated_by FROM nuotti_workspace_setlist WHERE workspace_id=$1
            """);
        command.Parameters.AddWithValue(workspaceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var songs = JsonSerializer.Deserialize<SetlistSongSelection[]>(reader.GetString(0), Json) ?? [];
        return new WorkspaceSetlist(workspaceId, songs, reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2));
    }

    public async Task<WorkspaceSetlist> SaveAsync(string workspaceId, IReadOnlyList<SetlistSongSelection> songs, string userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var setlist = new WorkspaceSetlist(workspaceId, songs.ToArray(), _time.GetUtcNow(), userId);
        await using var command = dataSource.CreateCommand("""
            INSERT INTO nuotti_workspace_setlist(workspace_id, songs, updated_at, updated_by)
            VALUES ($1, $2::jsonb, $3, $4)
            ON CONFLICT (workspace_id) DO UPDATE
            SET songs=EXCLUDED.songs, updated_at=EXCLUDED.updated_at, updated_by=EXCLUDED.updated_by
            """);
        command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(setlist.Songs, Json));
        command.Parameters.AddWithValue(setlist.UpdatedAt);
        command.Parameters.AddWithValue(userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return setlist;
    }

    async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var command = dataSource.CreateCommand("""
                CREATE TABLE IF NOT EXISTS nuotti_workspace_setlist (
                    workspace_id text PRIMARY KEY,
                    songs jsonb NOT NULL,
                    updated_at timestamptz NOT NULL,
                    updated_by text NOT NULL);
                """);
            await command.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally { _gate.Release(); }
    }
}
