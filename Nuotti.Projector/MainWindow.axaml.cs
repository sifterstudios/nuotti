using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using Nuotti.Projector.Controls;
using Nuotti.Projector.Models;
using Nuotti.Projector.Services;
using Nuotti.Projector.Views;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
namespace Nuotti.Projector;

public partial class MainWindow : Window
{
    readonly HubConnection _connection;
    HubConnection? _logConnection;
    readonly TextBlock _connectionTextBlock;
    readonly TextBlock _sessionCodeText;
    readonly TextBlock _questionText;
    readonly TextBlock[] _choiceTexts;
    readonly TextBlock[] _choiceCounts;
    readonly Border[] _rows;
    readonly ListBox _logList;
    readonly Button _themeToggleButton;
    readonly Button _monitorButton;
    readonly Button _safeAreaButton;
    readonly Grid _contentGrid;
    readonly SafeAreaFrame _safeAreaFrame;
    readonly AvaloniaList<string> _logs = new();

    readonly string _backend = "http://localhost:5240";
    readonly string _sessionCode = "dev";

    int[] _tally = new int[4];
    
    // New services for F2, F3 & F5
    private readonly SettingsService _settingsService;
    private readonly MonitorService _monitorService;
    private readonly SafeAreaService _safeAreaService;
    private readonly GameStateService _gameStateService;
    private CursorService? _cursorService;
    private ProjectorSettings _settings;
    
    // F5 - Phase views
    private readonly Dictionary<Phase, PhaseViewBase> _phaseViews = new();
    private PhaseViewBase? _currentPhaseView;

    public MainWindow()
    {
        InitializeComponent();

        _connectionTextBlock = this.FindControl<TextBlock>("ConnectionStatus")!;
        _sessionCodeText = this.FindControl<TextBlock>("SessionCodeText")!;
        _questionText = this.FindControl<TextBlock>("QuestionText")!;
        _logList = this.FindControl<ListBox>("LogList")!;
        _themeToggleButton = this.FindControl<Button>("ThemeToggleButton")!;
        _monitorButton = this.FindControl<Button>("MonitorButton")!;
        _safeAreaButton = this.FindControl<Button>("SafeAreaButton")!;
        _contentGrid = this.FindControl<Grid>("ContentGrid")!;
        _safeAreaFrame = this.FindControl<SafeAreaFrame>("SafeAreaFrameControl")!;
        _logList.ItemsSource = _logs;
        
        // Initialize services
        _settingsService = new SettingsService();
        _monitorService = new MonitorService();
        _safeAreaService = new SafeAreaService();
        _gameStateService = new GameStateService();
        _settings = new ProjectorSettings();
        
        // Initialize cursor service
        _cursorService = new CursorService(this);
        
        // Handle window size changes for safe area
        SizeChanged += OnWindowSizeChanged;
        
        // Initialize phase views
        InitializePhaseViews();
        
        // Subscribe to game state changes
        _gameStateService.StateChanged += OnGameStateChanged;
        
        // Update theme toggle button icon based on current theme
        UpdateThemeToggleButton();
        _choiceTexts = new[]
        {
            this.FindControl<TextBlock>("Choice0Text")!,
            this.FindControl<TextBlock>("Choice1Text")!,
            this.FindControl<TextBlock>("Choice2Text")!,
            this.FindControl<TextBlock>("Choice3Text")!,
        };
        _choiceCounts = new[]
        {
            this.FindControl<TextBlock>("Choice0Count")!,
            this.FindControl<TextBlock>("Choice1Count")!,
            this.FindControl<TextBlock>("Choice2Count")!,
            this.FindControl<TextBlock>("Choice3Count")!,
        };
        _rows = new[]
        {
            this.FindControl<Border>("Row0")!,
            this.FindControl<Border>("Row1")!,
            this.FindControl<Border>("Row2")!,
            this.FindControl<Border>("Row3")!,
        };

        _sessionCodeText.Text = _sessionCode;

        _connection = new HubConnectionBuilder()
            .WithUrl($"{_backend}/hub")
            .WithAutomaticReconnect()
            .Build();

        // F5 - Subscribe to GameStateChanged instead of individual events
        _connection.On<GameStateSnapshot>("GameStateChanged", snapshot =>
        {
            AppendLocal($"GameStateChanged: Phase={snapshot.Phase}, Song={snapshot.CurrentSong?.Title}");
            Dispatcher.UIThread.Post(() => _gameStateService.UpdateFromSnapshot(snapshot));
        });
        
        // Keep legacy event handlers for backward compatibility
        _connection.On<QuestionPushed>("QuestionPushed", q =>
        {
            AppendLocal($"QuestionPushed: {q.Text}");
            Dispatcher.UIThread.Post(() => SetQuestion(q));
        });
        _connection.On<AnswerSubmitted>("AnswerSubmitted", a =>
        {
            AppendLocal($"AnswerSubmitted: choiceIndex={a.ChoiceIndex}");
            Dispatcher.UIThread.Post(() => 
            {
                Tally(a.ChoiceIndex);
                _gameStateService.UpdateTally(a.ChoiceIndex);
            });
        });
        _connection.On<PlayTrack>("RequestPlay", p =>
        {
            AppendLocal($"RequestPlay received: url={p.FileUrl}");
            _ = ForwardPlayToBackend(p);
        });

        Loaded += async (_, _) =>
        {
            try
            {
                // Load settings first
                _settings = await _settingsService.LoadSettingsAsync();
                
                // Apply saved theme
                if (Enum.TryParse<ThemeVariant>(_settings.ThemeVariant, out var themeVariant))
                {
                    Application.Current!.RequestedThemeVariant = themeVariant;
                }
                UpdateThemeToggleButton();
                
                // Apply safe area settings
                _safeAreaService.SafeAreaMargin = _settings.SafeAreaMargin;
                _safeAreaService.ShowSafeAreaFrame = _settings.ShowSafeAreaFrame;
                _safeAreaFrame.ShowFrame = _settings.ShowSafeAreaFrame;
                ApplySafeArea();
                
                // Apply saved fullscreen state
                if (_settings.IsFullscreen && !string.IsNullOrEmpty(_settings.SelectedMonitorId))
                {
                    var monitor = _monitorService.GetMonitorById(_settings.SelectedMonitorId);
                    if (monitor != null)
                    {
                        EnterFullscreen(monitor);
                    }
                }
                
                await StartConnection();
                _connectionTextBlock.Text = "Connected";
                AppendLocal("[hub] connected");
            }
            catch (Exception ex)
            {
                _connectionTextBlock.Text = $"Connection failed: {ex.Message}";
                AppendLocal($"[hub] connect error: {ex.Message}");
            }
        };
        _connection.Closed += async (_) =>
        {
            _connectionTextBlock.Text = "Disconnected";
            var delayMs = Random.Shared.Next(0, 5) * 1000;
            AppendLocal($"[hub] disconnected; reconnecting in {delayMs} ms");
            await Task.Delay(delayMs);
            await StartConnection();
        };
    }

