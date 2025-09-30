using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
namespace Nuotti.Projector;

public partial class MainWindow : Window
{
    readonly HubConnection _connection;
    readonly TextBlock _connectionTextBlock;
    readonly TextBlock _sessionCodeText;
    readonly TextBlock _questionText;
    readonly TextBlock[] _choiceTexts;
    readonly TextBlock[] _choiceCounts;
    readonly Border[] _rows;

    readonly string _backend = "http://localhost:5240";
    readonly string _sessionCode = "dev";

    int[] _tally = new int[4];

    public MainWindow()
    {
        InitializeComponent();

        _connectionTextBlock = this.FindControl<TextBlock>("ConnectionStatus")!;
        _sessionCodeText = this.FindControl<TextBlock>("SessionCodeText")!;
        _questionText = this.FindControl<TextBlock>("QuestionText")!;
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
            Dispatcher.UIThread.Post(() => SetQuestion(q));
        });
        _connection.On<AnswerSubmitted>("AnswerSubmitted", a =>
        {
            Dispatcher.UIThread.Post(() => Tally(a.ChoiceIndex));
        });
        _connection.On<PlayTrack>("RequestPlay", p =>
        {
            _ = ForwardPlayToBackend(p);
        });

        Loaded += async (_, _) =>
        {
            try
            {
                await StartConnection();
                _connectionTextBlock.Text = "Connected";
            }
            catch (Exception ex)
            {
                _connectionTextBlock.Text = $"Connection failed: {ex.Message}";
            }
        };
        _connection.Closed += async (_) =>
        {
            _connectionTextBlock.Text = "Disconnected";
            await Task.Delay(Random.Shared.Next(0, 5) * 1000);
            await StartConnection();
        };
    }

    async Task StartConnection()
    {
        await _connection.StartAsync();
        await _connection.InvokeAsync("CreateOrJoin", _sessionCode);
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
            _rows[i].Background = new SolidColorBrush(Color.Parse("#222"));
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
        for (int i = 0; i < _tally.Length; i++)
        {
            var color = _tally[i] == max && max > 0 ? "#2e7d32" : "#222"; // green for leaders
            _rows[i].Background = new SolidColorBrush(Color.Parse(color));
        }
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
}