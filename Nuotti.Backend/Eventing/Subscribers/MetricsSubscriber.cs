using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
namespace Nuotti.Backend.Eventing.Subscribers;

/// <summary>
/// Minimal metrics subscriber that logs counts for events.
/// </summary>
public sealed class MetricsSubscriber : IDisposable
{
    readonly IDisposable _sub;
    readonly ILogger<MetricsSubscriber> _logger;
    int _answerSubmittedCount;

    public MetricsSubscriber(IEventBus bus, ILogger<MetricsSubscriber> logger)
    {
        _logger = logger;
        _sub = bus.Subscribe<AnswerSubmitted>(OnAnswerSubmittedAsync);
    }

    Task OnAnswerSubmittedAsync(AnswerSubmitted evt, CancellationToken ct)
    {
        var count = Interlocked.Increment(ref _answerSubmittedCount);
        _logger.LogInformation("Metrics: AnswerSubmitted total={Count} session={Session} CorrelationId={CorrelationId} CausedByCommandId={CausedByCommandId}", 
            count, evt.SessionCode, evt.CorrelationId, evt.CausedByCommandId);
        return Task.CompletedTask;
    }

    public void Dispose() => _sub.Dispose();
}