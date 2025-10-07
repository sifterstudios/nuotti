using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.Actors;

public sealed class PerformerActor : BaseActor
{
    public PerformerActor(IHubClientFactory hubClientFactory, Uri baseUri, string session)
        : base(hubClientFactory, baseUri, session) { }

    protected override string Role => "performer";
}