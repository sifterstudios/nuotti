using Nuotti.Contracts.V1.Model;
namespace Nuotti.AudioEngine;

public interface IProblemSink
{
    Task PublishAsync(NuottiProblem problem, CancellationToken cancellationToken = default);
}
