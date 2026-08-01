using System.Collections.Generic;

namespace Nuotti.Projector.Presentation;

/// <summary>
/// Which phase view the Projector shows.
/// </summary>
public enum PhaseView
{
    /// <summary>Nothing is shown — the session is Idle.</summary>
    None,
    Lobby,
    Guessing,
    Hint,
    Scoreboard,

    /// <summary>The shared icon-and-title view used for phases without a bespoke screen.</summary>
    Simple
}

/// <summary>One answer option as it should appear on screen.</summary>
/// <param name="Text">The choice text, already safety-filtered.</param>
/// <param name="CountText">What to print for the tally — a number, or "?" while tallies are hidden.</param>
/// <param name="IsVisible">False for option slots with no choice behind them.</param>
/// <param name="IsLeader">True when this option is tied for the most answers and tallies are shown.</param>
public sealed record ChoiceSpec(string Text, string CountText, bool IsVisible, bool IsLeader);

/// <summary>One scoreboard row as it should appear on screen.</summary>
public sealed record ScoreRowSpec(int Position, string Player, int Score);

/// <summary>
/// The icon-and-title screen used for phases without a bespoke view.
/// </summary>
/// <param name="Icon">Emoji shown above the title.</param>
/// <param name="Title">Large phase title.</param>
/// <param name="ShowSong">Whether the song panel is shown at all.</param>
/// <param name="Detail">Optional line under the title; empty means hidden.</param>
public sealed record SimpleSpec(string Icon, string Title, bool ShowSong, string Detail);

/// <summary>
/// Font sizes for one window size, resolved once so views do not each measure the visual tree.
/// </summary>
public sealed record TypographySpec(
    double Headline,
    double Question,
    double Option,
    double SongTitle,
    double SongArtist,
    double Body);

/// <summary>
/// A complete description of one Projector screen: which view, and every string, flag and size it
/// needs. Contains no Avalonia types, so it can be asserted on without a window.
/// </summary>
/// <remarks>
/// MainWindow is the adapter that realises a ViewSpec into controls. Before this existed, the
/// decisions below were spread across MainWindow's 1388 lines and eight UserControls, reachable only
/// by launching the app.
/// </remarks>
public sealed record ViewSpec(
    PhaseView View,
    bool Visible,
    string SessionCodeDisplay,
    string PhaseHeadline,
    string SongTitle,
    string SongArtist,
    string Question,
    bool ShowTallies,
    IReadOnlyList<ChoiceSpec> Choices,
    string PlayerCountText,
    string HintCounterText,
    IReadOnlyList<string> Hints,
    IReadOnlyList<ScoreRowSpec> ScoreRows,
    string ScoreboardHeader,
    string ScoreboardFooter,
    SimpleSpec Simple,
    bool HasSong,
    TypographySpec Typography,
    string? ActiveLyricLine = null,
    bool ConnectionDegraded = false,
    bool EmitsAudio = false);
