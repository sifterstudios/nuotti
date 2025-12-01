using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Nuotti.Projector.Models;
using Nuotti.Projector.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nuotti.Projector.Views;

public partial class ScoreboardView : PhaseViewBase
{
    private readonly TextBlock _scoreboardTitleText;
    private readonly TextBlock _songInfoText;
    private readonly TextBlock _footerText;
    private readonly StackPanel _scoreboardPanel;
    private readonly ScrollViewer _scoreboardScrollViewer;
    private readonly AnimationService _animationService;
    
    private const int MaxPlayersToShow = 15;
    
    public ScoreboardView()
    {
        InitializeComponent();
        
        _scoreboardTitleText = this.FindControl<TextBlock>("ScoreboardTitleText")!;
        _songInfoText = this.FindControl<TextBlock>("SongInfoText")!;
        _footerText = this.FindControl<TextBlock>("FooterText")!;
        _scoreboardPanel = this.FindControl<StackPanel>("ScoreboardPanel")!;
        _scoreboardScrollViewer = this.FindControl<ScrollViewer>("ScoreboardScrollViewer")!;
        _animationService = new AnimationService();
    }
    
    public override void UpdateState(GameState state)
    {
        // Update header info
        _songInfoText.Text = $"After Song {state.SongIndex + 1}";
        
        // Update footer
        var totalSongs = state.Catalog.Count;
        if (state.SongIndex + 1 >= totalSongs)
        {
            _footerText.Text = "Final Results!";
        }
        else
        {
            _footerText.Text = "Get ready for the next song!";
        }
        
        // Update responsive font sizes
        UpdateResponsiveFontSizes();
        
        // Update scoreboard
        UpdateScoreboard(state);
    }
    
    protected override void UpdateResponsiveFontSizes()
    {
        var windowSize = GetWindowSize();
        var safeAreaMargin = 0.05; // 5% default
        
        // Header "Scoreboard" text
        _scoreboardTitleText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.PhaseTitleMin,
            ResponsiveTypographyService.FontSizes.PhaseTitleMax,
            windowSize,
            safeAreaMargin);
        
