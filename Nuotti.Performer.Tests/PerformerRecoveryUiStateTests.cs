using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;
using Nuotti.Contracts.V1.Recovery;
using Nuotti.Contracts.V1.Reducer;
using Nuotti.Performer;
using Xunit;

namespace Nuotti.Performer.Tests;

public class PerformerRecoveryUiStateTests
{
    sealed class FakeFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    [Fact]
    public void Disconnect_suspends_controls_with_plain_language_impact()
    {
        var state = new PerformerUiState(new FakeFactory());
        state.SetConnection(true);
        Assert.True(state.ControlsReady);

        state.SetConnection(false);
        Assert.False(state.ControlsReady);
        Assert.True(state.IsReconciling);
        Assert.Contains("lost", state.RecoveryImpact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Wait", state.RecoveryAction);
    }

    [Fact]
    public void CompleteReconciliation_restores_controls_and_applies_snapshot()
    {
        var state = new PerformerUiState(new FakeFactory());
        state.SetConnection(false);
        state.BeginReconciliation();

        var recovered = GameReducer.Initial("S1") with { Phase = Phase.Guessing };
        var result = SessionReconciler.Apply(
            null, recovered, ControlGeneration.Initial.Next(), new SessionSequence(4), []);

        state.SetConnection(true);
        state.CompleteReconciliation(result);

        Assert.True(state.ControlsReady);
        Assert.False(state.IsReconciling);
        Assert.Equal(Phase.Guessing, state.Phase);
        Assert.Contains("Guessing", state.RecoveryImpact);
    }
}
