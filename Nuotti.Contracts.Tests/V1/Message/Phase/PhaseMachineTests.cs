using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Reflection;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;

namespace Nuotti.Contracts.Tests.V1.Message.Phase;

/// <summary>
/// Guards the phase machine as a whole rather than one command at a time. Three transitions were
/// missing before these tests existed - nothing reached Guessing, nothing left Intermission, and
/// nothing reached Finished - so a session could be started and then never driven any further, and
/// a game could never end. Each was invisible to per-command tests because every command was
/// individually correct.
/// </summary>
public class PhaseMachineTests
{
    /// <summary>Every IPhaseChange command in Contracts, discovered rather than listed.</summary>
    static IReadOnlyList<IPhaseChange> Transitions()
        => typeof(CommandBase).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(IPhaseChange).IsAssignableFrom(t))
            .Select(Instantiate)
            .Cast<IPhaseChange>()
            .ToArray();

    static object Instantiate(Type type)
    {
        var ctor = type.GetConstructors().OrderBy(c => c.GetParameters().Length).First();
        var args = ctor.GetParameters().Select(p => Sample(p.ParameterType)).ToArray();

        var instance = ctor.Invoke(args);

        // CommandBase's required members have to be set through the object initializer.
        foreach (var (name, value) in new (string, object)[]
                 {
                     (nameof(CommandBase.SessionCode), "S"),
                     (nameof(CommandBase.IssuedByRole), Role.Performer),
                     (nameof(CommandBase.IssuedById), "perf-1")
                 })
        {
            type.GetProperty(name)!.SetValue(instance, value);
        }

        return instance;
    }

    static object? Sample(Type t)
    {
        if (t == typeof(SongId)) return new SongId("song-1");
        if (t == typeof(SongId?)) return new SongId("song-1");
        if (t == typeof(SongRef)) return new SongRef(new SongId("song-1"), "T", "A");
        if (t == typeof(Hint)) return new Hint(0, "hint", null, new SongId("song-1"));
        if (t == typeof(SetlistManifest)) return new SetlistManifest();
        if (t == typeof(string)) return "S";
        if (t == typeof(int)) return 0;
        return t.IsValueType ? Activator.CreateInstance(t) : null;
    }

    /// <summary>
    /// Phases a session can sit in and still be driven onwards. Idle is excluded: a session leaves
    /// it by being created, not by a phase change.
    /// </summary>
    static IEnumerable<PhaseEnum> DrivablePhases()
        => System.Enum.GetValues<PhaseEnum>().Where(p => p != PhaseEnum.Idle);

    [Fact]
    public void Every_phase_a_session_can_reach_has_a_way_out()
    {
        var transitions = Transitions();

        var deadEnds = DrivablePhases()
            .Where(phase => !transitions.Any(t => t.IsPhaseChangeAllowed(phase)))
            .ToArray();

        Assert.Empty(deadEnds);
    }

    [Fact]
    public void Every_phase_except_the_starting_one_can_be_reached()
    {
        var reachable = Transitions().Select(t => t.TargetPhase).ToHashSet();

        // Lobby is produced by creating a session, and Idle is the pre-session default, so neither
        // is the target of a phase change.
        var unreachable = System.Enum.GetValues<PhaseEnum>()
            .Where(p => p is not (PhaseEnum.Idle or PhaseEnum.Lobby))
            .Where(p => !reachable.Contains(p))
            .ToArray();

        // Hint is a known exception: hints are given during Guessing via GiveHint, which changes no
        // phase, so the Hint phase itself is currently vestigial.
        Assert.Equal([PhaseEnum.Hint], unreachable);
    }

    [Fact]
    public void A_full_round_can_be_driven_from_Lobby_back_to_Start()
    {
        var transitions = Transitions();

        PhaseEnum Apply<T>(PhaseEnum from) where T : IPhaseChange
        {
            var command = transitions.OfType<T>().Single();
            Assert.True(command.IsPhaseChangeAllowed(from),
                $"{typeof(T).Name} should be allowed from {from}");
            return command.TargetPhase;
        }

        var phase = PhaseEnum.Lobby;
        phase = Apply<StartGame>(phase);
        Assert.Equal(PhaseEnum.Start, phase);

        phase = Apply<OpenAnswers>(phase);
        Assert.Equal(PhaseEnum.Guessing, phase);

        phase = Apply<LockAnswers>(phase);
        Assert.Equal(PhaseEnum.Lock, phase);

        phase = Apply<RevealAnswer>(phase);
        Assert.Equal(PhaseEnum.Reveal, phase);

        phase = Apply<PlaySong>(phase);
        Assert.Equal(PhaseEnum.Play, phase);

        phase = Apply<EndSong>(phase);
        Assert.Equal(PhaseEnum.Intermission, phase);

        // ...and round again for the next song.
        phase = Apply<NextRound>(phase);
        Assert.Equal(PhaseEnum.Start, phase);
    }

    [Fact]
    public void A_game_can_be_ended_from_Intermission_and_restarted()
    {
        var transitions = Transitions();
        var endGame = transitions.OfType<EndGame>().Single();
        var startGame = transitions.OfType<StartGame>().Single();

        Assert.True(endGame.IsPhaseChangeAllowed(PhaseEnum.Intermission));
        Assert.Equal(PhaseEnum.Finished, endGame.TargetPhase);

        // StartGame already accepted Finished as a source, which is why Finished being unreachable
        // was a gap rather than a deliberate omission.
        Assert.True(startGame.IsPhaseChangeAllowed(PhaseEnum.Finished));
    }

    [Fact]
    public void No_transition_claims_a_source_phase_it_also_targets()
    {
        // A self-transition would let a command fire repeatedly with no observable progress.
        var offenders = Transitions()
            .Where(t => t.AllowedSourcePhases.Contains(t.TargetPhase))
            .Select(t => t.GetType().Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Commands_implementing_both_guards_keep_them_in_step()
    {
        // Every IPhaseRestricted+IPhaseChange command must declare the same set for both interfaces,
        // otherwise Guard cannot be satisfied: it enforces AllowedPhases AND IsPhaseChangeAllowed
        // (which checks AllowedSourcePhases). PlaySong violated this until fixed, making it
        // unsatisfiable from any phase. This invariant prevents the copy-paste bug from
        // re-appearing in sibling commands.
        //
        // The tests that predate this one could not have caught the PlaySong bug: they all
        // consult IsPhaseChangeAllowed / AllowedSourcePhases and never AllowedPhases, so a
        // command with a satisfiable transition but an unsatisfiable restriction looked healthy
        // to every one of them. That blind spot is what this invariant closes.
        var bothGuards = Transitions()
            .Where(t => t is IPhaseRestricted)
            .ToList();

        var offenders = bothGuards
            .Where(cmd =>
            {
                var restricted = (IPhaseRestricted)cmd;
                return !restricted.AllowedPhases.ToHashSet().SetEquals(cmd.AllowedSourcePhases);
            })
            .Select(cmd => cmd.GetType().Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void No_command_is_unsatisfiable_from_every_phase()
    {
        // Every command implementing both guards must be applicable from at least one phase.
        // If AllowedPhases and AllowedSourcePhases are disjoint, Guard cannot be satisfied.
        var bothGuards = Transitions()
            .Where(t => t is IPhaseRestricted)
            .ToList();

        var drivable = DrivablePhases().ToHashSet();
        var unsatisfiable = bothGuards
            .Where(cmd => !drivable.Any(p =>
            {
                var restricted = (IPhaseRestricted)cmd;
                return restricted.AllowedPhases.Contains(p) && cmd.IsPhaseChangeAllowed(p);
            }))
            .Select(cmd => cmd.GetType().Name)
            .ToArray();

        Assert.Empty(unsatisfiable);
    }
}