        // Song info text
        _songInfoText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.BodyMin,
            ResponsiveTypographyService.FontSizes.BodyMax,
            windowSize,
            safeAreaMargin);
        
        // Footer text
        _footerText.FontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.BodyMin,
            ResponsiveTypographyService.FontSizes.BodyMax,
            windowSize,
            safeAreaMargin);
    }
    
    private void UpdateScoreboard(GameState state)
    {
        // Clear existing entries
        _scoreboardPanel.Children.Clear();
        
        if (!state.HasScores)
        {
            // Show "no players" message
            var windowSize = GetWindowSize();
            var safeAreaMargin = 0.05;
            var noPlayersFontSize = TypographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.BodyMin,
                ResponsiveTypographyService.FontSizes.BodyMax,
                windowSize,
                safeAreaMargin);
            
            var noPlayersText = new TextBlock
            {
                Text = "No players yet...",
                FontSize = noPlayersFontSize,
                Foreground = GetBrush("TextSecondaryBrush"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 32)
            };
            _scoreboardPanel.Children.Add(noPlayersText);
            return;
        }
        
        // Get top players
        var topPlayers = state.GetTopPlayers(MaxPlayersToShow);
        
        for (int i = 0; i < topPlayers.Count; i++)
        {
            var (player, score, change) = topPlayers[i];
            var position = i + 1;
            
            var playerEntry = CreatePlayerEntry(position, player, score, change);
            _scoreboardPanel.Children.Add(playerEntry);
            
            // Animate entry appearance
            _ = _animationService.AnimateSlideIn(playerEntry);
        }
        
        // Auto-scroll if there are many players
        if (topPlayers.Count > 8)
        {
            _ = Task.Delay(2000).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StartAutoScroll();
                });
            });
        }
    }
    
    private Border CreatePlayerEntry(int position, string playerName, int score, int change)
    {
        // Determine position styling
        var (bgBrush, textBrush, positionIcon) = GetPositionStyling(position);
        
        // Truncate long names with ellipsis
        var displayName = TruncateName(playerName, 20);
        
        var entry = new Border
        {
            Background = bgBrush,
            BorderBrush = GetBrush("DividerBrush"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20, 12),
            Margin = new Thickness(0, 4)
        };
        
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto")
        };
        
        var windowSize = GetWindowSize();
        var safeAreaMargin = 0.05;
        
        // Position
        var positionFontSize = position <= 3 
            ? TypographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.ScorePositionMin * 1.2,
                ResponsiveTypographyService.FontSizes.ScorePositionMax * 1.2,
                windowSize,
                safeAreaMargin)
            : TypographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.ScorePositionMin,
                ResponsiveTypographyService.FontSizes.ScorePositionMax,
                windowSize,
                safeAreaMargin);
        
        var positionText = new TextBlock
        {
            Text = positionIcon ?? position.ToString(),
            FontSize = positionFontSize,
            FontWeight = FontWeight.Bold,
            Foreground = textBrush,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };
        Grid.SetColumn(positionText, 0);
        
        // Player name
        var nameFontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.ScoreNameMin,
            ResponsiveTypographyService.FontSizes.ScoreNameMax,
            windowSize,
            safeAreaMargin);
        
        var nameText = new TextBlock
        {
            Text = displayName,
            FontSize = nameFontSize,
            FontWeight = FontWeight.Medium,
            Foreground = GetBrush("TextPrimaryBrush"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(nameText, 1);
        
        // Score change (if any)
        if (change != 0)
        {
            var changeFontSize = TypographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.BodyMin,
                ResponsiveTypographyService.FontSizes.BodyMax,
                windowSize,
                safeAreaMargin);
            
            var changeText = new TextBlock
            {
                Text = change > 0 ? $"+{change}" : change.ToString(),
                FontSize = changeFontSize,
                FontWeight = FontWeight.Medium,
                Foreground = change > 0 ? GetBrush("SuccessBrush") : GetBrush("ErrorBrush"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(8, 0)
            };
            Grid.SetColumn(changeText, 2);
            grid.Children.Add(changeText);
        }
        
        // Total score
        var scoreFontSize = TypographyService.CalculateFontSizeFromWindow(
            ResponsiveTypographyService.FontSizes.ScoreValueMin,
            ResponsiveTypographyService.FontSizes.ScoreValueMax,
            windowSize,
            safeAreaMargin);
        
        var scoreText = new TextBlock
        {
            Text = score.ToString(),
            FontSize = scoreFontSize,
            FontWeight = FontWeight.Bold,
            Foreground = textBrush,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(scoreText, 3);
        
        grid.Children.Add(positionText);
        grid.Children.Add(nameText);
        grid.Children.Add(scoreText);
        
        entry.Child = grid;
        return entry;
    }
    
    private (IBrush BgBrush, IBrush TextBrush, string? Icon) GetPositionStyling(int position)
    {
        return position switch
        {
            1 => (GetBrush("SuccessBrush"), Brushes.White, "🥇"),
            2 => (GetBrush("SecondaryBrush"), Brushes.White, "🥈"),
            3 => (GetBrush("TertiaryBrush"), Brushes.White, "🥉"),
            _ => (GetBrush("SurfaceBrush"), GetBrush("TextPrimaryBrush"), null)
        };
    }
    
    private string TruncateName(string name, int maxLength)
    {
        if (name.Length <= maxLength) return name;
        return name.Substring(0, maxLength - 3) + "...";
    }
    
    private IBrush GetBrush(string resourceKey)
    {
        if (Application.Current?.Resources.TryGetResource(resourceKey, Application.Current?.ActualThemeVariant, out var brush) == true && brush is IBrush b)
            return b;
        
        // Fallback colors
        return resourceKey switch
        {
            "SuccessBrush" => new SolidColorBrush(Color.Parse("#46B283")),
            "SecondaryBrush" => new SolidColorBrush(Color.Parse("#004E89")),
            "TertiaryBrush" => new SolidColorBrush(Color.Parse("#1B9AAA")),
            "ErrorBrush" => new SolidColorBrush(Color.Parse("#EF476F")),
            "SurfaceBrush" => new SolidColorBrush(Color.Parse("#FFFFFF")),
            "TextPrimaryBrush" => new SolidColorBrush(Color.Parse("#1A1A1A")),
            "TextSecondaryBrush" => new SolidColorBrush(Color.Parse("#666666")),
            "DividerBrush" => new SolidColorBrush(Color.Parse("#E0E0E0")),
            _ => Brushes.Gray
        };
    }
    
    private async void StartAutoScroll()
    {
        try
        {
            var maxOffset = _scoreboardScrollViewer.ScrollBarMaximum.Y;
            if (maxOffset <= 0) return;
            
            // Scroll down slowly
            var scrollDuration = TimeSpan.FromSeconds(3);
            var startOffset = _scoreboardScrollViewer.Offset.Y;
            var targetOffset = maxOffset;
            
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < scrollDuration)
            {
                var progress = (DateTime.UtcNow - startTime).TotalMilliseconds / scrollDuration.TotalMilliseconds;
                var currentOffset = startOffset + (targetOffset - startOffset) * progress;
                
                _scoreboardScrollViewer.Offset = _scoreboardScrollViewer.Offset.WithY(currentOffset);
                
                await Task.Delay(16); // ~60fps
            }
            
            // Pause at bottom
            await Task.Delay(2000);
            
            // Scroll back to top
            _scoreboardScrollViewer.Offset = _scoreboardScrollViewer.Offset.WithY(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-scroll failed: {ex.Message}");
        }
    }
    
    protected override void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
