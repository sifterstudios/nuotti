using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;
using Nuotti.Projector.Models;
using Nuotti.Projector.Presentation;
using Nuotti.Projector.Services;
using Xunit;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;

namespace Nuotti.Projector.Tests;

/// <summary>
/// What the Projector shows, asserted through one interface with no window, no process and no
/// browser. The suite this replaces launched a detached Avalonia process and a Playwright browser
/// that was never navigated anywhere, compared screenshots with a method that returned true
/// unconditionally, and looked for a net9.0 .exe that no macOS build produces.
/// </summary>
public class PhasePresenterTests
{
    static readonly WindowSize Hd = new(1920, 1080);
    static readonly WindowSize Uhd = new(3840, 2160);

    static PhasePresenter Presenter() => new(
        new ContentSafetyService(),
        new LocalizationService(),
        new ResponsiveTypographyService());

    static GameStateSnapshot State(
        PhaseEnum phase,
        IEnumerable<string>? choices = null,
        IEnumerable<int>? tallies = null,
        SongRef? song = null,
        IReadOnlyDictionary<string, int>? scores = null,
        int songIndex = 0,
        int hintIndex = 0,
        IEnumerable<SongRef>? catalog = null)
        => GameReducer.Initial("s1") with
        {
            Phase = phase,
            Choices = choices?.ToArray() ?? [],
            Tallies = tallies?.ToArray() ?? [],
            CurrentSong = song,
            Scores = scores ?? new Dictionary<string, int>(),
            SongIndex = songIndex,
            HintIndex = hintIndex,
            Catalog = catalog?.ToArray() ?? []
        };

    static ProjectorSettings Settings(bool hideTallies = true) => new() { HideTalliesUntilReveal = hideTallies };

    [Theory]
    [InlineData(PhaseEnum.Idle, PhaseView.None)]
    [InlineData(PhaseEnum.Lobby, PhaseView.Lobby)]
    [InlineData(PhaseEnum.Guessing, PhaseView.Guessing)]
    [InlineData(PhaseEnum.Hint, PhaseView.Hint)]
    [InlineData(PhaseEnum.Intermission, PhaseView.Scoreboard)]
    [InlineData(PhaseEnum.Start, PhaseView.Simple)]
    [InlineData(PhaseEnum.Lock, PhaseView.Simple)]
    [InlineData(PhaseEnum.Reveal, PhaseView.Simple)]
    [InlineData(PhaseEnum.Play, PhaseView.Simple)]
    [InlineData(PhaseEnum.Finished, PhaseView.Simple)]
    public void Every_phase_maps_to_a_view(PhaseEnum phase, PhaseView expected)
        => Presenter().Present(State(phase), Settings(), Hd).View.Should().Be(expected);

    [Fact]
    public void Idle_is_the_only_phase_that_shows_nothing()
    {
        var presenter = Presenter();

        presenter.Present(State(PhaseEnum.Idle), Settings(), Hd).Visible.Should().BeFalse();

        foreach (var phase in Enum.GetValues<PhaseEnum>().Where(p => p != PhaseEnum.Idle))
        {
            presenter.Present(State(phase), Settings(), Hd).Visible
                .Should().BeTrue($"{phase} should be visible");
        }
    }

    [Fact]
    public void Tallies_are_hidden_during_Guessing_when_the_setting_is_on()
    {
        var spec = Presenter().Present(
            State(PhaseEnum.Guessing, choices: ["A", "B"], tallies: [3, 1]),
            Settings(hideTallies: true),
            Hd);

        spec.ShowTallies.Should().BeFalse();
        spec.Choices[0].CountText.Should().Be("?");
        spec.Choices[1].CountText.Should().Be("?");
        spec.Choices.Should().OnlyContain(c => !c.IsLeader, "no leader can be shown while counts are hidden");
    }

    [Fact]
    public void Tallies_are_shown_during_Guessing_when_the_setting_is_off()
    {
        var spec = Presenter().Present(
            State(PhaseEnum.Guessing, choices: ["A", "B"], tallies: [3, 1]),
            Settings(hideTallies: false),
            Hd);

        spec.ShowTallies.Should().BeTrue();
        spec.Choices[0].CountText.Should().Be("3");
        spec.Choices[0].IsLeader.Should().BeTrue();
        spec.Choices[1].IsLeader.Should().BeFalse();
    }

