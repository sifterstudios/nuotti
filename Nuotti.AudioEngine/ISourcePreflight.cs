using Nuotti.Contracts.V1.Model;
namespace Nuotti.AudioEngine;

public interface ISourcePreflight
{
    Task<PreflightResult> CheckAsync(string input, CancellationToken cancellationToken = default);
}

public sealed record PreflightResult(bool Ok, string? NormalizedUrl = null, NuottiProblem? Problem = null);
