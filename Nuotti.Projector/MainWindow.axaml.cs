using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
namespace Nuotti.Projector;

public partial class MainWindow : Window
{
    readonly HubConnection _connection;
    readonly TextBlock? _connectionTextBlock;
    public MainWindow()
    {
        var songTextBlock = this.FindControl<TextBlock>("SongText");
        _connectionTextBlock = this.FindControl<TextBlock>("ConnectionStatus");
        Debug.Assert(_connectionTextBlock != null, nameof(_connectionTextBlock) + " != null");
        
        InitializeComponent();
        _connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5240/gameHub")
            .WithAutomaticReconnect()
            .Build();
        _connection.On<string>("ReceiveSongUpdate", song =>
        {
            if (songTextBlock != null) songTextBlock.Text = song;
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
    }

    public async Task StopConnection()
    {
        await _connection.StopAsync();
    }

    void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    async void TestButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _connection.InvokeAsync("SendSongUpdate", "Test Song Title");
        }
        catch (Exception ex)
        {
            if (_connectionTextBlock != null) _connectionTextBlock.Text = $"Error: {ex.Message}";
        }
    }
}