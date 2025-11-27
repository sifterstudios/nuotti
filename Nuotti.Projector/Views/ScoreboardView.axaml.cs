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
    private readonly TextBlock _songInfoText;
    private readonly TextBlock _footerText;
    private readonly StackPanel _scoreboardPanel;
    private readonly ScrollViewer _scoreboardScrollViewer;
    private readonly AnimationService _animationService;
    
    private const int MaxPlayersToShow = 15;
    
    public ScoreboardView()
    {
        InitializeComponent();
        
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
        
        // Update scoreboard
        UpdateScoreboard(state);
    }
    
    private void UpdateScoreboard(GameState state)
    {
        // Clear existing entries
        _scoreboardPanel.Children.Clear();
        
        if (!state.HasScores)
        {
            // Show "no players" message
            var noPlayersText = new TextBlock
            {
                Text = "No players yet...",
                FontSize = 24,
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
        
        // Position
        var positionText = new TextBlock
        {
            Text = positionIcon ?? position.ToString(),
            FontSize = position <= 3 ? 32 : 24,
            FontWeight = FontWeight.Bold,
            Foreground = textBrush,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };
        Grid.SetColumn(positionText, 0);
        
        // Player name
        var nameText = new TextBlock
        {
            Text = displayName,
            FontSize = 24,
            FontWeight = FontWeight.Medium,
            Foreground = GetBrush("TextPrimaryBrush"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(nameText, 1);
        
        // Score change (if any)
        if (change != 0)
        {
            var changeText = new TextBlock
            {
                Text = change > 0 ? $"+{change}" : change.ToString(),
                FontSize = 18,
                FontWeight = FontWeight.Medium,
                Foreground = change > 0 ? GetBrush("SuccessBrush") : GetBrush("ErrorBrush"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(8, 0)
            };
            Grid.SetColumn(changeText, 2);
            grid.Children.Add(changeText);
        }
        
        // Total score
        var scoreText = new TextBlock
        {
            Text = score.ToString(),
            FontSize = 28,
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
