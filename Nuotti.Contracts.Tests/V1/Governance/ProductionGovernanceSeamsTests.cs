using FluentAssertions;
using Nuotti.Contracts.V1.Governance;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Governance;

public class ProductionGovernanceSeamsTests
{
    [Fact]
    public void Telemetry_correlates_safe_ids_and_redacts_secrets()
    {
        var session = SafeTelemetryIdentifiers.CorrelateSession("SHOW-42");
        session.Should().StartWith("session:");
        session.Should().NotContain("SHOW");

        var text = SafeTelemetryIdentifiers.RedactSecrets("Authorization: Bearer super-secret-token password=hunter2");
        text.Should().NotContain("super-secret-token");
        text.Should().NotContain("hunter2");
        text.Should().Contain("***REDACTED***");
        SafeTelemetryIdentifiers.ContainsSecret("api_key=abc").Should().BeTrue();
    }

    [Fact]
    public void Scoped_verbose_auto_expires()
    {
        var capture = new ScopedVerboseCapture();
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z");
        var scope = capture.Elevate(DiagnosticVerbosity.Verbose, TimeSpan.FromMinutes(15), now, "scope-a");
        scope.Should().Be("scope-a");
        capture.EffectiveLevel(now).Should().Be(DiagnosticVerbosity.Verbose);
        capture.EffectiveLevel(now.AddMinutes(16)).Should().Be(DiagnosticVerbosity.Information);
        capture.ActiveScopeId(now.AddMinutes(16)).Should().BeNull();
    }

    [Fact]
    public void Support_bundles_bound_and_redact_evidence()
    {
        var evidence = new BoundedSupportEvidence(maxItems: 2, maxCharsPerItem: 40, maxTotalChars: 60);
        evidence.TryAdd("log-1", "token=abc123456789 and more text that will truncate").Should().BeTrue();
        evidence.TryAdd("log-2", "second").Should().BeTrue();
        evidence.TryAdd("log-3", "overflow").Should().BeFalse();
        evidence.Truncated.Should().BeTrue();
        evidence.Items.Should().HaveCount(2);
        evidence.Items[0].Content.Should().NotContain("abc123456789");
        evidence.RenderManifest(SafeTelemetryIdentifiers.CorrelateWorkspace("corr-1"))
            .Should().Contain("truncated=True");
    }

    [Fact]
    public void Signed_lease_verifies_expiry_and_integrity()
    {
        var issuer = new SignedLeaseIssuer(SignedLeaseIssuer.CreateKey());
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z");
        var lease = issuer.Issue("agent-1", "ws-1", "S1", now.AddMinutes(30));
        issuer.TryVerify(lease, now, out _).Should().BeTrue();
        issuer.TryVerify(lease, now.AddHours(1), out var expired).Should().BeFalse();
        expired.Should().Be("expired");

        var tampered = lease with { SessionCode = "OTHER" };
        issuer.TryVerify(tampered, now, out var badSig).Should().BeFalse();
        badSig.Should().Be("invalid-signature");
    }

    [Fact]
    public void Entitlement_and_takedown_enforce_boundaries()
    {
        var entitlements = new EntitlementGate();
        entitlements.Invoking(g => g.Ensure("ws-1", EntitlementKind.DiagnosticsExport))
            .Should().Throw<UnauthorizedAccessException>();
        entitlements.Grant("ws-1", EntitlementKind.DiagnosticsExport);
        entitlements.IsAllowed("ws-1", EntitlementKind.DiagnosticsExport).Should().BeTrue();

        var takedown = new TakedownCaseStore();
        var now = DateTimeOffset.UtcNow;
        var opened = takedown.Open("ws-1", "rev_audio_1", "rights dispute", now);
        takedown.IsBlocked("ws-1", "rev_audio_1").Should().BeFalse();
        takedown.Enforce(opened.CaseId, now);
        takedown.IsBlocked("ws-1", "rev_audio_1").Should().BeTrue();

        RetentionBoundary.IsExpired(now.AddDays(-31), RetentionBoundary.SessionResults, now).Should().BeTrue();
        RetentionBoundary.IsExpired(now.AddDays(-1), RetentionBoundary.SessionResults, now).Should().BeFalse();
    }
}
