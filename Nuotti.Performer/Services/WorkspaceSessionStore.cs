using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Nuotti.Performer.Services;

/// <summary>
/// Where a redeemed Workspace session token is kept between page loads.
/// </summary>
/// <remarks>
/// This exists as an interface because <see cref="ProtectedLocalStorage"/> is sealed with
/// non-virtual members and needs a live JS runtime, so a component test cannot sign in without
/// it. It is also the seam a future cookie-based scheme would replace.
/// </remarks>
public interface IWorkspaceSessionStore
{
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
    Task SetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task ClearTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists the token in browser local storage, encrypted with ASP.NET Data Protection.
/// </summary>
/// <remarks>
/// The key ring must outlive the container or every stored token becomes undecryptable on
/// restart - see the performer-dpkeys volume in deploy/docker-compose.unraid.yml.
/// </remarks>
public sealed class ProtectedLocalStorageSessionStore(ProtectedLocalStorage storage) : IWorkspaceSessionStore
{
    const string Key = "nuotti.workspace.session";

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await storage.GetAsync<string>(Key);
            return result.Success ? result.Value : null;
        }
        catch (System.Exception)
        {
            // Thrown during prerender (no JS runtime yet) and when the Data Protection key that
            // encrypted the value is gone. Neither is worth failing a render over; the caller
            // treats a null token as "not signed in".
            return null;
        }
    }

    public async Task SetTokenAsync(string token, CancellationToken cancellationToken = default) =>
        await storage.SetAsync(Key, token);

    public async Task ClearTokenAsync(CancellationToken cancellationToken = default) =>
        await storage.DeleteAsync(Key);
}