    async Task StartConnection()
    {
        await _connection.StartAsync();
        AppendLocal("[hub] start ok");
        await _connection.InvokeAsync("Join", _sessionCode, "projector", "projector");
        AppendLocal($"[hub] joined as projector to session={_sessionCode}");
        _ = StartLogConnection();
    }

    public async Task StopConnection()
    {
        await _connection.StopAsync();
    }

    void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    void SetQuestion(QuestionPushed q)
    {
        _questionText.Text = q.Text;
        _tally = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (q.Options != null && i < q.Options.Length)
            {
                _choiceTexts[i].Text = q.Options[i];
                _rows[i].IsVisible = true;
            }
            else
            {
                _choiceTexts[i].Text = string.Empty;
                _rows[i].IsVisible = false;
            }
            _choiceCounts[i].Text = "0";
            // Reset to default background using application resources with theme support
            if (Application.Current?.Resources.TryGetResource("OptionBackgroundBrush", Application.Current?.ActualThemeVariant, out var optionBgObj) == true && optionBgObj is IBrush optionBg)
            {
                _rows[i].Background = optionBg;
            }
            else
            {
                _rows[i].Background = new SolidColorBrush(Color.Parse("#F5F5F5"));
            }
        }
    }

    void Tally(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= _tally.Length) return;
        _tally[choiceIndex]++;
        for (int i = 0; i < _tally.Length; i++)
        {
            _choiceCounts[i].Text = _tally[i].ToString();
        }
        HighlightLeaders();
    }

    void HighlightLeaders()
    {
        int max = _tally.Max();
        IBrush? successBrush = null;
        IBrush? defaultBrush = null;
        if (Application.Current?.Resources.TryGetResource("SuccessBrush", Application.Current?.ActualThemeVariant, out var successObj) == true && successObj is IBrush s)
            successBrush = s;
        if (Application.Current?.Resources.TryGetResource("OptionBackgroundBrush", Application.Current?.ActualThemeVariant, out var defaultObj) == true && defaultObj is IBrush d)
            defaultBrush = d;
        successBrush ??= new SolidColorBrush(Color.Parse("#46B283"));
        defaultBrush ??= new SolidColorBrush(Color.Parse("#F5F5F5"));
        
        for (int i = 0; i < _tally.Length; i++)
        {
            _rows[i].Background = _tally[i] == max && max > 0 ? successBrush : defaultBrush;
        }
    }

    private void ToggleTheme(object? sender, RoutedEventArgs e)
    {
        var currentTheme = Application.Current?.ActualThemeVariant;
        var newTheme = currentTheme == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = newTheme;
        }
        
        UpdateThemeToggleButton();
        AppendLocal($"[Theme] Switched to {newTheme}");
    }

    private void UpdateThemeToggleButton()
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        _themeToggleButton.Content = isDark ? "☀️" : "🌙";
    }

    private async Task ForwardPlayToBackend(PlayTrack p)
    {
        try
        {
            using var client = new HttpClient();
            var resp = await client.PostAsJsonAsync($"{_backend}/api/play/{_sessionCode}", p);
            if (!resp.IsSuccessStatusCode)
            {
                Dispatcher.UIThread.Post(() => _connectionTextBlock.Text = $"Play POST failed: {(int)resp.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => _connectionTextBlock.Text = $"Play POST error: {ex.Message}");
        }
    }

    async Task StartLogConnection()
    {
        try
        {
            if (_logConnection == null)
            {
                _logConnection = new HubConnectionBuilder()
                    .WithUrl($"{_backend}/log")
                    .WithAutomaticReconnect()
                    .Build();

                _logConnection.On<LogEvent>("Log", e =>
                {
                    Dispatcher.UIThread.Post(() => AppendLog(e));
                });
            }
            if (_logConnection.State == HubConnectionState.Disconnected)
            {
                await _logConnection.StartAsync();
                AppendLocal("[log] connected");
            }
        }
        catch (Exception ex)
        {
            AppendLocal($"[log] connect error: {ex.Message}");
        }
    }

    void AppendLog(LogEvent e)
    {
        var ts = e.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        var line = $"{ts} {e.Level,-5} {e.Source}: {e.Message} | conn={e.ConnectionId} sess={e.Session} role={e.Role}";
        _logs.Add(line);
        TrimAndScroll();
    }

    void AppendLocal(string message)
    {
        var ts = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
        _logs.Add($"{ts} LOCAL  Projector: {message}");
        TrimAndScroll();
    }

    void TrimAndScroll()
    {
        const int max = 500;
        while (_logs.Count > max)
            _logs.RemoveAt(0);
        // Scroll to bottom
        if (_logList.ItemCount > 0)
            _logList.ScrollIntoView(_logList.ItemCount - 1);
    }
    
    // F2 - Monitor Selection & Fullscreen functionality
    private async void OnMonitorSelectionClick(object? sender, RoutedEventArgs e)
    {
        var monitors = _monitorService.GetAvailableMonitors();
        if (monitors.Count == 0)
        {
            AppendLocal("[monitor] No monitors detected");
            return;
        }
        
        var dialog = new MonitorSelectionDialog();
        dialog.SetMonitors(monitors, _settings.SelectedMonitorId);
        
        var result = await dialog.ShowDialog<bool>(this);
        if (result && dialog.SelectedMonitor != null)
        {
            _settings.SelectedMonitorId = dialog.SelectedMonitor.Id;
            _settings.IsFullscreen = true;
            await _settingsService.SaveSettingsAsync(_settings);
            
            EnterFullscreen(dialog.SelectedMonitor);
            AppendLocal($"[monitor] Fullscreen on {dialog.SelectedMonitor.DisplayName}");
        }
    }
    
    private void EnterFullscreen(MonitorInfo monitor)
    {
        try
        {
            WindowState = WindowState.FullScreen;
            
            // Position window on the selected monitor
            Position = new PixelPoint(monitor.X, monitor.Y);
            Width = monitor.Width;
            Height = monitor.Height;
            
            // Start cursor auto-hide
            _cursorService?.StartAutoHide();
            
            AppendLocal($"[fullscreen] Entered on {monitor.DisplayName}");
        }
        catch (Exception ex)
        {
            AppendLocal($"[fullscreen] Error: {ex.Message}");
        }
    }
    
    private async void ExitFullscreen()
    {
        try
        {
            WindowState = WindowState.Normal;
            _settings.IsFullscreen = false;
            await _settingsService.SaveSettingsAsync(_settings);
            
            // Stop cursor auto-hide
            _cursorService?.StopAutoHide();
            
            AppendLocal("[fullscreen] Exited");
        }
        catch (Exception ex)
        {
            AppendLocal($"[fullscreen] Exit error: {ex.Message}");
        }
    }
    
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        // F key toggles fullscreen
        if (e.Key == Key.F && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (WindowState == WindowState.FullScreen)
            {
                ExitFullscreen();
            }
            else
            {
                // Show monitor selection dialog
                OnMonitorSelectionClick(null, new RoutedEventArgs());
            }
            e.Handled = true;
        }
        // Escape exits fullscreen
        else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen)
        {
            ExitFullscreen();
            e.Handled = true;
        }
        // S key toggles safe area frame
        else if (e.Key == Key.S && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnToggleSafeAreaFrame(null, new RoutedEventArgs());
            e.Handled = true;
        }
    }
    
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _cursorService?.Dispose();
    }
    
    // F3 - Safe Area & Overscan Margins functionality
    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ApplySafeArea();
    }
    
    private void ApplySafeArea()
    {
        if (Bounds.Size.Width > 0 && Bounds.Size.Height > 0)
        {
            _safeAreaService.ApplySafeAreaToControl(_contentGrid, Bounds.Size);
            
            // Update safe area frame to match the content grid margins
            var safeAreaBounds = _safeAreaService.GetSafeAreaBounds(Bounds.Size);
            _safeAreaFrame.Margin = _safeAreaService.GetSafeAreaMargin(Bounds.Size);
        }
    }
    
    private async void OnToggleSafeAreaFrame(object? sender, RoutedEventArgs e)
    {
        _settings.ShowSafeAreaFrame = !_settings.ShowSafeAreaFrame;
        _safeAreaService.ShowSafeAreaFrame = _settings.ShowSafeAreaFrame;
        _safeAreaFrame.ShowFrame = _settings.ShowSafeAreaFrame;
        
        await _settingsService.SaveSettingsAsync(_settings);
        
        var status = _settings.ShowSafeAreaFrame ? "shown" : "hidden";
        AppendLocal($"[safe-area] Frame {status}");
    }
    
    public async Task SetSafeAreaMargin(double margin)
    {
        _settings.SafeAreaMargin = margin;
        _safeAreaService.SafeAreaMargin = margin;
        ApplySafeArea();
        
        await _settingsService.SaveSettingsAsync(_settings);
        AppendLocal($"[safe-area] Margin set to {margin:P1}");
    }
    
    // F5 - GameState Renderer functionality
    private void InitializePhaseViews()
    {
        // Create phase-specific views
        _phaseViews[Phase.Lobby] = new LobbyView();
        _phaseViews[Phase.Guessing] = new GuessingView();
        
        // Use SimplePhaseView for other phases
        var simplePhases = new[] { Phase.Start, Phase.Hint, Phase.Lock, Phase.Reveal, Phase.Play, Phase.Intermission, Phase.Finished };
        foreach (var phase in simplePhases)
        {
            _phaseViews[phase] = new SimplePhaseView();
        }
    }
    
    private void OnGameStateChanged(GameState state)
    {
        try
        {
            // Update session code display
            _sessionCodeText.Text = state.SessionCode.ToUpperInvariant();
            
            // Switch to appropriate phase view
            if (_gameStateService.ShouldShowPhase(state.Phase))
            {
                SwitchToPhaseView(state.Phase, state);
            }
            
            AppendLocal($"[gamestate] Phase: {state.Phase}, Song: {state.CurrentSongTitle}");
        }
        catch (Exception ex)
        {
            AppendLocal($"[gamestate] Error: {ex.Message}");
        }
    }
    
    private void SwitchToPhaseView(Phase phase, GameState state)
    {
        if (!_phaseViews.TryGetValue(phase, out var phaseView))
        {
            AppendLocal($"[gamestate] No view for phase {phase}");
            return;
        }
        
        // Remove current view
        if (_currentPhaseView != null)
        {
            _contentGrid.Children.Remove(_currentPhaseView);
        }
        
        // Add new view
        _currentPhaseView = phaseView;
        _currentPhaseView.UpdateState(state);
        
        // Add to main content area (replace the current question/options area)
        Grid.SetRow(_currentPhaseView, 1);
        Grid.SetColumn(_currentPhaseView, 0);
        _contentGrid.Children.Add(_currentPhaseView);
        
        AppendLocal($"[gamestate] Switched to {phase} view");
    }
}