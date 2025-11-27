using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Nuotti.Projector.Views;

public partial class EmptyStateView : UserControl
{
    private readonly TextBlock _emptyIcon;
    private readonly TextBlock _emptyTitle;
    private readonly TextBlock _emptyMessage;
    private readonly Button _actionButton;
    private readonly StackPanel _loadingIndicator;
    
    public event Action? ActionRequested;
    
    public EmptyStateView()
    {
        InitializeComponent();
        
        _emptyIcon = this.FindControl<TextBlock>("EmptyIcon")!;
        _emptyTitle = this.FindControl<TextBlock>("EmptyTitle")!;
        _emptyMessage = this.FindControl<TextBlock>("EmptyMessage")!;
        _actionButton = this.FindControl<Button>("ActionButton")!;
        _loadingIndicator = this.FindControl<StackPanel>("LoadingIndicator")!;
    }
    
    public void ShowEmptyState(EmptyStateType emptyType, string? customMessage = null, string? actionText = null)
    {
        switch (emptyType)
        {
            case EmptyStateType.WaitingForGame:
                ShowWaitingForGame(customMessage);
                break;
                
            case EmptyStateType.NoPlayers:
                ShowNoPlayers(customMessage);
                break;
                
            case EmptyStateType.NoSongs:
                ShowNoSongs(customMessage);
                break;
                
            case EmptyStateType.NoScores:
                ShowNoScores(customMessage);
                break;
                
            case EmptyStateType.Loading:
                ShowLoading(customMessage);
                break;
                
            case EmptyStateType.Disconnected:
                ShowDisconnected(customMessage);
                break;
                
            case EmptyStateType.Generic:
            default:
                ShowGeneric(customMessage);
                break;
        }
        
        if (!string.IsNullOrEmpty(actionText))
        {
            _actionButton.Content = actionText;
            _actionButton.IsVisible = true;
        }
        else
        {
            _actionButton.IsVisible = false;
        }
    }
    
    private void ShowWaitingForGame(string? message)
    {
        _emptyIcon.Text = "⏳";
        _emptyTitle.Text = "Waiting for Game";
        _emptyMessage.Text = message ?? "The game hasn't started yet. Please wait for the host to begin.";
        _loadingIndicator.IsVisible = false;
    }
    
    private void ShowNoPlayers(string? message)
    {
        _emptyIcon.Text = "👥";
        _emptyTitle.Text = "No Players Yet";
        _emptyMessage.Text = message ?? "Waiting for players to join the game session.";
        _loadingIndicator.IsVisible = false;
    }
    
    private void ShowNoSongs(string? message)
    {
        _emptyIcon.Text = "🎵";
        _emptyTitle.Text = "No Songs Available";
        _emptyMessage.Text = message ?? "There are no songs in the current playlist.";
        _loadingIndicator.IsVisible = false;
    }
    
    private void ShowNoScores(string? message)
    {
        _emptyIcon.Text = "🏆";
        _emptyTitle.Text = "No Scores Yet";
        _emptyMessage.Text = message ?? "Scores will appear here once the game begins.";
        _loadingIndicator.IsVisible = false;
    }
    
    private void ShowLoading(string? message)
    {
        _emptyIcon.Text = "⏳";
        _emptyTitle.Text = "Loading";
        _emptyMessage.Text = message ?? "Please wait while we load the content.";
        _loadingIndicator.IsVisible = true;
    }
    
    private void ShowDisconnected(string? message)
    {
        _emptyIcon.Text = "📡";
        _emptyTitle.Text = "Disconnected";
        _emptyMessage.Text = message ?? "Connection lost. Attempting to reconnect...";
        _loadingIndicator.IsVisible = false;
    }
    
    private void ShowGeneric(string? message)
    {
        _emptyIcon.Text = "📭";
        _emptyTitle.Text = "Nothing here yet";
        _emptyMessage.Text = message ?? "We're waiting for something to show up here.";
        _loadingIndicator.IsVisible = false;
    }
    
    public void ShowLoadingIndicator(bool show)
    {
        _loadingIndicator.IsVisible = show;
    }
    
    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        ActionRequested?.Invoke();
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

public enum EmptyStateType
{
    Generic,
    WaitingForGame,
    NoPlayers,
    NoSongs,
    NoScores,
    Loading,
    Disconnected
}
