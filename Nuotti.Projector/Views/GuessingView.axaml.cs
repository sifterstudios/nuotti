using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Projector.Models;
using Nuotti.Projector.Presentation;
using Nuotti.Contracts.V1.Model;
using Nuotti.Projector.Services;

namespace Nuotti.Projector.Views;

public partial class GuessingView : PhaseViewBase
{
    private readonly TextBlock _songTitleText;
    private readonly TextBlock _songArtistText;
    private readonly TextBlock _questionText;
    private readonly TextBlock[] _optionTexts;
    private readonly TextBlock[] _optionCounts;
    private readonly Border[] _optionBorders;
    private readonly AnimationService _animationService;
    private string[] _lastCountTexts = [];
    private ProjectorSettings? _settings;
    
    public GuessingView()
    {
        InitializeComponent();
        
        _animationService = new AnimationService();
        
        _songTitleText = this.FindControl<TextBlock>("SongTitleText")!;
        _songArtistText = this.FindControl<TextBlock>("SongArtistText")!;
        _questionText = this.FindControl<TextBlock>("QuestionText")!;
        
        _optionTexts = new[]
        {
            this.FindControl<TextBlock>("OptionAText")!,
            this.FindControl<TextBlock>("OptionBText")!,
            this.FindControl<TextBlock>("OptionCText")!,
            this.FindControl<TextBlock>("OptionDText")!
        };
        
        _optionCounts = new[]
        {
            this.FindControl<TextBlock>("OptionACount")!,
            this.FindControl<TextBlock>("OptionBCount")!,
            this.FindControl<TextBlock>("OptionCCount")!,
            this.FindControl<TextBlock>("OptionDCount")!
        };
        
        _optionBorders = new[]
        {
            this.FindControl<Border>("OptionA")!,
            this.FindControl<Border>("OptionB")!,
            this.FindControl<Border>("OptionC")!,
            this.FindControl<Border>("OptionD")!
        };
    }
    
    public override void Apply(ViewSpec spec)
    {
        _songTitleText.Text = spec.SongTitle;
        _songArtistText.Text = spec.SongArtist;
        _questionText.Text = spec.Question;

        for (int i = 0; i < _optionTexts.Length && i < spec.Choices.Count; i++)
        {
            var choice = spec.Choices[i];
            _optionBorders[i].IsVisible = choice.IsVisible;
            if (!choice.IsVisible) continue;

            _optionTexts[i].Text = choice.Text;

            // Animate only when the number actually moved, so a re-render does not replay it.
            var previous = i < _lastCountTexts.Length ? _lastCountTexts[i] : string.Empty;
            if (spec.ShowTallies
                && previous != choice.CountText
                && int.TryParse(previous, out var from)
                && int.TryParse(choice.CountText, out var to))
            {
                _ = _animationService.AnimateCounterUpdate(_optionCounts[i], from, to);
            }
            else
            {
                _optionCounts[i].Text = choice.CountText;
            }
        }

        _lastCountTexts = spec.Choices.Select(c => c.CountText).ToArray();

        HighlightLeaders(spec);

        UpdateResponsiveFontSizes();
    }
    
    
    protected override void UpdateResponsiveFontSizes()
    {
        var windowSize = GetWindowSize();
        var safeAreaMargin = 0.05; // 5% default
        
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
        
        _questionText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.QuestionMin,
            ResponsiveTypographyService.FontSizes.QuestionMax,
            windowSize,
            safeAreaMargin);
        
        // Update option text sizes
        foreach (var optionText in _optionTexts)
        {
            optionText.FontSize = TypographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.OptionMin,
                ResponsiveTypographyService.FontSizes.OptionMax,
                windowSize,
                safeAreaMargin);
        }
        
        // Update option count sizes (slightly smaller than option text)
        foreach (var optionCount in _optionCounts)
        {
            optionCount.FontSize = TypographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.OptionMin * 0.9,
                ResponsiveTypographyService.FontSizes.OptionMax * 0.9,
                windowSize,
                safeAreaMargin);
        }
    }
    
    
    
    private void HighlightLeaders(ViewSpec spec)
    {
        if (spec.Choices.Count == 0) return;
        
        // Get theme brushes
        IBrush? successBrush = null;
        IBrush? defaultBrush = null;
        
        if (Application.Current?.Resources.TryGetResource("SuccessBrush", Application.Current?.ActualThemeVariant, out var successObj) == true && successObj is IBrush s)
            successBrush = s;
        if (Application.Current?.Resources.TryGetResource("OptionBackgroundBrush", Application.Current?.ActualThemeVariant, out var defaultObj) == true && defaultObj is IBrush d)
            defaultBrush = d;
        
        successBrush ??= new SolidColorBrush(Color.Parse("#46B283"));
        defaultBrush ??= new SolidColorBrush(Color.Parse("#F5F5F5"));
        
        // IsLeader is decided by PhasePresenter, which also accounts for hidden tallies and ties.
        for (int i = 0; i < _optionBorders.Length && i < spec.Choices.Count; i++)
        {
            _optionBorders[i].Background = spec.Choices[i].IsLeader ? successBrush : defaultBrush;
        }
    }
    
    protected override void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
