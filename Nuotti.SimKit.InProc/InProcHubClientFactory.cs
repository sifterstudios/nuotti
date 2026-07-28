using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.InProc;

/// <summary>
/// Produces hub clients wired to an in-process backend. The base address is ignored — this is
/// the fidelity swap's in-memory half, and there is no network.
/// </summary>
public sealed class InProcHubClientFactory(InProcBackend backend, string session) : IHubClientFactory
{
    public IHubClient Create(Uri baseAddress) => new InProcHubClient(backend.Bus, session);
}
