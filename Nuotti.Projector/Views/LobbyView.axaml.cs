using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nuotti.Projector.Models;
using Nuotti.Projector.Services;

namespace Nuotti.Projector.Views;

public partial class LobbyView : PhaseViewBase
{
    private readonly TextBlock _welcomeText;
    private readonly TextBlock _sessionCodeLabel;
    private readonly TextBlock _sessionCodeText;
    private readonly TextBlock _instructionsText;
    private readonly TextBlock _playerCountText;

    public LobbyView()
    {
        InitializeComponent();
        _welcomeText = this.FindControl<TextBlock>("WelcomeText")!;
        _sessionCodeLabel = this.FindControl<TextBlock>("SessionCodeLabel")!;
        _sessionCodeText = this.FindControl<TextBlock>("SessionCodeText")!;
        _instructionsText = this.FindControl<TextBlock>("InstructionsText")!;
        _playerCountText = this.FindControl<TextBlock>("PlayerCountText")!;
    }

    public override void UpdateState(GameState state)
    {
        _sessionCodeText.Text = state.SessionCode.ToUpperInvariant();

        var playerCount = state.Scores.Count;
        _playerCountText.Text = playerCount switch
        {
            0 => "Waiting for players...",
            1 => "1 player joined",
            _ => $"{playerCount} players joined"
        };

        // Update responsive font sizes
        UpdateResponsiveFontSizes();
    }

    protected override void UpdateResponsiveFontSizes()
    {
        var windowSize = GetWindowSize();
        var safeAreaMargin = 0.05; // 5% default

        // Welcome text (headline)
        _welcomeText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.HeadlineMin,
            ResponsiveTypographyService.FontSizes.HeadlineMax,
            windowSize,
            safeAreaMargin);

        // Session code label
        _sessionCodeLabel.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.BodyMin * 1.5,
            ResponsiveTypographyService.FontSizes.BodyMax * 1.5,
            windowSize,
            safeAreaMargin);

        // Session code text
        _sessionCodeText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.PhaseTitleMin,
            ResponsiveTypographyService.FontSizes.PhaseTitleMax,
            windowSize,
            safeAreaMargin);

        // Instructions text
        _instructionsText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.BodyMin,
            ResponsiveTypographyService.FontSizes.BodyMax,
            windowSize,
            safeAreaMargin);

        // Player count text
        _playerCountText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.BodyMin,
            ResponsiveTypographyService.FontSizes.BodyMax,
            windowSize,
            safeAreaMargin);
    }

    protected override void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
