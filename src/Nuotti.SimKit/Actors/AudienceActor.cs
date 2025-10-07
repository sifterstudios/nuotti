using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.Actors;

public sealed class AudienceActor : BaseActor
{
    readonly string _name;

    public AudienceActor(IHubClientFactory hubClientFactory, Uri baseUri, string session, string name)
        : base(hubClientFactory, baseUri, session)
    {
        _name = name;
    }

    protected override string Role => "audience";
    protected override string? DisplayName => _name;
}