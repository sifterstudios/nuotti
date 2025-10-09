using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
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
    readonly AvaloniaList<string> _logs = new();

    readonly string _backend = "http://localhost:5240";
    readonly string _sessionCode = "dev";

    int[] _tally = new int[4];

    public MainWindow()
    {
        InitializeComponent();

        _connectionTextBlock = this.FindControl<TextBlock>("ConnectionStatus")!;
        _sessionCodeText = this.FindControl<TextBlock>("SessionCodeText")!;
        _questionText = this.FindControl<TextBlock>("QuestionText")!;
        _logList = this.FindControl<ListBox>("LogList")!;
        _themeToggleButton = this.FindControl<Button>("ThemeToggleButton")!;
        _logList.ItemsSource = _logs;
        
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

        _connection.On<QuestionPushed>("QuestionPushed", q =>
        {
            AppendLocal($"QuestionPushed: {q.Text}");
            Dispatcher.UIThread.Post(() => SetQuestion(q));
        });
        _connection.On<AnswerSubmitted>("AnswerSubmitted", a =>
        {
            AppendLocal($"AnswerSubmitted: choiceIndex={a.ChoiceIndex}");
            Dispatcher.UIThread.Post(() => Tally(a.ChoiceIndex));
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
}