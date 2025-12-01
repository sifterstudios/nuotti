using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Projector.Models;
using Nuotti.Projector.Services;

namespace Nuotti.Projector.Views;

public partial class SimplePhaseView : PhaseViewBase
{
    private readonly TextBlock _phaseIconText;
    private readonly TextBlock _phaseTitleText;
    private readonly StackPanel _songInfoPanel;
    private readonly TextBlock _songTitleText;
    private readonly TextBlock _songArtistText;
    private readonly TextBlock _additionalInfoText;

    public SimplePhaseView()
    {
        InitializeComponent();

        _phaseIconText = this.FindControl<TextBlock>("PhaseIconText")!;
        _phaseTitleText = this.FindControl<TextBlock>("PhaseTitleText")!;
        _songInfoPanel = this.FindControl<StackPanel>("SongInfoPanel")!;
        _songTitleText = this.FindControl<TextBlock>("SongTitleText")!;
        _songArtistText = this.FindControl<TextBlock>("SongArtistText")!;
        _additionalInfoText = this.FindControl<TextBlock>("AdditionalInfoText")!;
    }

    protected override void UpdateResponsiveFontSizes()
    {
        var windowSize = GetWindowSize();
        var safeAreaMargin = 0.05; // 5% default

        _phaseIconText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.PhaseIconMin,
            ResponsiveTypographyService.FontSizes.PhaseIconMax,
            windowSize,
            safeAreaMargin);

        _phaseTitleText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.PhaseTitleMin,
            ResponsiveTypographyService.FontSizes.PhaseTitleMax,
            windowSize,
            safeAreaMargin);

        _songTitleText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.SongTitleMin,
            ResponsiveTypographyService.FontSizes.SongTitleMax,
            windowSize,
            safeAreaMargin);

        _songArtistText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.SongArtistMin,
            ResponsiveTypographyService.FontSizes.SongArtistMax,
            windowSize,
            safeAreaMargin);

        _additionalInfoText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.BodyMin,
            ResponsiveTypographyService.FontSizes.BodyMax,
            windowSize,
            safeAreaMargin);
    }

    public override void UpdateState(GameState state)
    {
        UpdateForPhase(state.Phase, state);
        UpdateResponsiveFontSizes();
    }

    public void UpdateForPhase(Phase phase, GameState state)
    {
        var (icon, title, showSong, additionalInfo) = GetPhaseInfo(phase, state);

        _phaseIconText.Text = icon;
        _phaseTitleText.Text = title;

        _songInfoPanel.IsVisible = showSong && state.CurrentSong != null;
        if (showSong && state.CurrentSong != null)
        {
            _songTitleText.Text = state.CurrentSongTitle;
            _songArtistText.Text = state.CurrentSongArtist;
        }

        _additionalInfoText.IsVisible = !string.IsNullOrEmpty(additionalInfo);
        _additionalInfoText.Text = additionalInfo ?? "";
    }

    private (string Icon, string Title, bool ShowSong, string? AdditionalInfo) GetPhaseInfo(Phase phase, GameState state)
    {
        return phase switch
        {
            Phase.Start => ("🚀", "Get Ready!", true, $"Song {state.SongIndex + 1}"),
            Phase.Hint => ("💡", "Hint Time", true, $"Hint {state.HintIndex + 1}"),
            Phase.Lock => ("🔒", "Time's Up!", true, "No more answers!"),
            Phase.Reveal => ("🎉", "The Answer Is...", true, null),
            Phase.Play => ("🎵", "Now Playing", true, null),
            Phase.Intermission => ("📊", "Scoreboard", false, "Check your score!"),
            Phase.Finished => ("🏆", "Game Over!", false, "Thanks for playing!"),
            _ => ("🎵", phase.ToString(), false, null)
        };
    }

    protected override void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
