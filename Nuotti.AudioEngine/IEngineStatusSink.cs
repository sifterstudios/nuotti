using Nuotti.Contracts.V1.Message;
namespace Nuotti.AudioEngine;

public interface IEngineStatusSink
{
    Task PublishAsync(EngineStatusChanged evt, CancellationToken cancellationToken = default);
}
