using System.Text.Json;
using Npgsql;

namespace Nuotti.Backend.Workspaces;

/// <summary>
/// Durable Workspace security boundary. The MVP keeps the small identity graph in one locked
/// document so every invitation, redemption, selection, and revocation is atomic across replicas.
/// The interface allows this representation to be normalized later without changing callers.
/// </summary>
public sealed class PostgresWorkspaceAccessStore(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null)
    : IWorkspaceAccessStore
{
    readonly SemaphoreSlim _initializeGate = new(1, 1);
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    volatile bool _initialized;

    public Task<IssuedMagicLink> IssueSignInAsync(string email, CancellationToken cancellationToken = default) =>
        MutateAsync(state => Issue(state, WorkspaceTokens.NormalizeEmail(email), null), cancellationToken);

    public Task<RedeemedMagicLink?> RedeemAsync(string token, CancellationToken cancellationToken = default) =>
        MutateAsync<RedeemedMagicLink?>(state =>
        {
            var hash = WorkspaceTokens.Hash(token);
            if (!state.Links.TryGetValue(hash, out var link) || link.Used || link.ExpiresAt <= _time.GetUtcNow())
                return null;
            link.Used = true;
            var user = GetOrCreateUser(state, link.Email);
            if (link.WorkspaceId is not null && state.Workspaces.TryGetValue(link.WorkspaceId, out var workspace))
                workspace.Memberships.TryAdd(user.Id, WorkspaceRole.Member);
            var rawSession = WorkspaceTokens.New();
            var sessionId = WorkspaceTokens.Hash(rawSession);
            // Membership and active context are separate decisions. Redemption never selects.
            state.Sessions[sessionId] = new WorkspaceSessionState { UserId = user.Id };
            return new RedeemedMagicLink(rawSession,
                new WorkspacePrincipal(user.Id, user.Email, null, sessionId));
        }, cancellationToken);

    public Task<WorkspacePrincipal?> AuthenticateAsync(string sessionToken, CancellationToken cancellationToken = default) =>
        ReadAsync<WorkspacePrincipal?>(state =>
        {
            var sessionId = WorkspaceTokens.Hash(sessionToken);
            return state.Sessions.TryGetValue(sessionId, out var session)
                   && state.UsersById.TryGetValue(session.UserId, out var user)
                ? new WorkspacePrincipal(user.Id, user.Email, session.SelectedWorkspaceId, sessionId)
                : null;
        }, cancellationToken);

    public Task<WorkspaceAccess> CreateWorkspaceAsync(WorkspacePrincipal principal, string name, CancellationToken cancellationToken = default) =>
        MutateAsync(state =>
        {
            var id = $"ws_{Guid.NewGuid():N}";
            var workspace = new WorkspaceDocument { Id = id, Name = name.Trim() };
            workspace.Memberships[principal.UserId] = WorkspaceRole.Owner;
            state.Workspaces[id] = workspace;
            return new WorkspaceAccess(id, workspace.Name, WorkspaceRole.Owner);
        }, cancellationToken);

    public Task<IReadOnlyList<WorkspaceAccess>> ListAsync(WorkspacePrincipal principal, CancellationToken cancellationToken = default) =>
        ReadAsync<IReadOnlyList<WorkspaceAccess>>(state => state.Workspaces.Values
            .Where(workspace => workspace.Memberships.ContainsKey(principal.UserId))
            .Select(workspace => new WorkspaceAccess(workspace.Id, workspace.Name, workspace.Memberships[principal.UserId]))
            .OrderBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase).ToArray(), cancellationToken);

    public Task<WorkspacePrincipal?> SelectAsync(WorkspacePrincipal principal, string workspaceId, CancellationToken cancellationToken = default) =>
        MutateAsync<WorkspacePrincipal?>(state =>
        {
            if (principal.AuthenticationSessionId is null
                || !state.Sessions.TryGetValue(principal.AuthenticationSessionId, out var session)
                || !state.Workspaces.TryGetValue(workspaceId, out var workspace)
                || !workspace.Memberships.ContainsKey(principal.UserId)) return null;
            session.SelectedWorkspaceId = workspaceId;
            return principal with { SelectedWorkspaceId = workspaceId };
        }, cancellationToken);

    public Task<WorkspaceAccess?> GetAccessAsync(WorkspacePrincipal principal, string workspaceId, CancellationToken cancellationToken = default) =>
        ReadAsync<WorkspaceAccess?>(state => state.Workspaces.TryGetValue(workspaceId, out var workspace)
            && workspace.Memberships.TryGetValue(principal.UserId, out var role)
                ? new WorkspaceAccess(workspace.Id, workspace.Name, role) : null, cancellationToken);

    public Task<IssuedMagicLink?> InviteAsync(WorkspacePrincipal owner, string workspaceId, string email, CancellationToken cancellationToken = default) =>
        MutateAsync<IssuedMagicLink?>(state => IsOwner(state, owner.UserId, workspaceId)
            ? Issue(state, WorkspaceTokens.NormalizeEmail(email), workspaceId) : null, cancellationToken);

    public Task<bool> RevokeAsync(WorkspacePrincipal owner, string workspaceId, string memberUserId, CancellationToken cancellationToken = default) =>
        MutateAsync(state =>
        {
            if (!IsOwner(state, owner.UserId, workspaceId) || owner.UserId == memberUserId
                || !state.Workspaces[workspaceId].Memberships.TryGetValue(memberUserId, out var role)
                || role == WorkspaceRole.Owner) return false;
            state.Workspaces[workspaceId].Memberships.Remove(memberUserId);
            foreach (var session in state.Sessions.Values.Where(session => session.UserId == memberUserId
                         && session.SelectedWorkspaceId == workspaceId)) session.SelectedWorkspaceId = null;
            return true;
        }, cancellationToken);

    public Task<IReadOnlyList<WorkspaceMember>?> MembersAsync(WorkspacePrincipal principal, string workspaceId, CancellationToken cancellationToken = default) =>
        ReadAsync<IReadOnlyList<WorkspaceMember>?>(state =>
        {
            if (!state.Workspaces.TryGetValue(workspaceId, out var workspace)
                || !workspace.Memberships.ContainsKey(principal.UserId)) return null;
            return workspace.Memberships.Select(member => new WorkspaceMember(
                member.Key, state.UsersById[member.Key].Email, member.Value)).ToArray();
        }, cancellationToken);

    /// <summary>
    /// Idempotent Development fixture: fixed workspace + session token already selected.
    /// </summary>
    public Task<DevelopmentWorkspaceFixture> EnsureDevelopmentFixtureAsync(
        string? workspaceId = null,
        string? workspaceName = null,
        string? email = null,
        string? sessionToken = null,
        CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrWhiteSpace(workspaceId) ? DevelopmentWorkspaceDefaults.WorkspaceId : workspaceId.Trim();
        var name = string.IsNullOrWhiteSpace(workspaceName) ? DevelopmentWorkspaceDefaults.WorkspaceName : workspaceName.Trim();
        var normalizedEmail = WorkspaceTokens.NormalizeEmail(
            string.IsNullOrWhiteSpace(email) ? DevelopmentWorkspaceDefaults.Email : email);
        var token = string.IsNullOrWhiteSpace(sessionToken) ? DevelopmentWorkspaceDefaults.SessionToken : sessionToken.Trim();

        return MutateAsync(state =>
        {
            WorkspaceUserState user;
            if (state.UserIdsByEmail.TryGetValue(normalizedEmail, out var existingId)
                && state.UsersById.TryGetValue(existingId, out var existingUser))
            {
                user = existingUser;
            }
            else
            {
                user = new WorkspaceUserState { Id = DevelopmentWorkspaceDefaults.UserId, Email = normalizedEmail };
                state.UserIdsByEmail[normalizedEmail] = user.Id;
                state.UsersById[user.Id] = user;
            }

            if (!state.Workspaces.TryGetValue(id, out var workspace))
            {
                workspace = new WorkspaceDocument { Id = id, Name = name };
                state.Workspaces[id] = workspace;
            }
            else
            {
                workspace.Name = name;
            }

            workspace.Memberships[user.Id] = WorkspaceRole.Owner;
            state.Sessions[WorkspaceTokens.Hash(token)] = new WorkspaceSessionState
            {
                UserId = user.Id,
                SelectedWorkspaceId = id
            };
            return new DevelopmentWorkspaceFixture(id, workspace.Name, normalizedEmail, token);
        }, cancellationToken);
    }

    IssuedMagicLink Issue(WorkspaceAccessDocument state, string email, string? workspaceId)
    {
        var raw = WorkspaceTokens.New();
        var expires = _time.GetUtcNow().AddMinutes(15);
        state.Links[WorkspaceTokens.Hash(raw)] = new WorkspaceLinkState
            { Email = email, WorkspaceId = workspaceId, ExpiresAt = expires };
        return new IssuedMagicLink(raw, expires);
    }

    static WorkspaceUserState GetOrCreateUser(WorkspaceAccessDocument state, string email)
    {
        if (state.UserIdsByEmail.TryGetValue(email, out var id)) return state.UsersById[id];
        var user = new WorkspaceUserState { Id = $"usr_{Guid.NewGuid():N}", Email = email };
        state.UserIdsByEmail[email] = user.Id;
        state.UsersById[user.Id] = user;
        return user;
    }

    static bool IsOwner(WorkspaceAccessDocument state, string userId, string workspaceId) =>
        state.Workspaces.TryGetValue(workspaceId, out var workspace)
        && workspace.Memberships.GetValueOrDefault(userId) == WorkspaceRole.Owner;

    async Task<T> ReadAsync<T>(Func<WorkspaceAccessDocument, T> read, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var command = dataSource.CreateCommand(
            "SELECT state::text FROM nuotti_workspace_access WHERE singleton=true");
        return read(JsonSerializer.Deserialize<WorkspaceAccessDocument>((string)(await command.ExecuteScalarAsync(ct))!)!);
    }

    async Task<T> MutateAsync<T>(Func<WorkspaceAccessDocument, T> mutate, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var load = new NpgsqlCommand(
            "SELECT state::text FROM nuotti_workspace_access WHERE singleton=true FOR UPDATE", connection, transaction);
        var state = JsonSerializer.Deserialize<WorkspaceAccessDocument>((string)(await load.ExecuteScalarAsync(ct))!)!;
        var result = mutate(state);
        await using var save = new NpgsqlCommand(
            "UPDATE nuotti_workspace_access SET state=$1::jsonb, updated_at=now() WHERE singleton=true", connection, transaction);
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
                CREATE TABLE IF NOT EXISTS nuotti_workspace_access (
                    singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
                    state jsonb NOT NULL,
                    updated_at timestamptz NOT NULL DEFAULT now());
                INSERT INTO nuotti_workspace_access(singleton, state)
                VALUES (true, '{"usersById":{},"userIdsByEmail":{},"links":{},"sessions":{},"workspaces":{}}'::jsonb)
                ON CONFLICT (singleton) DO NOTHING;
                """);
            await command.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally { _initializeGate.Release(); }
    }
}

internal sealed class WorkspaceAccessDocument
{
    public Dictionary<string, WorkspaceUserState> UsersById { get; set; } = [];
    public Dictionary<string, string> UserIdsByEmail { get; set; } = [];
    public Dictionary<string, WorkspaceLinkState> Links { get; set; } = [];
    public Dictionary<string, WorkspaceSessionState> Sessions { get; set; } = [];
    public Dictionary<string, WorkspaceDocument> Workspaces { get; set; } = [];
}
internal sealed class WorkspaceUserState { public string Id { get; set; } = ""; public string Email { get; set; } = ""; }
internal sealed class WorkspaceLinkState
{
    public string Email { get; set; } = "";
    public string? WorkspaceId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
}
internal sealed class WorkspaceSessionState { public string UserId { get; set; } = ""; public string? SelectedWorkspaceId { get; set; } }
internal sealed class WorkspaceDocument
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, WorkspaceRole> Memberships { get; set; } = [];
}
