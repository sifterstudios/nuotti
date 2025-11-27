using Nuotti.Backend.Metrics;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
namespace Nuotti.Backend.Eventing.Subscribers;

/// <summary>
/// Minimal metrics subscriber that logs counts for events and records metrics.
/// </summary>
public sealed class MetricsSubscriber : IDisposable
{
    readonly IDisposable _sub;
    readonly ILogger<MetricsSubscriber> _logger;
    readonly BackendMetrics? _metrics;
    int _answerSubmittedCount;

    public MetricsSubscriber(IEventBus bus, ILogger<MetricsSubscriber> logger, BackendMetrics? metrics = null)
    {
        _logger = logger;
        _metrics = metrics;
        _sub = bus.Subscribe<AnswerSubmitted>(OnAnswerSubmittedAsync);
    }

    Task OnAnswerSubmittedAsync(AnswerSubmitted evt, CancellationToken ct)
    {
        var count = Interlocked.Increment(ref _answerSubmittedCount);
        _logger.LogInformation("Metrics: AnswerSubmitted total={Count} session={Session} CorrelationId={CorrelationId} CausedByCommandId={CausedByCommandId}", 
            count, evt.SessionCode, evt.CorrelationId, evt.CausedByCommandId);
        
        // Record answer submission in metrics
        _metrics?.RecordAnswerSubmitted();
        
        return Task.CompletedTask;
    }

    public void Dispose() => _sub.Dispose();
}