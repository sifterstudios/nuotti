using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Projector.Models;
using Nuotti.Projector.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nuotti.Projector.Presentation;

/// <summary>
/// Decides what the Projector shows. Takes the session snapshot, the Projector's settings and the
/// window size, and returns a fully-derived <see cref="ViewSpec"/>.
/// </summary>
/// <remarks>
/// Pure with respect to the window: it touches no control and no visual tree, so every rule below is
/// testable by calling one method. Previously these rules lived in MainWindow.InitializePhaseViews,
/// MainWindow.SwitchToPhaseView, GameStateService, and inside each of the eight views.
/// </remarks>
public sealed class PhasePresenter(
    ContentSafetyService safety,
    LocalizationService localization,
    ResponsiveTypographyService typography)
{
    /// <summary>How many hints a song is assumed to have when the manifest does not say.</summary>
    const int AssumedHintCount = 3;

    /// <summary>Option slots the Projector renders.</summary>
    public const int ChoiceSlots = 4;

    public ViewSpec Present(GameStateSnapshot state, ProjectorSettings settings, WindowSize windowSize)
    {
        var showTallies = !(settings.HideTalliesUntilReveal && state.Phase == Phase.Guessing);
        var songTitle = safety.SanitizeSongTitle(state.SongTitle()).SafeContent;
        var songArtist = safety.SanitizeArtistName(state.SongArtist()).SafeContent;

        return new ViewSpec(
            View: ViewFor(state.Phase),
            Visible: state.Phase != Phase.Idle,
            SessionCodeDisplay: safety.SanitizeText(state.SessionCode, ContentType.General).SafeContent.ToUpperInvariant(),
            PhaseHeadline: HeadlineFor(state.Phase),
            SongTitle: songTitle,
            SongArtist: songArtist,
            Question: Localized("guessing.question", "What song is this?"),
            ShowTallies: showTallies,
            Choices: ChoicesFor(state, showTallies),
            PlayerCountText: PlayerCountFor(state),
            HintCounterText: HintCounterFor(state),
            Hints: HintsFor(state, songTitle),
            ScoreRows: ScoreRowsFor(state),
            ScoreboardHeader: $"After Song {state.SongIndex + 1}",
            ScoreboardFooter: state.SongIndex + 1 >= state.Catalog.Count
                ? "Final Results!"
                : "Get ready for the next song!",
            Simple: SimpleFor(state),
            HasSong: state.CurrentSong is not null,
            Typography: TypographyFor(windowSize));
    }

    /// <summary>
    /// The phase-to-view mapping, previously a dictionary built in MainWindow's constructor.
    /// </summary>
    static PhaseView ViewFor(Phase phase) => phase switch
    {
        Phase.Idle => PhaseView.None,
        Phase.Lobby => PhaseView.Lobby,
        Phase.Guessing => PhaseView.Guessing,
        Phase.Hint => PhaseView.Hint,
        Phase.Intermission => PhaseView.Scoreboard,
        _ => PhaseView.Simple
    };

    /// <summary>
    /// A translation, or the given English text when the key is missing. LocalizationService returns
    /// "[key]" for a miss, which must never reach a screen an audience is looking at.
    /// </summary>
    string Localized(string key, string fallback)
    {
        var value = localization.GetString(key);
        return value == $"[{key}]" ? fallback : value;
    }

    string HeadlineFor(Phase phase) => phase switch
    {
        Phase.Lobby => "Waiting for players...",
        Phase.Start => "Get ready!",
        Phase.Hint => "Hint",
        Phase.Guessing => "Submit your answers!",
        Phase.Lock => "Time's up!",
        Phase.Reveal => "The answer is...",
        Phase.Play => "Now playing",
        Phase.Intermission => "Scoreboard",
        Phase.Finished => "Game Over!",
        _ => phase.ToString()
    };

    IReadOnlyList<ChoiceSpec> ChoicesFor(GameStateSnapshot state, bool showTallies)
    {
        var max = state.Tallies.Count == 0 ? 0 : state.Tallies.Max();
        var specs = new List<ChoiceSpec>(ChoiceSlots);

        for (var i = 0; i < ChoiceSlots; i++)
        {
            if (i >= state.Choices.Count)
            {
                specs.Add(new ChoiceSpec(string.Empty, string.Empty, IsVisible: false, IsLeader: false));
                continue;
            }

            var count = i < state.Tallies.Count ? state.Tallies[i] : 0;
            specs.Add(new ChoiceSpec(
                Text: safety.SanitizeChoice(state.Choices[i], i).SafeContent,
                CountText: showTallies ? count.ToString() : "?",
                IsVisible: true,
                IsLeader: showTallies && max > 0 && count == max));
        }

        return specs;
    }

    string PlayerCountFor(GameStateSnapshot state) => state.Scores.Count switch
    {
        0 => "Waiting for players...",
        1 => "1 player joined",
        var n => $"{n} players joined"
    };

    string HintCounterFor(GameStateSnapshot state)
        => AssumedHintCount > 0
            ? $"Hint {state.CurrentHintNumber()} of {AssumedHintCount}"
            : $"Hint {state.CurrentHintNumber()}";

    /// <summary>
    /// Hint text for every hint revealed so far. The manifest does not reach the Projector yet, so
    /// these are placeholders derived from the song — the same ones HintView generated inline.
    /// </summary>
    IReadOnlyList<string> HintsFor(GameStateSnapshot state, string songTitle)
    {
        var count = state.CurrentHintNumber();
        var hints = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            var text = i switch
            {
                0 => $"This song has {songTitle.Length} characters in its title",
                1 => $"The artist's name starts with '{FirstLetterOf(state.SongArtist())}'",
                _ => "Listen carefully to the melody"
            };
            hints.Add(safety.SanitizeHint(text).SafeContent);
        }

        return hints;
    }

    static string FirstLetterOf(string value)
        => string.IsNullOrWhiteSpace(value) ? "?" : value.Trim()[..1].ToUpperInvariant();

    /// <summary>
    /// The icon-and-title screen, previously derived inside SimplePhaseView.GetPhaseInfo.
    /// </summary>
    static SimpleSpec SimpleFor(GameStateSnapshot state) => state.Phase switch
    {
        Phase.Start => new("\U0001F680", "Get Ready!", true, $"Song {state.SongIndex + 1}"),
        Phase.Hint => new("\U0001F4A1", "Hint Time", true, $"Hint {state.CurrentHintNumber()}"),
        Phase.Lock => new("\U0001F512", "Time's Up!", true, "No more answers!"),
        Phase.Reveal => new("\U0001F389", "The Answer Is...", true, string.Empty),
        Phase.Play => new("\U0001F3B5", "Now Playing", true, string.Empty),
        Phase.Intermission => new("\U0001F4CA", "Scoreboard", false, "Check your score!"),
        Phase.Finished => new("\U0001F3C6", "Game Over!", false, "Thanks for playing!"),
        _ => new("\U0001F3B5", state.Phase.ToString(), false, string.Empty)
    };

    IReadOnlyList<ScoreRowSpec> ScoreRowsFor(GameStateSnapshot state)
        => state.TopPlayers()
            .Select(row => new ScoreRowSpec(
                row.Rank,
                safety.SanitizePlayerName(row.Player).SafeContent,
                row.Score))
            .ToArray();

    TypographySpec TypographyFor(WindowSize windowSize) => new(
        Headline: typography.CalculateFontSizeFromWindow(ResponsiveTypographyService.FontSizes.HeadlineMin, ResponsiveTypographyService.FontSizes.HeadlineMax, windowSize),
        Question: typography.CalculateFontSizeFromWindow(ResponsiveTypographyService.FontSizes.QuestionMin, ResponsiveTypographyService.FontSizes.QuestionMax, windowSize),
        Option: typography.CalculateFontSizeFromWindow(ResponsiveTypographyService.FontSizes.OptionMin, ResponsiveTypographyService.FontSizes.OptionMax, windowSize),
        SongTitle: typography.CalculateFontSizeFromWindow(ResponsiveTypographyService.FontSizes.SongTitleMin, ResponsiveTypographyService.FontSizes.SongTitleMax, windowSize),
        SongArtist: typography.CalculateFontSizeFromWindow(ResponsiveTypographyService.FontSizes.SongArtistMin, ResponsiveTypographyService.FontSizes.SongArtistMax, windowSize),
        Body: typography.CalculateFontSizeFromWindow(ResponsiveTypographyService.FontSizes.BodyMin, ResponsiveTypographyService.FontSizes.BodyMax, windowSize));
}
