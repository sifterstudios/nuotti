using FluentAssertions;
using Nuotti.AudioEngine.Playback;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class PreflightFailureIntegrationTests
{
    private sealed class FakePreflight : ISourcePreflight
    {
        private readonly NuottiProblem _problem;
        public FakePreflight(string title = "HEAD failed")
        {
            _problem = NuottiProblem.UnprocessableEntity(title, "Simulated failure", ReasonCode.None, "url");
        }
        public Task<PreflightResult> CheckAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(new PreflightResult(false, null, _problem));
    }

    private sealed class ListSink : IEngineStatusSink
    {
        public List<EngineStatusChanged> Events { get; } = new();
        public Task PublishAsync(EngineStatusChanged evt, CancellationToken cancellationToken = default)
        {
            Events.Add(evt);
            return Task.CompletedTask;
        }
    }

    private sealed class NoopProblemSink : IProblemSink
    {
        public List<NuottiProblem> Problems { get; } = new();
        public Task PublishAsync(NuottiProblem problem, CancellationToken cancellationToken = default)
        {
            Problems.Add(problem);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Head_failure_preflight_reports_Error()
    {
        var player = new NoopPlayer();
        var sink = new ListSink();
        var ps = new NoopProblemSink();
        var preflight = new FakePreflight();
        var engine = new EngineCoordinator(player, sink, preflight, ps);

        await engine.OnTrackPlayRequested("https://example.com/fail.mp3");

        sink.Events.Should().ContainSingle(e => e.Status == EngineStatus.Error);
        ps.Problems.Should().ContainSingle(p => p.Title.Contains("failed", StringComparison.OrdinalIgnoreCase));
    }
}
