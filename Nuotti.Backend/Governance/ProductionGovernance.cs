using Nuotti.Contracts.V1.Governance;
using ServiceDefaults;
using Serilog.Events;

namespace Nuotti.Backend.Governance;

/// <summary>
/// Process-wide production governance seams registered in DI.
/// </summary>
public sealed class ProductionGovernance
{
    public ScopedVerboseCapture VerboseCapture { get; } = new();
    public EntitlementGate Entitlements { get; } = new();
    public TakedownCaseStore Takedowns { get; } = new();
    public SignedLeaseIssuer LeaseIssuer { get; } = new(SignedLeaseIssuer.CreateKey());
    public LogLevelSwitchService? LogLevelSwitch { get; set; }

    public DiagnosticVerbosity ApplyVerboseLevel(DateTimeOffset nowUtc)
    {
        var level = VerboseCapture.EffectiveLevel(nowUtc);
        if (LogLevelSwitch is not null)
        {
            LogLevelSwitch.SetLevel(level switch
            {
                DiagnosticVerbosity.Verbose => LogEventLevel.Verbose,
                DiagnosticVerbosity.Debug => LogEventLevel.Debug,
                _ => LogEventLevel.Information
            });
        }
        return level;
    }

    /// <summary>
    /// Restores private-show launch defaults for a workspace (asset download, Show Agent pairing,
    /// publish). Launch kinds are allowed unless revoked; calling this clears any prior revoke.
    /// </summary>
    public void GrantLaunchEntitlements(string workspaceId)
    {
        Entitlements.Grant(workspaceId, EntitlementKind.AssetDownload);
        Entitlements.Grant(workspaceId, EntitlementKind.ShowAgentPairing);
        Entitlements.Grant(workspaceId, EntitlementKind.PublishPackage);
    }
}
