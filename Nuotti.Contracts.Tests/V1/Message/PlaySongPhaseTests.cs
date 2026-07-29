using FluentAssertions;
using Nuotti.Contracts.V1.Message.Phase;
using Xunit;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;

namespace Nuotti.Contracts.Tests.V1.Message;

public class PlaySongPhaseTests
{
    static PlaySong ACommand() => new(new Nuotti.Contracts.V1.Model.SongId(Guid.NewGuid().ToString()))
    {
        SessionCode = "dev",
        IssuedByRole = Nuotti.Contracts.V1.Enum.Role.Performer,
        IssuedById = "perf-1"
    };

    [Fact]
    public void Is_applicable_from_at_least_one_phase()
    {
        var cmd = ACommand();

        // SessionCommandProcessor.Guard enforces AllowedPhases AND IsPhaseChangeAllowed, so a
        // command whose two declarations disjoint can never be applied from any phase at all.
        var applicable = System.Enum.GetValues<PhaseEnum>()
            .Where(p => cmd.AllowedPhases.Contains(p) && cmd.IsPhaseChangeAllowed(p))
            .ToList();

        applicable.Should().NotBeEmpty(
            "a command that satisfies neither guard simultaneously is dead code");
    }

    [Fact]
    public void Reveal_is_not_a_terminal_phase()
    {
        // Only PlaySong lists Reveal as a source phase. While it was unfireable, nothing could
        // leave Reveal, which also made Play, Intermission and Finished unreachable.
        var cmd = new PlaySong(new Nuotti.Contracts.V1.Model.SongId(Guid.NewGuid().ToString()))
        {
            SessionCode = "dev",
            IssuedByRole = Nuotti.Contracts.V1.Enum.Role.Performer,
            IssuedById = "perf-1"
        };
        var leavesReveal = new IPhaseChange[] { cmd }.Any(c => c.IsPhaseChangeAllowed(PhaseEnum.Reveal));

        leavesReveal.Should().BeTrue();
    }

    [Fact]
    public void Declares_the_same_source_phases_on_both_interfaces()
    {
        var cmd = ACommand();

        // Every other command implementing both interfaces keeps these in step; PlaySong was
        // the sole outlier, and its "Allowed phases: Play" comment described the TARGET phase.
        cmd.AllowedPhases.Should().BeEquivalentTo(cmd.AllowedSourcePhases);
    }
}
