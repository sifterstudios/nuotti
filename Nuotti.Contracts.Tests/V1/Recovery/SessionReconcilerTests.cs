using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;
using Nuotti.Contracts.V1.Recovery;
using Nuotti.Contracts.V1.Reducer;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Recovery;

public class SessionReconcilerTests
{
    [Fact]
    public void Adopts_snapshot_applies_replay_and_explains_impact()
    {
        var local = GameReducer.Initial("S1") with { Phase = Phase.Guessing };
        var recovered = GameReducer.Initial("S1") with { Phase = Phase.Lock, SongIndex = 1 };
        var hint = new HintGiven(1)
        {
            SessionCode = "S1",
            CausedByCommandId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid()
        };

        var result = SessionReconciler.Apply(
            local,
            recovered,
            new ControlGeneration(3),
            new SessionSequence(12),
            [hint]);

        result.Snapshot.Phase.Should().Be(Phase.Lock);
        result.Snapshot.HintIndex.Should().Be(1);
        result.ControlGeneration.Value.Should().Be(3);
        result.LastSequence.Value.Should().Be(12);
        result.ControlsReady.Should().BeTrue();
        result.ImpactSummary.Should().Contain("Lock");
        result.RecommendedAction.Should().Contain("Wait");
    }

    [Fact]
    public void Without_local_state_reports_restored_phase()
    {
        var recovered = GameReducer.Initial("S1") with { Phase = Phase.Intermission };
        var result = SessionReconciler.Apply(
            null,
            recovered,
            ControlGeneration.Initial,
            SessionSequence.None,
            []);

        result.ImpactSummary.Should().Contain("Intermission");
        result.ControlsReady.Should().BeTrue();
    }
}
