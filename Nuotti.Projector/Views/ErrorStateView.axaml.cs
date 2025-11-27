using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Nuotti.Projector.Views;

public partial class ErrorStateView : UserControl
{
    private readonly TextBlock _errorIcon;
    private readonly TextBlock _errorTitle;
    private readonly TextBlock _errorMessage;
    private readonly TextBlock _errorDetails;
    private readonly Border _errorDetailsContainer;
    private readonly Button _detailsButton;
    
    private bool _detailsVisible = false;
    
    public event Action? RetryRequested;
    public event Action? BackToLobbyRequested;
    
    public ErrorStateView()
    {
        InitializeComponent();
        
        _errorIcon = this.FindControl<TextBlock>("ErrorIcon")!;
        _errorTitle = this.FindControl<TextBlock>("ErrorTitle")!;
        _errorMessage = this.FindControl<TextBlock>("ErrorMessage")!;
        _errorDetails = this.FindControl<TextBlock>("ErrorDetails")!;
        _errorDetailsContainer = this.FindControl<Border>("ErrorDetailsContainer")!;
        _detailsButton = this.FindControl<Button>("DetailsButton")!;
    }
    
    public void ShowError(ErrorType errorType, string message, string? details = null)
    {
        switch (errorType)
        {
            case ErrorType.NetworkConnection:
                ShowNetworkError(message, details);
                break;
                
            case ErrorType.SessionNotFound:
                ShowSessionError(message, details);
                break;
                
            case ErrorType.InvalidData:
                ShowDataError(message, details);
                break;
                
            case ErrorType.ThemeError:
                ShowThemeError(message, details);
                break;
                
            case ErrorType.FontError:
                ShowFontError(message, details);
                break;
                
            case ErrorType.Generic:
            default:
                ShowGenericError(message, details);
                break;
        }
        
        if (!string.IsNullOrEmpty(details))
        {
            _errorDetails.Text = details;
            _detailsButton.IsVisible = true;
        }
        else
        {
            _detailsButton.IsVisible = false;
        }
    }
    
    private void ShowNetworkError(string message, string? details)
    {
        _errorIcon.Text = "🌐";
        _errorTitle.Text = "Connection Problem";
        _errorMessage.Text = message.IsNullOrEmpty() 
            ? "Unable to connect to the game server. Please check your network connection."
            : message;
    }
    
    private void ShowSessionError(string message, string? details)
    {
        _errorIcon.Text = "🔍";
        _errorTitle.Text = "Session Not Found";
        _errorMessage.Text = message.IsNullOrEmpty()
            ? "The game session could not be found. It may have ended or the session code is incorrect."
            : message;
    }
    
    private void ShowDataError(string message, string? details)
    {
        _errorIcon.Text = "📄";
        _errorTitle.Text = "Data Error";
        _errorMessage.Text = message.IsNullOrEmpty()
            ? "There was a problem with the game data. Some information may be missing or corrupted."
            : message;
    }
    
    private void ShowThemeError(string message, string? details)
    {
        _errorIcon.Text = "🎨";
        _errorTitle.Text = "Display Error";
        _errorMessage.Text = message.IsNullOrEmpty()
            ? "There was a problem loading the display theme. The app may not look as expected."
            : message;
    }
    
    private void ShowFontError(string message, string? details)
    {
        _errorIcon.Text = "🔤";
        _errorTitle.Text = "Font Loading Error";
        _errorMessage.Text = message.IsNullOrEmpty()
            ? "Some fonts could not be loaded. Text may appear different than expected."
            : message;
    }
    
    private void ShowGenericError(string message, string? details)
    {
        _errorIcon.Text = "⚠️";
        _errorTitle.Text = "Something went wrong";
        _errorMessage.Text = message.IsNullOrEmpty()
            ? "We encountered an unexpected error. Please try again."
            : message;
    }
    
    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        RetryRequested?.Invoke();
    }
    
    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        BackToLobbyRequested?.Invoke();
    }
    
    private void OnDetailsClick(object? sender, RoutedEventArgs e)
    {
        _detailsVisible = !_detailsVisible;
        _errorDetailsContainer.IsVisible = _detailsVisible;
        _detailsButton.Content = _detailsVisible ? "🔼 Hide Details" : "🔍 Show Details";
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

public enum ErrorType
{
    Generic,
    NetworkConnection,
    SessionNotFound,
    InvalidData,
    ThemeError,
    FontError
}

public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? str) => string.IsNullOrEmpty(str);
}