    [Fact]
    public void Tallies_are_shown_on_Reveal_even_with_the_setting_on()
    {
        var spec = Presenter().Present(
            State(PhaseEnum.Reveal, choices: ["A", "B"], tallies: [3, 1]),
            Settings(hideTallies: true),
            Hd);

        spec.ShowTallies.Should().BeTrue();
        spec.Choices[0].CountText.Should().Be("3");
    }

    [Fact]
    public void Tied_options_are_both_leaders()
    {
        var spec = Presenter().Present(
            State(PhaseEnum.Reveal, choices: ["A", "B", "C"], tallies: [2, 2, 1]),
            Settings(),
            Hd);

        spec.Choices[0].IsLeader.Should().BeTrue();
        spec.Choices[1].IsLeader.Should().BeTrue();
        spec.Choices[2].IsLeader.Should().BeFalse();
    }

    [Fact]
    public void With_no_answers_yet_nothing_is_a_leader()
    {
        var spec = Presenter().Present(
            State(PhaseEnum.Reveal, choices: ["A", "B"], tallies: [0, 0]),
            Settings(),
            Hd);

        spec.Choices.Should().OnlyContain(c => !c.IsLeader);
    }

    [Fact]
    public void Unused_option_slots_are_not_visible()
    {
        var spec = Presenter().Present(
            State(PhaseEnum.Guessing, choices: ["A", "B"], tallies: [0, 0]),
            Settings(),
            Hd);

        spec.Choices.Should().HaveCount(PhasePresenter.ChoiceSlots);
        spec.Choices[0].IsVisible.Should().BeTrue();
        spec.Choices[1].IsVisible.Should().BeTrue();
        spec.Choices[2].IsVisible.Should().BeFalse();
        spec.Choices[3].IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Choices_missing_a_tally_entry_count_as_zero()
    {
        // The Backend pads tallies to the choice count, but a snapshot mid-change may be short.
        var spec = Presenter().Present(
            State(PhaseEnum.Reveal, choices: ["A", "B", "C"], tallies: [5]),
            Settings(),
            Hd);

        spec.Choices[1].CountText.Should().Be("0");
        spec.Choices[2].CountText.Should().Be("0");
    }

    [Fact]
    public void An_unsafe_choice_is_filtered_before_it_reaches_the_screen()
    {
        var spec = Presenter().Present(
            State(PhaseEnum.Guessing, choices: ["<script>alert('x')</script>"], tallies: [0]),
            Settings(),
            Hd);

        spec.Choices[0].Text.Should().NotContain("<script>");
    }

    [Fact]
    public void A_missing_song_reads_as_unknown_rather_than_blank()
    {
        var spec = Presenter().Present(State(PhaseEnum.Play), Settings(), Hd);

        spec.SongTitle.Should().Be("Unknown Song");
        spec.SongArtist.Should().Be("Unknown Artist");
    }

    [Fact]
    public void The_session_code_is_shown_upper_case()
        => Presenter().Present(State(PhaseEnum.Lobby), Settings(), Hd)
            .SessionCodeDisplay.Should().Be("S1");

    [Theory]
    [InlineData(0, "Waiting for players...")]
    [InlineData(1, "1 player joined")]
    [InlineData(4, "4 players joined")]
    public void The_lobby_counts_players(int players, string expected)
    {
        var scores = Enumerable.Range(0, players).ToDictionary(i => $"p{i}", _ => 0);

        Presenter().Present(State(PhaseEnum.Lobby, scores: scores), Settings(), Hd)
            .PlayerCountText.Should().Be(expected);
    }

    [Fact]
    public void The_hint_counter_is_one_based()
    {
        var presenter = Presenter();

        presenter.Present(State(PhaseEnum.Hint, hintIndex: 0), Settings(), Hd)
            .HintCounterText.Should().Be("Hint 1 of 3");
        presenter.Present(State(PhaseEnum.Hint, hintIndex: 2), Settings(), Hd)
            .HintCounterText.Should().Be("Hint 3 of 3");
    }

    [Fact]
    public void One_hint_is_listed_per_hint_revealed()
    {
        var spec = Presenter().Present(State(PhaseEnum.Hint, hintIndex: 1), Settings(), Hd);

        spec.Hints.Should().HaveCount(2);
        spec.Hints.Should().OnlyContain(h => !string.IsNullOrWhiteSpace(h));
    }

    [Fact]
    public void The_scoreboard_is_ordered_by_score_and_positioned_from_one()
    {
        var spec = Presenter().Present(
            State(PhaseEnum.Intermission, scores: new Dictionary<string, int>
            {
                ["alice"] = 3,
                ["bob"] = 9,
                ["carol"] = 5
            }),
            Settings(),
            Hd);

        spec.ScoreRows.Select(r => r.Player).Should().Equal("bob", "carol", "alice");
        spec.ScoreRows.Select(r => r.Position).Should().Equal(1, 2, 3);
        spec.ScoreRows[0].Score.Should().Be(9);
    }

    [Fact]
    public void The_last_song_ends_with_final_results()
    {
        var catalog = new[]
        {
            new SongRef(new SongId("a"), "A", "x"),
            new SongRef(new SongId("b"), "B", "y")
        };
        var presenter = Presenter();

        presenter.Present(State(PhaseEnum.Intermission, songIndex: 0, catalog: catalog), Settings(), Hd)
            .ScoreboardFooter.Should().Be("Get ready for the next song!");
        presenter.Present(State(PhaseEnum.Intermission, songIndex: 1, catalog: catalog), Settings(), Hd)
            .ScoreboardFooter.Should().Be("Final Results!");
    }

    [Fact]
    public void The_scoreboard_header_counts_songs_from_one()
        => Presenter().Present(State(PhaseEnum.Intermission, songIndex: 2), Settings(), Hd)
            .ScoreboardHeader.Should().Be("After Song 3");

    [Fact]
    public void Font_sizes_grow_with_the_window()
    {
        var presenter = Presenter();

        var hd = presenter.Present(State(PhaseEnum.Guessing), Settings(), Hd).Typography;
        var uhd = presenter.Present(State(PhaseEnum.Guessing), Settings(), Uhd).Typography;

        uhd.Headline.Should().BeGreaterThan(hd.Headline);
        uhd.Option.Should().BeGreaterThanOrEqualTo(hd.Option);
    }

    [Fact]
    public void Font_sizes_stay_within_their_declared_bounds()
    {
        var tiny = Presenter().Present(State(PhaseEnum.Guessing), Settings(), new WindowSize(640, 360)).Typography;
        var huge = Presenter().Present(State(PhaseEnum.Guessing), Settings(), new WindowSize(7680, 4320)).Typography;

        tiny.Headline.Should().BeGreaterThanOrEqualTo(ResponsiveTypographyService.FontSizes.HeadlineMin);
        huge.Headline.Should().BeLessThanOrEqualTo(ResponsiveTypographyService.FontSizes.HeadlineMax);
        tiny.Option.Should().BeGreaterThanOrEqualTo(ResponsiveTypographyService.FontSizes.OptionMin);
        huge.Option.Should().BeLessThanOrEqualTo(ResponsiveTypographyService.FontSizes.OptionMax);
    }

    [Fact]
    public void Every_visible_phase_has_a_human_readable_headline()
    {
        var presenter = Presenter();

        foreach (var phase in Enum.GetValues<PhaseEnum>().Where(p => p != PhaseEnum.Idle))
        {
            presenter.Present(State(phase), Settings(), Hd).PhaseHeadline
                .Should().NotBeNullOrWhiteSpace($"{phase} needs headline text");
        }

        // Spot-check the wording rather than asserting "not the enum name": Phase.Hint's headline is
        // legitimately the word "Hint".
        presenter.Present(State(PhaseEnum.Lock), Settings(), Hd).PhaseHeadline.Should().Be("Time's up!");
        presenter.Present(State(PhaseEnum.Finished), Settings(), Hd).PhaseHeadline.Should().Be("Game Over!");
        presenter.Present(State(PhaseEnum.Hint), Settings(), Hd).PhaseHeadline.Should().Be("Hint");
    }

    [Fact]
    public void A_missing_translation_never_leaks_its_key_onto_the_screen()
    {
        // LocalizationService returns "[key]" for a miss; that must not reach an audience.
        var spec = Presenter().Present(State(PhaseEnum.Guessing), Settings(), Hd);

        spec.Question.Should().Be("What song is this?");
        spec.Question.Should().NotStartWith("[");
    }
}
