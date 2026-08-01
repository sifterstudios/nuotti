namespace Nuotti.Backend.Workspaces;

public sealed class InMemoryWorkspaceAccessStore(TimeProvider? timeProvider = null) : IWorkspaceAccessStore
{
    sealed record User(string Id, string Email);
    sealed record Link(string Email, string? WorkspaceId, DateTimeOffset ExpiresAt, bool Used = false);
    sealed record Session(string UserId, string? SelectedWorkspaceId);
    sealed record Workspace(string Id, string Name, Dictionary<string, WorkspaceRole> Memberships);

    readonly object _gate = new();
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly Dictionary<string, User> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, User> _usersById = new(StringComparer.Ordinal);
    readonly Dictionary<string, Link> _links = new(StringComparer.Ordinal);
    readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    readonly Dictionary<string, Workspace> _workspaces = new(StringComparer.Ordinal);

    public Task<IssuedMagicLink> IssueSignInAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Issue(WorkspaceTokens.NormalizeEmail(email), workspaceId: null));

    public Task<RedeemedMagicLink?> RedeemAsync(string token, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var hash = WorkspaceTokens.Hash(token);
            if (!_links.TryGetValue(hash, out var link) || link.Used || link.ExpiresAt <= _time.GetUtcNow())
                return Task.FromResult<RedeemedMagicLink?>(null);
            _links[hash] = link with { Used = true };
            var user = GetOrCreateUser(link.Email);
            if (link.WorkspaceId is not null && _workspaces.TryGetValue(link.WorkspaceId, out var workspace))
                workspace.Memberships.TryAdd(user.Id, WorkspaceRole.Member);
            var rawSession = WorkspaceTokens.New();
            var sessionId = WorkspaceTokens.Hash(rawSession);
            // Redeeming membership never changes active context; selection is explicit.
            _sessions[sessionId] = new Session(user.Id, SelectedWorkspaceId: null);
            return Task.FromResult<RedeemedMagicLink?>(new(
                rawSession, new WorkspacePrincipal(user.Id, user.Email, null, sessionId)));
        }
    }

    public Task<WorkspacePrincipal?> AuthenticateAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(WorkspaceTokens.Hash(sessionToken), out var session)
                || !_usersById.TryGetValue(session.UserId, out var user))
                return Task.FromResult<WorkspacePrincipal?>(null);
            return Task.FromResult<WorkspacePrincipal?>(new(user.Id, user.Email, session.SelectedWorkspaceId, WorkspaceTokens.Hash(sessionToken)));
        }
    }

    public Task<WorkspaceAccess> CreateWorkspaceAsync(WorkspacePrincipal principal, string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Workspace name is required.", nameof(name)) : name.Trim();
        lock (_gate)
        {
            var id = $"ws_{Guid.NewGuid():N}";
            _workspaces[id] = new Workspace(id, normalizedName, new Dictionary<string, WorkspaceRole>
            {
                [principal.UserId] = WorkspaceRole.Owner
            });
            return Task.FromResult(new WorkspaceAccess(id, normalizedName, WorkspaceRole.Owner));
        }
    }

    public Task<IReadOnlyList<WorkspaceAccess>> ListAsync(WorkspacePrincipal principal, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<WorkspaceAccess>>(_workspaces.Values
                .Where(workspace => workspace.Memberships.TryGetValue(principal.UserId, out _))
                .Select(workspace => new WorkspaceAccess(workspace.Id, workspace.Name, workspace.Memberships[principal.UserId]))
                .OrderBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public Task<WorkspacePrincipal?> SelectAsync(WorkspacePrincipal principal, string workspaceId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_workspaces.TryGetValue(workspaceId, out var workspace)
                || !workspace.Memberships.ContainsKey(principal.UserId))
                return Task.FromResult<WorkspacePrincipal?>(null);
            var key = principal.AuthenticationSessionId;
            if (key is null) return Task.FromResult<WorkspacePrincipal?>(null);
            _sessions[key] = _sessions[key] with { SelectedWorkspaceId = workspaceId };
            return Task.FromResult<WorkspacePrincipal?>(principal with { SelectedWorkspaceId = workspaceId });
        }
    }

    public Task<WorkspaceAccess?> GetAccessAsync(WorkspacePrincipal principal, string workspaceId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_workspaces.TryGetValue(workspaceId, out var workspace)
                || !workspace.Memberships.TryGetValue(principal.UserId, out var role))
                return Task.FromResult<WorkspaceAccess?>(null);
            return Task.FromResult<WorkspaceAccess?>(new(workspace.Id, workspace.Name, role));
        }
    }

    public Task<IssuedMagicLink?> InviteAsync(WorkspacePrincipal owner, string workspaceId, string email, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!IsOwner(owner.UserId, workspaceId)) return Task.FromResult<IssuedMagicLink?>(null);
            return Task.FromResult<IssuedMagicLink?>(Issue(WorkspaceTokens.NormalizeEmail(email), workspaceId));
        }
    }

    public Task<bool> RevokeAsync(WorkspacePrincipal owner, string workspaceId, string memberUserId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!IsOwner(owner.UserId, workspaceId) || owner.UserId == memberUserId
                || !_workspaces[workspaceId].Memberships.TryGetValue(memberUserId, out var role)
                || role == WorkspaceRole.Owner) return Task.FromResult(false);
            _workspaces[workspaceId].Memberships.Remove(memberUserId);
            foreach (var key in _sessions.Where(pair => pair.Value.UserId == memberUserId
                    && pair.Value.SelectedWorkspaceId == workspaceId).Select(pair => pair.Key).ToArray())
                _sessions[key] = _sessions[key] with { SelectedWorkspaceId = null };
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<WorkspaceMember>?> MembersAsync(WorkspacePrincipal principal, string workspaceId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_workspaces.TryGetValue(workspaceId, out var workspace)
                || !workspace.Memberships.ContainsKey(principal.UserId))
                return Task.FromResult<IReadOnlyList<WorkspaceMember>?>(null);
            return Task.FromResult<IReadOnlyList<WorkspaceMember>?>(workspace.Memberships
                .Select(pair => new WorkspaceMember(pair.Key, _usersById[pair.Key].Email, pair.Value)).ToArray());
        }
    }

    /// <summary>
    /// Idempotent Development fixture: fixed workspace + session token already selected.
    /// </summary>
    public DevelopmentWorkspaceFixture EnsureDevelopmentFixture(
        string? workspaceId = null,
        string? workspaceName = null,
        string? email = null,
        string? sessionToken = null)
    {
        var id = string.IsNullOrWhiteSpace(workspaceId) ? DevelopmentWorkspaceDefaults.WorkspaceId : workspaceId.Trim();
        var name = string.IsNullOrWhiteSpace(workspaceName) ? DevelopmentWorkspaceDefaults.WorkspaceName : workspaceName.Trim();
        var normalizedEmail = WorkspaceTokens.NormalizeEmail(
            string.IsNullOrWhiteSpace(email) ? DevelopmentWorkspaceDefaults.Email : email);
        var token = string.IsNullOrWhiteSpace(sessionToken) ? DevelopmentWorkspaceDefaults.SessionToken : sessionToken.Trim();

        lock (_gate)
        {
            if (!_usersByEmail.TryGetValue(normalizedEmail, out var user))
            {
                user = new User(DevelopmentWorkspaceDefaults.UserId, normalizedEmail);
                _usersByEmail[normalizedEmail] = user;
                _usersById[user.Id] = user;
            }

            if (!_workspaces.TryGetValue(id, out var workspace))
            {
                workspace = new Workspace(id, name, new Dictionary<string, WorkspaceRole>
                {
                    [user.Id] = WorkspaceRole.Owner
                });
                _workspaces[id] = workspace;
            }
            else
            {
                workspace.Memberships[user.Id] = WorkspaceRole.Owner;
                if (!string.Equals(workspace.Name, name, StringComparison.Ordinal))
                    _workspaces[id] = workspace with { Name = name };
            }

            var sessionHash = WorkspaceTokens.Hash(token);
            _sessions[sessionHash] = new Session(user.Id, id);
            return new DevelopmentWorkspaceFixture(id, _workspaces[id].Name, normalizedEmail, token);
        }
    }

    IssuedMagicLink Issue(string email, string? workspaceId)
    {
        lock (_gate)
        {
            var raw = WorkspaceTokens.New();
            var expires = _time.GetUtcNow().AddMinutes(15);
            _links[WorkspaceTokens.Hash(raw)] = new Link(email, workspaceId, expires);
            return new IssuedMagicLink(raw, expires);
        }
    }

    User GetOrCreateUser(string email)
    {
        if (_usersByEmail.TryGetValue(email, out var existing)) return existing;
        var user = new User($"usr_{Guid.NewGuid():N}", email);
        _usersByEmail[email] = user;
        _usersById[user.Id] = user;
        return user;
    }

    bool IsOwner(string userId, string workspaceId) => _workspaces.TryGetValue(workspaceId, out var workspace)
        && workspace.Memberships.GetValueOrDefault(userId) == WorkspaceRole.Owner;

}
