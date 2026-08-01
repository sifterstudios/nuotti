using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Nuotti.Projector.Models;
using Nuotti.Projector.Presentation;
using Nuotti.Contracts.V1.Model;
using Nuotti.Projector.Services;
using System;
using System.Collections.Generic;

namespace Nuotti.Projector.Views;

public partial class HintView : PhaseViewBase
{
    private readonly TextBlock _hintTitleText;
    private readonly TextBlock _songTitleText;
    private readonly TextBlock _byText;
    private readonly TextBlock _songArtistText;
    private readonly TextBlock _hintCountText;
    private readonly StackPanel _hintsPanel;
    private readonly AnimationService _animationService;
    
    private readonly List<string> _displayedHints = new();
    private int _lastHintIndex = -1;
    
    public HintView()
    {
        InitializeComponent();
        
        _hintTitleText = this.FindControl<TextBlock>("HintTitleText")!;
        _songTitleText = this.FindControl<TextBlock>("SongTitleText")!;
        _byText = this.FindControl<TextBlock>("ByText")!;
        _songArtistText = this.FindControl<TextBlock>("SongArtistText")!;
        _hintCountText = this.FindControl<TextBlock>("HintCountText")!;
        _hintsPanel = this.FindControl<StackPanel>("HintsPanel")!;
        _animationService = new AnimationService();
    }
    
    public override void Apply(ViewSpec spec)
    {
        _songTitleText.Text = spec.SongTitle;
        _songArtistText.Text = spec.SongArtist;

        if (spec.Hints.Count != _displayedHints.Count)
        {
            UpdateHints(spec);
        }

        _hintCountText.Text = spec.HintCounterText;

        UpdateResponsiveFontSizes();
    }
    
    protected override void UpdateResponsiveFontSizes()
    {
        var windowSize = GetWindowSize();
        var safeAreaMargin = 0.05; // 5% default
        
        // Header "Hint Time!"
        _hintTitleText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.PhaseTitleMin,
            ResponsiveTypographyService.FontSizes.PhaseTitleMax,
            windowSize,
            safeAreaMargin);
        
        // Song info
        _songTitleText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.SongTitleMin,
            ResponsiveTypographyService.FontSizes.SongTitleMax,
            windowSize,
            safeAreaMargin);
        
        _byText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.BodyMin,
            ResponsiveTypographyService.FontSizes.BodyMax,
            windowSize,
            safeAreaMargin);
        
        _songArtistText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.SongArtistMin,
            ResponsiveTypographyService.FontSizes.SongArtistMax,
            windowSize,
            safeAreaMargin);
        
        // Hint count
        _hintCountText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.BodyMin,
            ResponsiveTypographyService.FontSizes.BodyMax,
            windowSize,
            safeAreaMargin);
    }
    
    private void UpdateHints(ViewSpec spec)
    {
        // Hint text is derived by PhasePresenter; this only renders what it was handed.
        while (_displayedHints.Count > spec.Hints.Count)
        {
            _displayedHints.RemoveAt(_displayedHints.Count - 1);
            _hintsPanel.Children.RemoveAt(_hintsPanel.Children.Count - 1);
        }

        while (_displayedHints.Count < spec.Hints.Count)
        {
            var index = _displayedHints.Count;
            _displayedHints.Add(spec.Hints[index]);

            var hintElement = CreateHintElement(index + 1, spec.Hints[index]);
            _hintsPanel.Children.Add(hintElement);
            _ = _animationService.AnimateSlideIn(hintElement);
        }
    }

    
    private Border CreateHintElement(int hintNumber, string hintText)
    {
        var hintBorder = new Border
        {
            Background = GetBrush("SurfaceBrush"),
            BorderBrush = GetBrush("PrimaryBrush"),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(24, 16),
            Margin = new Thickness(0, 8)
        };
        
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
        };
        
        // Hint number badge
        var numberBadge = new Border
        {
            Background = GetBrush("PrimaryBrush"),
            CornerRadius = new CornerRadius(0),
            Width = 40,
            Height = 40,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        
        var windowSize = GetWindowSize();
        var safeAreaMargin = 0.05;
        
        var numberFontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.BodyMin,
            ResponsiveTypographyService.FontSizes.BodyMax,
            windowSize,
            safeAreaMargin);
        
        var numberText = new TextBlock
        {
            Text = hintNumber.ToString(),
            FontSize = numberFontSize,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        
        numberBadge.Child = numberText;
        Grid.SetColumn(numberBadge, 0);
        
        // Hint text
        var hintFontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.OptionMin,
            ResponsiveTypographyService.FontSizes.OptionMax,
            windowSize,
            safeAreaMargin);
        
        var hintTextBlock = new TextBlock
        {
            Text = hintText,
            FontSize = hintFontSize,
            FontWeight = FontWeight.Medium,
            Foreground = GetBrush("TextPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(hintTextBlock, 1);
        
        grid.Children.Add(numberBadge);
        grid.Children.Add(hintTextBlock);
        hintBorder.Child = grid;
        
        return hintBorder;
    }
    
    private int GetEstimatedTotalHints(GameStateSnapshot state)
    {
        // In a real implementation, this would come from the song's hint count
        // For now, estimate based on common patterns
        return 3; // Most songs have 2-4 hints
    }
    
    private IBrush GetBrush(string resourceKey)
    {
        if (Application.Current?.Resources.TryGetResource(resourceKey, Application.Current?.ActualThemeVariant, out var brush) == true && brush is IBrush b)
            return b;
        
        // Fallback colors — Variant B dark stage
        return resourceKey switch
        {
            "PrimaryBrush" => new SolidColorBrush(Color.Parse("#00FFF5")),
            "SurfaceBrush" => new SolidColorBrush(Color.Parse("#091115")),
            "TextPrimaryBrush" => new SolidColorBrush(Color.Parse("#ECFFFF")),
            "TextSecondaryBrush" => new SolidColorBrush(Color.Parse("#91A5AA")),
            _ => Brushes.Gray
        };
    }
    
    protected override void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
