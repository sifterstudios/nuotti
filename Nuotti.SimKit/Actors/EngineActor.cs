using Nuotti.SimKit.Hub;
namespace Nuotti.SimKit.Actors;

public sealed class EngineActor : BaseActor
{
    public EngineActor(IHubClientFactory hubClientFactory, Uri baseUri, string session)
        : base(hubClientFactory, baseUri, session) { }

    protected override string Role => "engine";
}