using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Design = Nuotti.Contracts.V1.Design;
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
    readonly Button _tallyToggleButton;
    readonly Grid _contentGrid;
    readonly SafeAreaFrame _safeAreaFrame;
    readonly NowPlayingBanner _nowPlayingBanner;
    readonly ReconnectOverlay _reconnectOverlay;
    readonly DebugOverlay _debugOverlay;
    readonly AvaloniaList<string> _logs = new();

    readonly string _backend = "http://localhost:5240";
    readonly string _sessionCode = "dev";

    int[] _tally = new int[4];

    // New services for F2, F3, F5, F11, F12, F15, F16, F17, F18, F19, F21 & F22
    private readonly SettingsService _settingsService;
    private readonly MonitorService _monitorService;
    private readonly SafeAreaService _safeAreaService;
    private readonly GameStateService _gameStateService;
    private readonly ReconnectService _reconnectService;
    private readonly PerformanceService _performanceService;
    private readonly FontService _fontService;
    private readonly ErrorHandlingService _errorHandlingService;
    private readonly LocalizationService _localizationService;
    private readonly ContentSafetyService _contentSafetyService;
    private readonly ThemingApiService _themingApiService;
    private readonly AudioEnforcementService _audioEnforcementService;
    private readonly HotplugService _hotplugService;
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
        _tallyToggleButton = this.FindControl<Button>("TallyToggleButton")!;
        _contentGrid = this.FindControl<Grid>("ContentGrid")!;
        _safeAreaFrame = this.FindControl<SafeAreaFrame>("SafeAreaFrameControl")!;
        _nowPlayingBanner = this.FindControl<NowPlayingBanner>("NowPlayingBannerControl")!;
        _reconnectOverlay = this.FindControl<ReconnectOverlay>("ReconnectOverlayControl")!;
        _debugOverlay = this.FindControl<DebugOverlay>("DebugOverlayControl")!;
        _logList.ItemsSource = _logs;

        // Initialize services
        _settingsService = new SettingsService();
        _monitorService = new MonitorService();
        _safeAreaService = new SafeAreaService();
        _gameStateService = new GameStateService();
        _reconnectService = new ReconnectService(_backend);
        _performanceService = new PerformanceService();
        _fontService = new FontService();
        _errorHandlingService = new ErrorHandlingService();
        _localizationService = new LocalizationService();
        _contentSafetyService = new ContentSafetyService();
        _themingApiService = new ThemingApiService(_backend);
        _audioEnforcementService = new AudioEnforcementService();
        _hotplugService = new HotplugService(_monitorService);
        _settings = new ProjectorSettings();

        // Initialize cursor service
        _cursorService = new CursorService(this);

        // Handle window size changes for safe area
        SizeChanged += OnWindowSizeChanged;

        // Initialize phase views
        InitializePhaseViews();

        // F18 - Set up content safety service
        _gameStateService.SetContentSafetyService(_contentSafetyService);

        // Subscribe to game state changes
        _gameStateService.StateChanged += OnGameStateChanged;
        _gameStateService.StateChanged += state => _debugOverlay.UpdateGameState(state);

        // F16 - Error handling service events
        _errorHandlingService.SetLocalizationService(_localizationService);
        _errorHandlingService.ErrorOccurred += OnErrorOccurred;
        _errorHandlingService.EmptyStateRequired += OnEmptyStateRequired;
        _errorHandlingService.RetryRequested += OnRetryRequested;
        _errorHandlingService.BackToLobbyRequested += OnBackToLobbyRequested;

        // F17 - Localization service events
        _localizationService.LanguageChanged += OnLanguageChanged;

        // F19 - Theming API service events
        _themingApiService.ThemeChangeRequested += OnRemoteThemeChangeRequested;
        _themingApiService.TallyModeChangeRequested += OnRemoteTallyModeChangeRequested;
        _themingApiService.StyleSettingsChanged += OnRemoteStyleSettingsChanged;

        // F22 - Audio enforcement service events
        _audioEnforcementService.AudioViolationDetected += OnAudioViolationDetected;

        // F21 - Hotplug service events
        _hotplugService.MonitorChanged += OnMonitorHotplugEvent;
        _hotplugService.MonitorDisconnected += OnMonitorDisconnected;

        // F12 - Performance monitoring
        _performanceService.HeavyAnimationsToggled += OnHeavyAnimationsToggled;
        _performanceService.MetricsUpdated += OnPerformanceMetricsUpdated;

        // F13 - Debug overlay (DEV only)
        this.KeyDown += OnKeyDown;

        // Hook into render loop for performance monitoring
        // Use a timer-based approach instead of render events for now
        var renderTimer = new Timer(_ =>
        {
            _performanceService.RecordFrameStart();
            _performanceService.RecordFrameEnd();
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(16.67)); // ~60 FPS

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
            // F18 - Apply content safety to incoming questions
            var safeQuestion = ApplyQuestionSafety(q);
            Dispatcher.UIThread.Post(() => SetQuestion(safeQuestion));
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

        // F10 - Handle engine status changes for Now Playing banner
        _connection.On<EngineStatusChanged>("EngineStatusChanged", status =>
        {
            AppendLocal($"EngineStatusChanged: {status.Status}");
            Dispatcher.UIThread.Post(() => UpdateNowPlayingBanner(status));
        });

        Loaded += async (_, _) =>
        {
            try
            {
                // F17 - Load localization first
                await _errorHandlingService.ExecuteWithErrorHandling(async () =>
                {
                    await _localizationService.LoadTranslationsAsync();

                    // Use saved language preference or system default
                    var savedLanguage = _settings?.Locale ?? "en";
                    if (!_localizationService.SetLanguage(savedLanguage))
                    {
                        _localizationService.SetCultureFromSystem();
                    }

                    AppendLocal($"[i18n] Localization loaded: {_localizationService.GetCurrentCultureName()}");
                }, "localization loading");

                // F15 - Load fonts before applying settings
                await _errorHandlingService.ExecuteWithErrorHandling(async () =>
                {
                    await _fontService.LoadFontsAsync();
                    AppendLocal("[fonts] Font loading completed");
                }, "font loading");

                // Load settings first
                _settings = await _errorHandlingService.ExecuteWithErrorHandling(async () =>
                {
                    return await _settingsService.LoadSettingsAsync();
                }, "settings loading") ?? new ProjectorSettings();

                // Apply saved theme
                _errorHandlingService.ExecuteWithErrorHandling(() =>
                {
                    var themeVariant = _settings.ThemeVariant switch
                    {
                        "Light" => Design.NuottiThemeVariant.Light,
                        "Dark" => Design.NuottiThemeVariant.Dark,
                        "HighContrast" => Design.NuottiThemeVariant.HighContrast,
                        _ => Design.NuottiThemeVariant.Light // Default to Light instead of system preference for consistency
                    };
                    ThemeHelper.ApplyThemeVariant(themeVariant);
                    UpdateThemeToggleButton();
                }, "theme application");

                // Apply safe area settings
                _safeAreaService.SafeAreaMargin = _settings.SafeAreaMargin;
                _safeAreaService.ShowSafeAreaFrame = _settings.ShowSafeAreaFrame;
                _safeAreaFrame.ShowFrame = _settings.ShowSafeAreaFrame;
                ApplySafeArea();

                // Apply tally visibility settings
                _tallyToggleButton.Content = _settings.HideTalliesUntilReveal ? "🙈" : "👁️";

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

                // F19 - Start theming API connection (optional, non-blocking)
                _ = StartThemingApiConnection();

                // F22 - Audio enforcement is already running
                AppendLocal($"[audio-enforcement] Monitoring started - {_audioEnforcementService.GenerateReport().BlockedProcessCount} processes blocked");

                // F21 - Hotplug monitoring is already running
                AppendLocal($"[hotplug] Monitor detection started - {_hotplugService.CurrentMonitors.Count} monitors detected");
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
            _reconnectOverlay.Show("Connection Lost", "Attempting to reconnect...");

            var delayMs = Random.Shared.Next(0, 5) * 1000;
            AppendLocal($"[hub] disconnected; reconnecting in {delayMs} ms");
            await Task.Delay(delayMs);

            await StartConnectionWithStateResync();
        };
    }

    async Task StartConnection()
    {
        await _connection.StartAsync();
        AppendLocal("[hub] start ok");

        // Update debug overlay with connection ID
        _debugOverlay.UpdateConnectionId(_connection.ConnectionId ?? "Unknown");

        await _connection.InvokeAsync("Join", _sessionCode, "projector", "projector");
        AppendLocal($"[hub] joined as projector to session={_sessionCode}");
        _ = StartLogConnection();
    }

    // F11 - Enhanced connection with state resync
    async Task StartConnectionWithStateResync()
    {
        try
        {
            _reconnectOverlay.Show("Reconnecting...", "Restoring connection...");

            await _connection.StartAsync();
            AppendLocal("[hub] reconnect start ok");

            await _connection.InvokeAsync("Join", _sessionCode, "projector", "projector");
            AppendLocal($"[hub] rejoined as projector to session={_sessionCode}");

            // Fetch latest state to resync
            _reconnectOverlay.Show("Reconnecting...", "Syncing latest state...");
            var latestState = await _reconnectService.FetchLatestStateAsync(_sessionCode);

            if (latestState != null)
            {
                _gameStateService.UpdateFromSnapshot(latestState);
                AppendLocal("[hub] state resynced successfully");
            }
            else
            {
                AppendLocal("[hub] state resync failed, continuing with current state");
            }

            _connectionTextBlock.Text = "Connected";
            _reconnectOverlay.Hide();

            _ = StartLogConnection();
            AppendLocal("[hub] reconnected successfully");
        }
        catch (Exception ex)
        {
            _connectionTextBlock.Text = $"Reconnection failed: {ex.Message}";
            _reconnectOverlay.Show("Reconnection Failed", "Will retry automatically...");
            AppendLocal($"[hub] reconnect error: {ex.Message}");

            // Retry after a longer delay
            await Task.Delay(5000);
            await StartConnectionWithStateResync();
        }
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
        var currentTheme = ThemeHelper.GetCurrentThemeVariant();
        var newTheme = ThemeHelper.GetNextThemeVariant(currentTheme);

        ThemeHelper.ApplyThemeVariant(newTheme);
        _settings.ThemeVariant = newTheme.ToString();

        // Save the new theme preference
        _ = Task.Run(async () => await _settingsService.SaveSettingsAsync(_settings));

        UpdateThemeToggleButton();
        AppendLocal($"[Theme] Switched to {newTheme}");
    }

    private void UpdateThemeToggleButton()
    {
        var currentTheme = ThemeHelper.GetCurrentThemeVariant();
        _themeToggleButton.Content = currentTheme switch
        {
            Design.NuottiThemeVariant.Dark => "☀️",
            Design.NuottiThemeVariant.HighContrast => "♿", // Accessibility symbol for high contrast
            _ => "🌙" // Light theme
        };
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
        // T key toggles tally visibility
        else if (e.Key == Key.T && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnToggleTallyVisibility(null, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // F22 - Audio enforcement functionality
    private void OnAudioViolationDetected(AudioViolation violation)
    {
        var severity = violation.ViolationType switch
        {
            AudioViolationType.BlockedProcess => "WARNING",
            AudioViolationType.AudioWindow => "INFO",
            AudioViolationType.SystemAudioService => "DEBUG",
            _ => "NOTICE"
        };

        AppendLocal($"[audio-{severity.ToLower()}] {violation.Description}");

        // For critical violations, we could show a warning overlay
        if (violation.ViolationType == AudioViolationType.BlockedProcess)
        {
            // In a production system, this might show a warning to the operator
            Console.WriteLine($"[audio-enforcement] CRITICAL: {violation.Description}");
        }
    }

    public AudioEnforcementReport GetAudioEnforcementReport()
    {
        return _audioEnforcementService.GenerateReport();
    }

    // F21 - Monitor hotplug functionality
    private void OnMonitorHotplugEvent(HotplugEvent hotplugEvent)
    {
        var eventType = hotplugEvent.EventType.ToString().ToLower();
        AppendLocal($"[hotplug] Monitor {eventType}: {hotplugEvent.Monitor.Name} ({hotplugEvent.Monitor.Width}x{hotplugEvent.Monitor.Height})");

        // If the current monitor was disconnected, try to find a safe fallback
        if (hotplugEvent.EventType == HotplugEventType.Disconnected)
        {
            var currentMonitorId = _settings.SelectedMonitorId;
            if (currentMonitorId == hotplugEvent.Monitor.Id)
            {
                var fallback = _hotplugService.FindSafeMonitorFallback();
                if (fallback != null)
                {
                    AppendLocal($"[hotplug] Switching to fallback monitor: {fallback.Name}");
                    // In a full implementation, we would switch to the fallback monitor
                }
                else
                {
                    AppendLocal("[hotplug] WARNING: No fallback monitor available");
                }
            }
        }
    }

    private void OnMonitorDisconnected(MonitorInfo monitor)
    {
        // Show a non-blocking notification about monitor disconnection
        AppendLocal($"[hotplug] NOTICE: Monitor '{monitor.Name}' was disconnected");
    }

    public HotplugReport GetHotplugReport()
    {
        return _hotplugService.GenerateReport();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _cursorService?.Dispose();
        _reconnectService?.Dispose();
        _performanceService?.Dispose();
        _themingApiService?.Dispose(); // F19 - Clean up theming API
        _audioEnforcementService?.Dispose(); // F22 - Clean up audio enforcement
        _hotplugService?.Dispose(); // F21 - Clean up hotplug monitoring
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
        _phaseViews[Phase.Intermission] = new ScoreboardView();
        _phaseViews[Phase.Hint] = new HintView();

        // Use SimplePhaseView for other phases
        var simplePhases = new[] { Phase.Start, Phase.Lock, Phase.Reveal, Phase.Play, Phase.Finished };
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

        // Update settings for views that support it
        if (_currentPhaseView is GuessingView guessingView)
        {
            guessingView.UpdateSettings(_settings);
        }

        _currentPhaseView.UpdateState(state);

        // Add to main content area (replace the current question/options area)
        Grid.SetRow(_currentPhaseView, 1);
        Grid.SetColumn(_currentPhaseView, 0);
        _contentGrid.Children.Add(_currentPhaseView);

        AppendLocal($"[gamestate] Switched to {phase} view");
    }

    // F7 - Live Tallies & Animations functionality
    private async void OnToggleTallyVisibility(object? sender, RoutedEventArgs e)
    {
        _settings.HideTalliesUntilReveal = !_settings.HideTalliesUntilReveal;
        await _settingsService.SaveSettingsAsync(_settings);

        // Update button appearance
        _tallyToggleButton.Content = _settings.HideTalliesUntilReveal ? "🙈" : "👁️";

        // Refresh current view if it's a guessing view
        if (_currentPhaseView is GuessingView guessingView)
        {
            guessingView.UpdateSettings(_settings);
            guessingView.UpdateState(_gameStateService.CurrentState);
        }

        var status = _settings.HideTalliesUntilReveal ? "hidden" : "visible";
        AppendLocal($"[tallies] Tallies during guessing: {status}");
    }

    // F10 - Now Playing Banner functionality
    private void UpdateNowPlayingBanner(EngineStatusChanged statusChange)
    {
        try
        {
            var isPlaying = statusChange.Status == EngineStatus.Playing;

            if (isPlaying)
            {
                // Get current song info from game state
                var currentState = _gameStateService.CurrentState;
                var songTitle = currentState.CurrentSongTitle;
                var artist = currentState.CurrentSongArtist != "Unknown Artist" ? currentState.CurrentSongArtist : null;

                _nowPlayingBanner.UpdateSong(songTitle, artist);
                _nowPlayingBanner.Show();

                AppendLocal($"[now-playing] Showing: {songTitle}");
            }
            else
            {
                _nowPlayingBanner.Hide();
                AppendLocal("[now-playing] Hidden");
            }
        }
        catch (Exception ex)
        {
            AppendLocal($"[now-playing] Error: {ex.Message}");
        }
    }

    // F12 - Performance Budget & Frame Loop functionality
    private void OnHeavyAnimationsToggled(bool enabled)
    {
        var status = enabled ? "enabled" : "disabled";
        AppendLocal($"[performance] Heavy animations {status} due to performance");

        // Update animation service settings for all views
        if (_currentPhaseView is GuessingView guessingView)
        {
            // Could add a method to update animation settings
        }
    }

    public PerformanceMetrics GetPerformanceMetrics()
    {
        return _performanceService.GetCurrentMetrics();
    }

    public bool ShouldUseHeavyAnimations()
    {
        return _performanceService.HeavyAnimationsEnabled;
    }

    private void OnPerformanceMetricsUpdated(PerformanceMetrics metrics)
    {
        _debugOverlay.UpdatePerformanceMetrics(metrics);
    }

    // F13 - Debug Overlay functionality & F14 - Window Management Controls
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+D to toggle debug overlay (DEV only)
        if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
#if DEBUG
            _debugOverlay.Toggle();
            var status = _debugOverlay.IsDebugVisible ? "shown" : "hidden";
            AppendLocal($"[debug] Debug overlay {status}");
#endif
            e.Handled = true;
        }

        // F14 - Window Management Controls
        switch (e.Key)
        {
            case Key.F when !e.KeyModifiers.HasFlag(KeyModifiers.Control):
                ToggleFullscreen();
                e.Handled = true;
                break;

            case Key.B when !e.KeyModifiers.HasFlag(KeyModifiers.Control):
                ToggleBlackScreen();
                e.Handled = true;
                break;

            case Key.Escape:
                if (WindowState == WindowState.FullScreen)
                {
                    ExitFullscreen();
                }
                else
                {
                    Close();
                }
                e.Handled = true;
                break;

            case Key.T when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                ToggleAlwaysOnTop();
                e.Handled = true;
                break;

            case Key.C when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                ToggleCursorVisibility();
                e.Handled = true;
                break;

            case Key.H when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                ShowKeyboardShortcuts();
                e.Handled = true;
                break;
        }
    }

    // F14 - Window Management functionality
    private bool _isBlackScreenActive = false;
    private Border? _blackScreenOverlay;

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            ExitFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    private async void EnterFullscreen()
    {
        try
        {
            WindowState = WindowState.FullScreen;
            _settings.IsFullscreen = true;
            await _settingsService.SaveSettingsAsync(_settings);
            AppendLocal("[window] Entered fullscreen mode (F to exit)");
        }
        catch (Exception ex)
        {
            AppendLocal($"[window] Error entering fullscreen: {ex.Message}");
        }
    }


    private void ToggleBlackScreen()
    {
        if (_isBlackScreenActive)
        {
            HideBlackScreen();
        }
        else
        {
            ShowBlackScreen();
        }
    }

    private void ShowBlackScreen()
    {
        if (_blackScreenOverlay == null)
        {
            _blackScreenOverlay = new Border
            {
                Background = Brushes.Black,
                ZIndex = 9999
            };

            // Add to the main grid
            var mainGrid = this.FindControl<Grid>("MainGrid");
            if (mainGrid != null)
            {
                mainGrid.Children.Add(_blackScreenOverlay);
                Grid.SetRowSpan(_blackScreenOverlay, 4);
                Grid.SetColumnSpan(_blackScreenOverlay, 3);
            }
        }

        _blackScreenOverlay.IsVisible = true;
        _isBlackScreenActive = true;
        AppendLocal("[window] Black screen activated (B to exit)");
    }

    private void HideBlackScreen()
    {
        if (_blackScreenOverlay != null)
        {
            _blackScreenOverlay.IsVisible = false;
        }

        _isBlackScreenActive = false;
        AppendLocal("[window] Black screen deactivated");
    }

    private void ToggleAlwaysOnTop()
    {
        Topmost = !Topmost;
        var status = Topmost ? "enabled" : "disabled";
        AppendLocal($"[window] Always on top {status}");
    }

    private void ToggleCursorVisibility()
    {
        if (_cursorService != null)
        {
            _cursorService.ToggleVisibility();
            var status = _cursorService.IsVisible ? "shown" : "hidden";
            AppendLocal($"[window] Cursor {status}");
        }
    }

    private void ShowKeyboardShortcuts()
    {
        var shortcuts = @"🎮 KEYBOARD SHORTCUTS

Window Controls:
  F           - Toggle fullscreen
  B           - Toggle black screen
  Esc         - Exit fullscreen / Close app
  Ctrl+T      - Toggle always on top
  Ctrl+C      - Toggle cursor visibility
  Ctrl+H      - Show this help

Debug (DEV only):
  Ctrl+D      - Toggle debug overlay

Monitor & Display:
  M           - Monitor selection
  S           - Safe area toggle
  T           - Tally display toggle

Fonts & Typography:
  Fonts loaded: " + (_fontService.AreFontsLoaded ? "✓" : "✗") + @"
  Primary: " + _fontService.PrimaryFont.Name + @"
  Monospace: " + _fontService.MonospaceFont.Name + @"

Content Safety:
  Service active: ✓
  Max choice length: 200 chars
  HTML/Script filtering: ✓

Audio Enforcement:
  Monitoring: " + (_audioEnforcementService.IsAudioBlocked ? "✓" : "✗") + @"
  Audio detected: " + (_audioEnforcementService.HasDetectedAudio ? "⚠️" : "✓") + @"
  Blocked processes: " + _audioEnforcementService.GenerateReport().BlockedProcessCount + @"

Monitor Hotplug:
  Monitoring: " + (_hotplugService.IsMonitoring ? "✓" : "✗") + @"
  Monitors detected: " + _hotplugService.CurrentMonitors.Count + @"
  Primary available: " + (_hotplugService.GenerateReport().PrimaryMonitor != null ? "✓" : "✗");

        AppendLocal("[help] Keyboard shortcuts:");
        foreach (var line in shortcuts.Split('\n'))
        {
            AppendLocal($"[help] {line}");
        }
    }

    // F16 - Error & Empty State handling
    private void OnErrorOccurred(ErrorStateView errorView)
    {
        // Replace current content with error view
        _contentGrid.Children.Clear();
        _contentGrid.Children.Add(errorView);
        Grid.SetRowSpan(errorView, 4);
        Grid.SetColumnSpan(errorView, 3);

        AppendLocal("[error] Error state displayed");
    }

    private void OnEmptyStateRequired(EmptyStateView emptyView)
    {
        // Replace current content with empty state view
        _contentGrid.Children.Clear();
        _contentGrid.Children.Add(emptyView);
        Grid.SetRowSpan(emptyView, 4);
        Grid.SetColumnSpan(emptyView, 3);

        AppendLocal("[empty] Empty state displayed");
    }

    private void OnRetryRequested()
    {
        AppendLocal("[error] Retry requested, attempting to reconnect...");

        // Clear error/empty state and try to reconnect
        _contentGrid.Children.Clear();

        // Show loading state
        _errorHandlingService.ShowEmptyState(EmptyStateType.Loading, "Reconnecting to game server...");

        // Attempt to restart connection
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000); // Brief delay
            await StartConnection();
        });
    }

    private void OnBackToLobbyRequested()
    {
        AppendLocal("[error] Back to lobby requested");

        // Clear error state and show lobby
        _contentGrid.Children.Clear();

        // Reset to lobby state - show empty state for now
        var message = _localizationService.GetString("empty.waiting_for_game");
        _errorHandlingService.ShowEmptyState(EmptyStateType.WaitingForGame, message);
    }

    // F17 - Internationalization functionality
    private void OnLanguageChanged(string newLanguage)
    {
        AppendLocal($"[i18n] Language changed to: {_localizationService.GetCurrentCultureName()}");

        // In a full implementation, we would refresh all UI text here
        // For now, we just log the change
    }

    public LocalizationService GetLocalizationService()
    {
        return _localizationService;
    }

    // F18 - Content safety for incoming SignalR messages
    private QuestionPushed ApplyQuestionSafety(QuestionPushed question)
    {
        // Check and log any content safety issues
        var safeText = _contentSafetyService.SanitizeText(question.Text, ContentType.General);
        if (safeText.WasModified)
        {
            AppendLocal($"[content-safety] Question text sanitized: {safeText.Warnings}");
        }

        for (int i = 0; i < question.Options.Length; i++)
        {
            var choiceResult = _contentSafetyService.SanitizeChoice(question.Options[i], i);
            if (choiceResult.WasModified)
            {
                AppendLocal($"[content-safety] Question option {i + 1} sanitized: {choiceResult.Warnings}");
            }
        }

        // For now, return the original question since creating a new one requires base class properties
        // In a production system, we would modify the question in place or have a different approach
        return question;
    }

    // F19 - Theming API functionality
    private async Task StartThemingApiConnection()
    {
        try
        {
            await _themingApiService.ConnectAsync();
            await _themingApiService.RegisterProjectorAsync(_sessionCode);
            AppendLocal("[theming-api] Connected and registered");
        }
        catch (Exception ex)
        {
            AppendLocal($"[theming-api] Connection failed: {ex.Message}");
            // Non-critical failure, continue without theming API
        }
    }

    private void OnRemoteThemeChangeRequested(ThemeVariant themeVariant)
    {
        try
        {
            // Convert Avalonia ThemeVariant to our Design.NuottiThemeVariant
            Design.NuottiThemeVariant designTheme;
            if (themeVariant == ThemeVariant.Dark)
            {
                designTheme = Design.NuottiThemeVariant.Dark;
            }
            else if (themeVariant == ThemeVariant.Light)
            {
                designTheme = Design.NuottiThemeVariant.Light;
            }
            else
            {
                designTheme = Design.NuottiThemeVariant.Light;
            }

            ThemeHelper.ApplyThemeVariant(designTheme);
            _settings.ThemeVariant = designTheme.ToString();

            // Save the new theme preference
            _ = Task.Run(async () => await _settingsService.SaveSettingsAsync(_settings));

            UpdateThemeToggleButton();
            AppendLocal($"[theming-api] Theme changed to: {designTheme}");
        }
        catch (Exception ex)
        {
            AppendLocal($"[theming-api] Theme change failed: {ex.Message}");
        }
    }

    private void OnRemoteTallyModeChangeRequested(TallyDisplayMode tallyMode)
    {
        try
        {
            _settings.TallyMode = tallyMode.ToString();

            // Apply tally mode to current view if applicable
            // Note: Specific tally mode application would require extending the view classes

            // Save the new tally mode preference
            _ = Task.Run(async () => await _settingsService.SaveSettingsAsync(_settings));

            AppendLocal($"[theming-api] Tally mode changed to: {tallyMode}");
        }
        catch (Exception ex)
        {
            AppendLocal($"[theming-api] Tally mode change failed: {ex.Message}");
        }
    }

    private void OnRemoteStyleSettingsChanged(ProjectorStyleSettings styleSettings)
    {
        try
        {
            // Apply style settings
            if (styleSettings.ThemeVariant != _settings.ThemeVariant)
            {
                var themeVariant = styleSettings.ThemeVariant switch
                {
                    "Light" => Design.NuottiThemeVariant.Light,
                    "Dark" => Design.NuottiThemeVariant.Dark,
                    "HighContrast" => Design.NuottiThemeVariant.HighContrast,
                    _ => Design.NuottiThemeVariant.Light
                };
                ThemeHelper.ApplyThemeVariant(themeVariant);
                _settings.ThemeVariant = styleSettings.ThemeVariant;
                UpdateThemeToggleButton();
            }

            if (styleSettings.ShowSafeArea != _settings.ShowSafeAreaFrame)
            {
                _settings.ShowSafeAreaFrame = styleSettings.ShowSafeArea;
                _safeAreaFrame.IsVisible = styleSettings.ShowSafeArea;
            }

            if (Math.Abs(styleSettings.SafeAreaMargin - _settings.SafeAreaMargin) > 0.001)
            {
                _settings.SafeAreaMargin = styleSettings.SafeAreaMargin;
                // Note: Safe area margin application would require extending the SafeAreaService
            }

            if (styleSettings.HideTalliesUntilReveal != _settings.HideTalliesUntilReveal)
            {
                _settings.HideTalliesUntilReveal = styleSettings.HideTalliesUntilReveal;
                // Note: Tally visibility application would require extending the view classes
            }

            // Save all settings
            _ = Task.Run(async () => await _settingsService.SaveSettingsAsync(_settings));

            AppendLocal("[theming-api] Style settings updated");
        }
        catch (Exception ex)
        {
            AppendLocal($"[theming-api] Style settings update failed: {ex.Message}");
        }
    }

}
