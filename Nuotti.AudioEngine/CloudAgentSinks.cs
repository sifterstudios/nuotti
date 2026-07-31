using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.AudioEngine;

public sealed class CloudAgentStatusSink(ShowAgentCloudClient client) : IEngineStatusSink
{
    public async Task PublishAsync(EngineStatusChanged evt, CancellationToken cancellationToken = default) =>
        _ = await client.ReportStatusAsync(evt.Status switch
        {
            EngineStatus.Playing => "Playing",
            EngineStatus.Error => "Error",
            _ => "Ready"
        }, null, cancellationToken);
}

public sealed class CloudAgentProblemSink(ShowAgentCloudClient client) : IProblemSink
{
    public async Task PublishAsync(NuottiProblem problem, CancellationToken cancellationToken = default) =>
        _ = await client.ReportStatusAsync("Error", problem.Detail, cancellationToken);
}
