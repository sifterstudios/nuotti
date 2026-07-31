using System.Text.Json;
using Npgsql;

namespace Nuotti.Backend.Assets;

public sealed class PostgresPrivateAssetMetadataStore(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null)
    : IPrivateAssetMetadataStore
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    volatile bool _initialized;

    public async Task<PrivateCatalogEntry> CreateEntryAsync(string workspaceId, string title, string artist, string userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var entry = new PrivateCatalogEntry($"entry_{Guid.NewGuid():N}", workspaceId, title.Trim(), artist.Trim(), userId, _time.GetUtcNow());
        await using var command = dataSource.CreateCommand("""
            INSERT INTO nuotti_private_catalog_entry(id, workspace_id, title, artist, created_by, created_at)
            VALUES ($1,$2,$3,$4,$5,$6)
            """);
        command.Parameters.AddWithValue(entry.CatalogEntryId); command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(entry.Title); command.Parameters.AddWithValue(entry.Artist);
        command.Parameters.AddWithValue(userId); command.Parameters.AddWithValue(entry.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return entry;
    }

    public async Task<PrivateCatalogEntry?> GetEntryAsync(string workspaceId, string catalogEntryId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            SELECT title,artist,created_by,created_at FROM nuotti_private_catalog_entry
            WHERE workspace_id=$1 AND id=$2
            """);
        command.Parameters.AddWithValue(workspaceId); command.Parameters.AddWithValue(catalogEntryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(catalogEntryId, workspaceId, reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3)) : null;
    }

    public async Task<(PrivateAssetRevision Revision, string ObjectKey)?> CreateDraftAsync(
        string workspaceId, string catalogEntryId, string assetType, string contentType, long declaredSize,
        AssetProvenance provenance, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var exists = new NpgsqlCommand(
            "SELECT 1 FROM nuotti_private_catalog_entry WHERE id=$1 AND workspace_id=$2", connection);
        exists.Parameters.AddWithValue(catalogEntryId); exists.Parameters.AddWithValue(workspaceId);
        if (await exists.ExecuteScalarAsync(cancellationToken) is null) return null;
        var revision = new PrivateAssetRevision($"rev_{Guid.NewGuid():N}", catalogEntryId, workspaceId,
            AssetRevisionStatus.Draft, assetType, contentType, declaredSize, null, null, provenance,
            userId, _time.GetUtcNow(), null, null);
        var key = $"asset_{Guid.NewGuid():N}";
        await using var insert = new NpgsqlCommand("""
            INSERT INTO nuotti_private_asset_revision(
                id,catalog_entry_id,workspace_id,status,asset_type,content_type,declared_size,
                provenance,uploaded_by,created_at,object_key)
            VALUES ($1,$2,$3,'Draft',$4,$5,$6,$7::jsonb,$8,$9,$10)
            """, connection);
        insert.Parameters.AddWithValue(revision.RevisionId); insert.Parameters.AddWithValue(catalogEntryId);
        insert.Parameters.AddWithValue(workspaceId); insert.Parameters.AddWithValue(assetType);
        insert.Parameters.AddWithValue(contentType); insert.Parameters.AddWithValue(declaredSize);
        insert.Parameters.AddWithValue(JsonSerializer.Serialize(provenance)); insert.Parameters.AddWithValue(userId);
        insert.Parameters.AddWithValue(revision.CreatedAt); insert.Parameters.AddWithValue(key);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return (revision, key);
    }

    public async Task<PrivateAssetRevision?> PublishAsync(string workspaceId, string revisionId, string sealedObjectKey,
        string claimToken, long storedSize, string sha256,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            UPDATE nuotti_private_asset_revision SET status='Published', object_key=$3, stored_size=$5, sha256=$6,
                published_at=$7, finalizing_at=NULL, finalization_token=NULL
            WHERE id=$1 AND workspace_id=$2 AND status='Finalizing' AND finalization_token=$4 AND declared_size=$5
            """);
        command.Parameters.AddWithValue(revisionId); command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(sealedObjectKey); command.Parameters.AddWithValue(claimToken);
        command.Parameters.AddWithValue(storedSize); command.Parameters.AddWithValue(sha256.ToLowerInvariant());
        command.Parameters.AddWithValue(_time.GetUtcNow());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? await GetAsync(workspaceId, revisionId, cancellationToken) : null;
    }

    public async Task<PrivateAssetFinalizationClaim?> TryBeginFinalizationAsync(string workspaceId, string revisionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var token = Guid.NewGuid().ToString("N");
        await using var command = dataSource.CreateCommand("""
            UPDATE nuotti_private_asset_revision SET status='Finalizing', finalizing_at=$3, finalization_token=$5
            WHERE id=$1 AND workspace_id=$2
              AND (status='Draft' OR (status='Finalizing' AND finalizing_at <= $4))
            """);
        command.Parameters.AddWithValue(revisionId); command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(_time.GetUtcNow()); command.Parameters.AddWithValue(_time.GetUtcNow().AddMinutes(-10));
        command.Parameters.AddWithValue(token);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return null;
        var revision = await GetAsync(workspaceId, revisionId, cancellationToken);
        return revision is null ? null : new(revision, token);
    }

    public async Task CancelFinalizationAsync(string workspaceId, string revisionId, string claimToken,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            UPDATE nuotti_private_asset_revision SET status='Draft', finalizing_at=NULL, finalization_token=NULL
            WHERE id=$1 AND workspace_id=$2 AND status='Finalizing' AND finalization_token=$3
            """);
        command.Parameters.AddWithValue(revisionId); command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(claimToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PrivateAssetRevision?> GetAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            SELECT catalog_entry_id,status,asset_type,content_type,declared_size,stored_size,sha256,
                   provenance::text,uploaded_by,created_at,published_at,archived_at
            FROM nuotti_private_asset_revision WHERE id=$1 AND workspace_id=$2
            """);
        command.Parameters.AddWithValue(revisionId); command.Parameters.AddWithValue(workspaceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(revisionId, reader.GetString(0), workspaceId, Enum.Parse<AssetRevisionStatus>(reader.GetString(1)),
            reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.IsDBNull(5) ? null : reader.GetInt64(5),
            reader.IsDBNull(6) ? null : reader.GetString(6), JsonSerializer.Deserialize<AssetProvenance>(reader.GetString(7))!,
            reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9),
            reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11));
    }

    public async Task<string?> GetObjectKeyAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand(
            "SELECT object_key FROM nuotti_private_asset_revision WHERE id=$1 AND workspace_id=$2");
        command.Parameters.AddWithValue(revisionId); command.Parameters.AddWithValue(workspaceId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task<PrivateAssetRevision?> ArchiveAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            UPDATE nuotti_private_asset_revision SET status='Archived', archived_at=$3
            WHERE id=$1 AND workspace_id=$2 AND status='Published'
            """);
        command.Parameters.AddWithValue(revisionId); command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(_time.GetUtcNow());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? await GetAsync(workspaceId, revisionId, cancellationToken) : null;
    }

    async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var command = dataSource.CreateCommand("""
                CREATE TABLE IF NOT EXISTS nuotti_private_catalog_entry(
                    id text NOT NULL, workspace_id text NOT NULL, title text NOT NULL, artist text NOT NULL,
                    created_by text NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY(workspace_id,id));
                CREATE INDEX IF NOT EXISTS ix_nuotti_private_catalog_workspace ON nuotti_private_catalog_entry(workspace_id,id);
                CREATE TABLE IF NOT EXISTS nuotti_private_asset_revision(
                    id text NOT NULL, catalog_entry_id text NOT NULL,
                    workspace_id text NOT NULL, status text NOT NULL, asset_type text NOT NULL, content_type text NOT NULL,
                    declared_size bigint NOT NULL, stored_size bigint NULL, sha256 text NULL, provenance jsonb NOT NULL,
                    uploaded_by text NOT NULL, created_at timestamptz NOT NULL, published_at timestamptz NULL,
                    archived_at timestamptz NULL, finalizing_at timestamptz NULL, finalization_token text NULL,
                    object_key text NOT NULL UNIQUE,
                    PRIMARY KEY(workspace_id,id), FOREIGN KEY(workspace_id,catalog_entry_id)
                    REFERENCES nuotti_private_catalog_entry(workspace_id,id));
                ALTER TABLE nuotti_private_asset_revision ADD COLUMN IF NOT EXISTS finalizing_at timestamptz NULL;
                ALTER TABLE nuotti_private_asset_revision ADD COLUMN IF NOT EXISTS finalization_token text NULL;
                CREATE INDEX IF NOT EXISTS ix_nuotti_private_revision_workspace ON nuotti_private_asset_revision(workspace_id,id);
                """);
            await command.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally { _gate.Release(); }
    }
}
