using System.Text.Json;
using Npgsql;

namespace Nuotti.Backend.SongPackages;

public sealed class PostgresSongPackageStore(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null)
    : ISongPackageStore
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly SemaphoreSlim _gate = new(1, 1);
    volatile bool _initialized;

    public async Task<SongPackageDraft> SaveDraftAsync(string workspaceId, string catalogEntryId,
        SongPackageDocument document, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var draft = new SongPackageDraft(workspaceId, catalogEntryId, document, userId, _time.GetUtcNow());
        await using var command = dataSource.CreateCommand("""
            INSERT INTO nuotti_song_package(workspace_id,catalog_entry_id,draft,updated_by,updated_at,current_revision)
            VALUES ($1,$2,$3::jsonb,$4,$5,0)
            ON CONFLICT(workspace_id,catalog_entry_id) DO UPDATE SET
                draft=EXCLUDED.draft,updated_by=EXCLUDED.updated_by,updated_at=EXCLUDED.updated_at
            """);
        command.Parameters.AddWithValue(workspaceId); command.Parameters.AddWithValue(catalogEntryId);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(document, Json)); command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(draft.UpdatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return draft;
    }

    public async Task<SongPackageDraft?> GetDraftAsync(string workspaceId, string catalogEntryId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            SELECT draft::text,updated_by,updated_at FROM nuotti_song_package
            WHERE workspace_id=$1 AND catalog_entry_id=$2
            """);
        command.Parameters.AddWithValue(workspaceId); command.Parameters.AddWithValue(catalogEntryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(workspaceId, catalogEntryId, JsonSerializer.Deserialize<SongPackageDocument>(reader.GetString(0), Json)!,
                reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2)) : null;
    }

    public async Task<SongPackageRevision> PublishAsync(string workspaceId, string catalogEntryId,
        SongPackageDocument document, string revisionNote, IReadOnlyList<string> acceptedWarningCodes, string userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var next = new NpgsqlCommand("""
            UPDATE nuotti_song_package SET current_revision=current_revision+1
            WHERE workspace_id=$1 AND catalog_entry_id=$2 RETURNING current_revision
            """, connection, transaction);
        next.Parameters.AddWithValue(workspaceId); next.Parameters.AddWithValue(catalogEntryId);
        var number = (int)(await next.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Song Package draft does not exist."));
        var revision = new SongPackageRevision(workspaceId, catalogEntryId, $"pkg_{Guid.NewGuid():N}", number,
            document, revisionNote.Trim(), userId, _time.GetUtcNow(), acceptedWarningCodes.Order(StringComparer.Ordinal).ToArray());
        await using var insert = new NpgsqlCommand("""
            INSERT INTO nuotti_song_package_revision(
                workspace_id,catalog_entry_id,id,revision_number,document,revision_note,published_by,published_at,accepted_warnings)
            VALUES ($1,$2,$3,$4,$5::jsonb,$6,$7,$8,$9::jsonb)
            """, connection, transaction);
        insert.Parameters.AddWithValue(workspaceId); insert.Parameters.AddWithValue(catalogEntryId);
        insert.Parameters.AddWithValue(revision.RevisionId); insert.Parameters.AddWithValue(number);
        insert.Parameters.AddWithValue(JsonSerializer.Serialize(document, Json)); insert.Parameters.AddWithValue(revision.RevisionNote);
        insert.Parameters.AddWithValue(userId); insert.Parameters.AddWithValue(revision.PublishedAt);
        insert.Parameters.AddWithValue(JsonSerializer.Serialize(revision.AcceptedWarningCodes, Json));
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return revision;
    }

    public async Task<IReadOnlyList<SongPackageRevision>> GetRevisionsAsync(string workspaceId, string catalogEntryId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            SELECT id,revision_number,document::text,revision_note,published_by,published_at,accepted_warnings::text
            FROM nuotti_song_package_revision WHERE workspace_id=$1 AND catalog_entry_id=$2 ORDER BY revision_number
            """);
        command.Parameters.AddWithValue(workspaceId); command.Parameters.AddWithValue(catalogEntryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<SongPackageRevision>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(workspaceId, catalogEntryId, reader.GetString(0),
            reader.GetInt32(1), JsonSerializer.Deserialize<SongPackageDocument>(reader.GetString(2), Json)!, reader.GetString(3),
            reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5),
            JsonSerializer.Deserialize<string[]>(reader.GetString(6), Json) ?? []));
        return result;
    }

    async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var command = dataSource.CreateCommand("""
                CREATE TABLE IF NOT EXISTS nuotti_song_package(
                    workspace_id text NOT NULL, catalog_entry_id text NOT NULL, draft jsonb NOT NULL,
                    updated_by text NOT NULL, updated_at timestamptz NOT NULL, current_revision integer NOT NULL DEFAULT 0,
                    PRIMARY KEY(workspace_id,catalog_entry_id), FOREIGN KEY(workspace_id,catalog_entry_id)
                    REFERENCES nuotti_private_catalog_entry(workspace_id,id));
                CREATE TABLE IF NOT EXISTS nuotti_song_package_revision(
                    workspace_id text NOT NULL, catalog_entry_id text NOT NULL, id text NOT NULL,
                    revision_number integer NOT NULL, document jsonb NOT NULL, revision_note text NOT NULL,
                    published_by text NOT NULL, published_at timestamptz NOT NULL, accepted_warnings jsonb NOT NULL,
                    PRIMARY KEY(workspace_id,id), UNIQUE(workspace_id,catalog_entry_id,revision_number),
                    FOREIGN KEY(workspace_id,catalog_entry_id) REFERENCES nuotti_song_package(workspace_id,catalog_entry_id));
                """);
            await command.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally { _gate.Release(); }
    }
}
