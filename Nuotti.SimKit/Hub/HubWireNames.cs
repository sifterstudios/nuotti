using Nuotti.Contracts.V1.Eventing;

namespace Nuotti.SimKit.Hub;

/// <summary>
/// SimKit view of the shared Fan-out wire contract. Prefer <see cref="HubWireContract"/> for new code.
/// </summary>
public static class HubWireNames
{
    public static IReadOnlyDictionary<Type, string> ByPayloadType => HubWireContract.SessionBroadcasts;

    public static string For<T>() => HubWireContract.SessionMethodFor<T>();
}
