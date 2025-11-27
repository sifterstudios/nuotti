using System;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Nuotti.Projector.Models;
using Nuotti.Projector.Services;

namespace Nuotti.Projector.Controls;

public partial class DebugOverlay : UserControl
{
    public static readonly StyledProperty<bool> IsDebugVisibleProperty =
        AvaloniaProperty.Register<DebugOverlay, bool>(nameof(IsDebugVisible));
    
    private readonly TextBlock _fpsText;
    private readonly TextBlock _frameTimeText;
    private readonly TextBlock _longestFrameText;
    private readonly TextBlock _animationsText;
    private readonly TextBlock _phaseText;
    private readonly TextBlock _songText;
    private readonly TextBlock _talliesText;
    private readonly TextBlock _connectionIdText;
    private readonly TextBlock _sessionText;
    
    private PerformanceMetrics? _lastPerformanceMetrics;
    private GameState? _lastGameState;
    private string _connectionId = "Unknown";
    
    public bool IsDebugVisible
    {
        get => GetValue(IsDebugVisibleProperty);
        set => SetValue(IsDebugVisibleProperty, value);
    }
    
    public DebugOverlay()
    {
        InitializeComponent();
        DataContext = this;
        
        _fpsText = this.FindControl<TextBlock>("FpsText")!;
        _frameTimeText = this.FindControl<TextBlock>("FrameTimeText")!;
        _longestFrameText = this.FindControl<TextBlock>("LongestFrameText")!;
        _animationsText = this.FindControl<TextBlock>("AnimationsText")!;
        _phaseText = this.FindControl<TextBlock>("PhaseText")!;
        _songText = this.FindControl<TextBlock>("SongText")!;
        _talliesText = this.FindControl<TextBlock>("TalliesText")!;
        _connectionIdText = this.FindControl<TextBlock>("ConnectionIdText")!;
        _sessionText = this.FindControl<TextBlock>("SessionText")!;
    }
    
    public void UpdatePerformanceMetrics(PerformanceMetrics metrics)
    {
        _lastPerformanceMetrics = metrics;
        
        _fpsText.Text = $"FPS (10s avg): {metrics.Fps:F1}";
        _frameTimeText.Text = $"Frame (avg): {metrics.AvgFrameTimeMs:F1} ms";
        _longestFrameText.Text = $"Longest frame (10s): {metrics.LongestFrameMs:F1} ms";
        _animationsText.Text = $"Animations: {(metrics.HeavyAnimationsEnabled ? "Enabled" : "DISABLED")}";
        
        // Color code FPS based on performance
        _fpsText.Foreground = metrics.Fps switch
        {
            >= 55 => Avalonia.Media.Brushes.LightGreen,
            >= 45 => Avalonia.Media.Brushes.Yellow,
            _ => Avalonia.Media.Brushes.Red
        };
        
        // Color code frame time
        _frameTimeText.Foreground = metrics.AvgFrameTimeMs switch
        {
            <= 16.67 => Avalonia.Media.Brushes.LightGreen,
            <= 22 => Avalonia.Media.Brushes.Yellow,
            _ => Avalonia.Media.Brushes.Red
        };
        
        // Color code longest frame
        _longestFrameText.Foreground = metrics.LongestFrameMs switch
        {
            <= 22 => Avalonia.Media.Brushes.LightGreen,
            <= 33 => Avalonia.Media.Brushes.Yellow,
            _ => Avalonia.Media.Brushes.Red
        };
    }
    
    public void UpdateGameState(GameState gameState)
    {
        _lastGameState = gameState;
        
        _phaseText.Text = $"Phase: {gameState.Phase}";
        _songText.Text = $"Song: {gameState.CurrentSongTitle}";
        _sessionText.Text = $"Session: {gameState.SessionCode}";
        
        var talliesStr = gameState.HasTallies 
            ? $"[{string.Join(", ", gameState.Tallies)}]"
            : "None";
        _talliesText.Text = $"Tallies: {talliesStr}";
    }
    
    public void UpdateConnectionId(string connectionId)
    {
        _connectionId = connectionId;
        _connectionIdText.Text = $"ID: {connectionId[..Math.Min(8, connectionId.Length)]}...";
    }
    
    public void Show()
    {
        IsDebugVisible = true;
        IsVisible = true;
    }
    
    public void Hide()
    {
        IsDebugVisible = false;
        IsVisible = false;
    }
    
    public void Toggle()
    {
        IsDebugVisible = !IsDebugVisible;
        IsVisible = IsDebugVisible;
    }
    
    private void OnCopyDiagnostics(object? sender, RoutedEventArgs e)
    {
        try
        {
            var diagnostics = CreateDiagnosticsJson();
            
            // Copy to clipboard
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                _ = clipboard.SetTextAsync(diagnostics);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to copy diagnostics: {ex.Message}");
        }
    }
    
    private string CreateDiagnosticsJson()
    {
        var diagnostics = new
        {
            timestamp = DateTimeOffset.UtcNow,
            performance = _lastPerformanceMetrics != null ? new
            {
                fps = _lastPerformanceMetrics.Fps,
                avgFrameTimeMs = _lastPerformanceMetrics.AvgFrameTimeMs,
                maxFrameTimeMs = _lastPerformanceMetrics.MaxFrameTimeMs,
                minFrameTimeMs = _lastPerformanceMetrics.MinFrameTimeMs,
                longestFrameMs = _lastPerformanceMetrics.LongestFrameMs,
                heavyAnimationsEnabled = _lastPerformanceMetrics.HeavyAnimationsEnabled
            } : null,
            gameState = _lastGameState != null ? new
            {
                phase = _lastGameState.Phase.ToString(),
                sessionCode = _lastGameState.SessionCode,
                songIndex = _lastGameState.SongIndex,
                currentSong = _lastGameState.CurrentSong?.Title ?? "None",
                artist = _lastGameState.CurrentSong?.Artist ?? "None",
                hintIndex = _lastGameState.HintIndex,
                tallies = _lastGameState.Tallies,
                totalAnswers = _lastGameState.TotalAnswers,
                playerCount = _lastGameState.Scores.Count
            } : null,
            connection = new
            {
                connectionId = _connectionId,
                sessionCode = _lastGameState?.SessionCode ?? "Unknown"
            },
            environment = new
            {
                platform = Environment.OSVersion.Platform.ToString(),
                version = Environment.Version.ToString(),
                workingSet = Environment.WorkingSet,
                processorCount = Environment.ProcessorCount
            }
        };
        
        return JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
