using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Nuotti.Projector.Models;
using Nuotti.Projector.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nuotti.Projector.Views;

public partial class HintView : PhaseViewBase
{
    private readonly TextBlock _songTitleText;
    private readonly TextBlock _songArtistText;
    private readonly TextBlock _hintCountText;
    private readonly StackPanel _hintsPanel;
    private readonly AnimationService _animationService;
    
    private readonly List<string> _displayedHints = new();
    private int _lastHintIndex = -1;
    
    public HintView()
    {
        InitializeComponent();
        
        _songTitleText = this.FindControl<TextBlock>("SongTitleText")!;
        _songArtistText = this.FindControl<TextBlock>("SongArtistText")!;
        _hintCountText = this.FindControl<TextBlock>("HintCountText")!;
        _hintsPanel = this.FindControl<StackPanel>("HintsPanel")!;
        _animationService = new AnimationService();
    }
    
    public override void UpdateState(GameState state)
    {
        // Update song info
        _songTitleText.Text = state.CurrentSongTitle;
        _songArtistText.Text = state.CurrentSongArtist;
        
        // Update hints if hint index changed
        if (state.HintIndex != _lastHintIndex)
        {
            UpdateHints(state);
            _lastHintIndex = state.HintIndex;
        }
        
        // Update hint counter
        var totalHints = GetEstimatedTotalHints(state);
        var currentHintNumber = Math.Max(1, state.HintIndex + 1);
        _hintCountText.Text = totalHints > 0 
            ? $"Hint {currentHintNumber} of {totalHints}"
            : $"Hint {currentHintNumber}";
    }
    
    private void UpdateHints(GameState state)
    {
        // For now, we'll generate placeholder hints since we don't have access to the actual hint content
        // In a real implementation, this would come from the setlist manifest or be passed through events
        
        var hintsToShow = Math.Max(1, state.HintIndex + 1);
        
        // Add new hints if needed
        while (_displayedHints.Count < hintsToShow)
        {
            var hintIndex = _displayedHints.Count;
            var hintText = GeneratePlaceholderHint(hintIndex, state);
            _displayedHints.Add(hintText);
            
            var hintElement = CreateHintElement(hintIndex + 1, hintText);
            _hintsPanel.Children.Add(hintElement);
            
            // Animate the new hint appearance
            _ = _animationService.AnimateSlideIn(hintElement);
        }
    }
    
    private string GeneratePlaceholderHint(int index, GameState state)
    {
        // Generate contextual placeholder hints
        // In a real implementation, these would come from the actual hint data
        return index switch
        {
            0 => "🎵 Listen carefully to the melody...",
            1 => "🎸 Pay attention to the instruments used",
            2 => "🎤 Focus on the vocal style and lyrics",
            3 => "📅 Think about when this song was released",
            4 => "🎭 Consider the genre and mood",
            _ => $"💭 Hint {index + 1}: Keep listening for more clues!"
        };
    }
    
    private Border CreateHintElement(int hintNumber, string hintText)
    {
        var hintBorder = new Border
        {
            Background = GetBrush("SurfaceBrush"),
            BorderBrush = GetBrush("PrimaryBrush"),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(16),
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
            CornerRadius = new CornerRadius(20),
            Width = 40,
            Height = 40,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        
        var numberText = new TextBlock
        {
            Text = hintNumber.ToString(),
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        
        numberBadge.Child = numberText;
        Grid.SetColumn(numberBadge, 0);
        
        // Hint text
        var hintTextBlock = new TextBlock
        {
            Text = hintText,
            FontSize = 28,
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
    
    private int GetEstimatedTotalHints(GameState state)
    {
        // In a real implementation, this would come from the song's hint count
        // For now, estimate based on common patterns
        return 3; // Most songs have 2-4 hints
    }
    
    private IBrush GetBrush(string resourceKey)
    {
        if (Application.Current?.Resources.TryGetResource(resourceKey, Application.Current?.ActualThemeVariant, out var brush) == true && brush is IBrush b)
            return b;
        
        // Fallback colors
        return resourceKey switch
        {
            "PrimaryBrush" => new SolidColorBrush(Color.Parse("#FF6B35")),
            "SurfaceBrush" => new SolidColorBrush(Color.Parse("#FFFFFF")),
            "TextPrimaryBrush" => new SolidColorBrush(Color.Parse("#1A1A1A")),
            "TextSecondaryBrush" => new SolidColorBrush(Color.Parse("#666666")),
            _ => Brushes.Gray
        };
    }
    
    protected override void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
