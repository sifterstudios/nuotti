using System.Text.Json;
using Npgsql;

namespace Nuotti.Backend.SessionSnapshots;

public sealed record LyricTrackRevision(string WorkspaceId, string CatalogEntryId, string RevisionId,
    int Version, string Lrc, long OffsetMs, string Sha256, string PublishedBy, DateTimeOffset PublishedAt);
public interface ILyricTrackRevisionStore
{
    Task<LyricTrackRevision> PublishAsync(string workspaceId, string catalogEntryId, string lrc, long offsetMs,
        string userId, CancellationToken cancellationToken = default);
    Task<LyricTrackRevision?> GetAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default);
    Task<LyricTrackRevision?> GetCurrentAsync(string workspaceId, string catalogEntryId,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryLyricTrackRevisionStore(TimeProvider? timeProvider = null) : ILyricTrackRevisionStore
{
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly object _gate = new();
    readonly List<LyricTrackRevision> _revisions = [];
    public Task<LyricTrackRevision> PublishAsync(string workspaceId, string catalogEntryId, string lrc, long offsetMs,
        string userId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var prior = _revisions.Where(x => x.WorkspaceId == workspaceId && x.CatalogEntryId == catalogEntryId).ToArray();
            var sha = Digest(lrc, offsetMs);
            var current = prior.LastOrDefault();
            if (current?.Sha256 == sha) return Task.FromResult(current);
            var revision = new LyricTrackRevision(workspaceId, catalogEntryId, $"lyric_{Guid.NewGuid():N}",
                prior.Length + 1, lrc, offsetMs, sha, userId, _time.GetUtcNow());
            _revisions.Add(revision); return Task.FromResult(revision);
        }
    }
    public Task<LyricTrackRevision?> GetAsync(string workspaceId, string revisionId,
        CancellationToken cancellationToken = default)
    { lock (_gate) return Task.FromResult(_revisions.FirstOrDefault(x => x.WorkspaceId == workspaceId && x.RevisionId == revisionId)); }
    public Task<LyricTrackRevision?> GetCurrentAsync(string workspaceId, string catalogEntryId,
        CancellationToken cancellationToken = default)
    { lock (_gate) return Task.FromResult(_revisions.LastOrDefault(x => x.WorkspaceId == workspaceId && x.CatalogEntryId == catalogEntryId)); }
    internal static string Digest(string lrc, long offsetMs) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes($"{offsetMs}\n{lrc}"))).ToLowerInvariant();
}

public sealed class PostgresLyricTrackRevisionStore(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null)
    : ILyricTrackRevisionStore
{
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    public async Task<LyricTrackRevision> PublishAsync(string workspaceId, string catalogEntryId, string lrc, long offsetMs,
        string userId, CancellationToken cancellationToken = default)
    {
        await EnsureAsync(cancellationToken); var sha = InMemoryLyricTrackRevisionStore.Digest(lrc, offsetMs);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext($1))", connection, transaction);
        lockCommand.Parameters.AddWithValue($"lyric:{workspaceId}:{catalogEntryId}");
        await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        await using var find = new NpgsqlCommand("SELECT id,version,lrc,offset_ms,sha256,published_by,published_at FROM nuotti_lyric_track_revision WHERE workspace_id=$1 AND catalog_entry_id=$2 ORDER BY version DESC LIMIT 1", connection, transaction);
        find.Parameters.AddWithValue(workspaceId); find.Parameters.AddWithValue(catalogEntryId);
        await using (var reader = await find.ExecuteReaderAsync(cancellationToken))
            if (await reader.ReadAsync(cancellationToken) && reader.GetString(4) == sha)
                return new(workspaceId, catalogEntryId, reader.GetString(0), reader.GetInt32(1), reader.GetString(2),
                    reader.GetInt64(3), reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6));
        await using var count = new NpgsqlCommand("SELECT count(*) FROM nuotti_lyric_track_revision WHERE workspace_id=$1 AND catalog_entry_id=$2", connection, transaction);
        count.Parameters.AddWithValue(workspaceId); count.Parameters.AddWithValue(catalogEntryId);
        var version = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken)) + 1;
        var revision = new LyricTrackRevision(workspaceId, catalogEntryId, $"lyric_{Guid.NewGuid():N}", version,
            lrc, offsetMs, sha, userId, _time.GetUtcNow());
        await using var insert = new NpgsqlCommand("INSERT INTO nuotti_lyric_track_revision(workspace_id,catalog_entry_id,id,version,lrc,offset_ms,sha256,published_by,published_at) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)", connection, transaction);
        insert.Parameters.AddWithValue(workspaceId); insert.Parameters.AddWithValue(catalogEntryId); insert.Parameters.AddWithValue(revision.RevisionId);
        insert.Parameters.AddWithValue(version); insert.Parameters.AddWithValue(lrc); insert.Parameters.AddWithValue(offsetMs);
        insert.Parameters.AddWithValue(sha); insert.Parameters.AddWithValue(userId); insert.Parameters.AddWithValue(revision.PublishedAt);
        await insert.ExecuteNonQueryAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return revision;
    }
    public Task<LyricTrackRevision?> GetAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default) =>
        QueryAsync("workspace_id=$1 AND id=$2", workspaceId, revisionId, cancellationToken);
    public Task<LyricTrackRevision?> GetCurrentAsync(string workspaceId, string catalogEntryId, CancellationToken cancellationToken = default) =>
        QueryAsync("workspace_id=$1 AND catalog_entry_id=$2 ORDER BY version DESC", workspaceId, catalogEntryId, cancellationToken);
    async Task<LyricTrackRevision?> QueryAsync(string where, string first, string second, CancellationToken ct, string? third = null)
    {
        await EnsureAsync(ct); await using var command = dataSource.CreateCommand($"SELECT workspace_id,catalog_entry_id,id,version,lrc,offset_ms,sha256,published_by,published_at FROM nuotti_lyric_track_revision WHERE {where} LIMIT 1");
        command.Parameters.AddWithValue(first); command.Parameters.AddWithValue(second); if (third is not null) command.Parameters.AddWithValue(third);
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct)
            ? new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(4), reader.GetInt64(5), reader.GetString(6), reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8)) : null;
    }
    async Task EnsureAsync(CancellationToken ct) { await using var command = dataSource.CreateCommand("CREATE TABLE IF NOT EXISTS nuotti_lyric_track_revision(workspace_id text NOT NULL,catalog_entry_id text NOT NULL,id text NOT NULL,version integer NOT NULL,lrc text NOT NULL,offset_ms bigint NOT NULL,sha256 text NOT NULL,published_by text NOT NULL,published_at timestamptz NOT NULL,PRIMARY KEY(workspace_id,id),UNIQUE(workspace_id,catalog_entry_id,version)); ALTER TABLE nuotti_lyric_track_revision DROP CONSTRAINT IF EXISTS nuotti_lyric_track_revision_workspace_id_catalog_entry_id_sha256_key"); await command.ExecuteNonQueryAsync(ct); }
}

public sealed record ScoringPolicyReference(string PolicyId, int Version);
public interface IScoringPolicyCatalog { ScoringPolicySnapshot? Resolve(ScoringPolicyReference reference); }
public sealed class MvpScoringPolicyCatalog : IScoringPolicyCatalog
{
    static readonly ScoringPolicySnapshot Standard = new("standard", 1, 1000, 500, 10_000);
    public ScoringPolicySnapshot? Resolve(ScoringPolicyReference reference) =>
        reference.PolicyId == Standard.PolicyId && reference.Version == Standard.Version ? Standard : null;
}
