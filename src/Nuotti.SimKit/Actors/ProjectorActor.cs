using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.Actors;

public sealed class ProjectorActor : BaseActor
{
    public ProjectorActor(IHubClientFactory hubClientFactory, Uri baseUri, string session)
        : base(hubClientFactory, baseUri, session) { }

    protected override string Role => "projector";
}