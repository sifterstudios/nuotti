using FluentAssertions;
using Nuotti.Contracts.V1.Reducer;
using Nuotti.Projector.Services;
using Xunit;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;

namespace Nuotti.Projector.Tests;

/// <summary>
/// UpdateFromSnapshot skips broadcasts it considers duplicates, using a hash of the snapshot.
/// Before this test, that hash counted Choices but never looked at their contents, so two
/// snapshots with the same phase/song/hint/tallies but different options hashed identically —
/// a corrected question would never reach the Projector once the stale one had already rendered.
/// </summary>
public class GameStateServiceTests
{
    [Fact]
    public void UpdateFromSnapshot_treats_changed_choice_contents_as_a_distinct_state()
    {
        var service = new GameStateService();
        var pushed = GameReducer.Initial("s1") with
        {
            Phase = PhaseEnum.Guessing,
            Choices = ["a", "b", "c", "d"],
            Tallies = [0, 0, 0, 0]
        };
        service.UpdateFromSnapshot(pushed);

        // Same phase, song index, hint index, tally shape and choice count — only the option
        // text itself changed, as it would if a performer corrected the question.
        var corrected = pushed with { Choices = ["w", "x", "y", "z"] };
        service.UpdateFromSnapshot(corrected);

        service.CurrentState.Choices.Should().Equal("w", "x", "y", "z");
    }

    [Fact]
    public void UpdateFromSnapshot_still_skips_a_truly_identical_broadcast()
    {
        var service = new GameStateService();
        var raised = 0;
        var snapshot = GameReducer.Initial("s1") with
        {
            Phase = PhaseEnum.Guessing,
            Choices = ["a", "b", "c", "d"],
            Tallies = [0, 0, 0, 0]
        };
        service.UpdateFromSnapshot(snapshot);
        service.StateChanged += _ => raised++;

        service.UpdateFromSnapshot(snapshot with { });

        raised.Should().Be(0);
    }
}
