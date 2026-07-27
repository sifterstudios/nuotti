using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Projector.Models;
using Nuotti.Projector.Presentation;
using Nuotti.Contracts.V1.Model;
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

    public override void Apply(ViewSpec spec)
    {
        UpdateForPhase(spec);
        UpdateResponsiveFontSizes();
    }

    void UpdateForPhase(ViewSpec spec)
    {
        _phaseIconText.Text = spec.Simple.Icon;
        _phaseTitleText.Text = spec.Simple.Title;

        _songInfoPanel.IsVisible = spec.Simple.ShowSong && spec.HasSong;
        if (_songInfoPanel.IsVisible)
        {
            _songTitleText.Text = spec.SongTitle;
            _songArtistText.Text = spec.SongArtist;
        }

        _additionalInfoText.IsVisible = !string.IsNullOrEmpty(spec.Simple.Detail);
        _additionalInfoText.Text = spec.Simple.Detail;
    }

    protected override void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
