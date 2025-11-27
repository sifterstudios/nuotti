using Nuotti.Backend.Sessions;
using Nuotti.Backend.Telemetry;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Reducer;
namespace Nuotti.Backend.Eventing.Subscribers;

/// <summary>
/// Applies incoming events to the per-session game state using the pure reducer.
/// </summary>
public sealed class StateApplySubscriber : IDisposable
{
    readonly IDisposable _sub;
    readonly IGameStateStore _store;
    readonly ILogger<StateApplySubscriber> _logger;

    public StateApplySubscriber(IEventBus bus, IGameStateStore store, ILogger<StateApplySubscriber> logger)
    {
        _store = store; _logger = logger;
        _sub = bus.Subscribe<AnswerSubmitted>(OnAnswerSubmittedAsync);
    }

    Task OnAnswerSubmittedAsync(AnswerSubmitted evt, CancellationToken ct)
    {
        using var activity = BackendActivitySource.StartEventProcessing(
            nameof(AnswerSubmitted), 
            nameof(StateApplySubscriber), 
            evt.SessionCode);
        activity?.SetTag("correlation.id", evt.CorrelationId.ToString());
        activity?.SetTag("command.id", evt.CausedByCommandId.ToString());

        var state = _store.GetOrCreate(evt.SessionCode, GameReducer.Initial);
        var (next, error) = GameReducer.Reduce(state, evt);
        if (error != null)
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", error);
            _logger.LogWarning("Reducer error applying {Event}: {Error}. CorrelationId={CorrelationId}, CausedByCommandId={CausedByCommandId}", 
                nameof(AnswerSubmitted), error, evt.CorrelationId, evt.CausedByCommandId);
            return Task.CompletedTask;
        }
        _store.Set(evt.SessionCode, next);
        _logger.LogDebug("Applied {Event} to session {Session}. CorrelationId={CorrelationId}, CausedByCommandId={CausedByCommandId}", 
            nameof(AnswerSubmitted), evt.SessionCode, evt.CorrelationId, evt.CausedByCommandId);
        return Task.CompletedTask;
    }

    public void Dispose() => _sub.Dispose();
}