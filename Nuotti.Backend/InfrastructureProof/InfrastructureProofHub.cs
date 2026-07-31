using Microsoft.AspNetCore.SignalR;

namespace Nuotti.Backend.InfrastructureProof;

/// <summary>Probe-only hub used to prove that the configured backplane crosses Backend replicas.</summary>
public sealed class InfrastructureProofHub : Hub;
