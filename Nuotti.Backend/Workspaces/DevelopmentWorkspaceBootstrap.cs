namespace Nuotti.Backend.Workspaces;

/// <summary>Seeds the fixed Development testing workspace on any supported access store.</summary>
public static class DevelopmentWorkspaceBootstrap
{
    public static async Task<DevelopmentWorkspaceFixture> EnsureAsync(
        IWorkspaceAccessStore access,
        IConfiguration? config = null,
        CancellationToken cancellationToken = default)
    {
        var section = config?.GetSection("Dev");
        return access switch
        {
            InMemoryWorkspaceAccessStore memory => memory.EnsureDevelopmentFixture(
                section?["WorkspaceId"], section?["WorkspaceName"], section?["Email"], section?["SessionToken"]),
            PostgresWorkspaceAccessStore postgres => await postgres.EnsureDevelopmentFixtureAsync(
                section?["WorkspaceId"], section?["WorkspaceName"], section?["Email"], section?["SessionToken"],
                cancellationToken),
            _ => throw new InvalidOperationException(
                $"Development fixture is not supported for {access.GetType().Name}.")
        };
    }
}
