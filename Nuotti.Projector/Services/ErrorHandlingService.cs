using System;
using System.Threading.Tasks;
using Nuotti.Projector.Views;

namespace Nuotti.Projector.Services;

public class ErrorHandlingService
{
    public event Action<ErrorStateView>? ErrorOccurred;
    public event Action<EmptyStateView>? EmptyStateRequired;
    public event Action? RetryRequested;
    public event Action? BackToLobbyRequested;
    
    public void ShowError(ErrorType errorType, string message, string? details = null, Exception? exception = null)
    {
        var errorView = new ErrorStateView();
        
        // Add exception details if available
        var fullDetails = details;
        if (exception != null)
        {
            var exceptionDetails = $"Exception: {exception.GetType().Name}\nMessage: {exception.Message}";
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                exceptionDetails += $"\nStack Trace:\n{exception.StackTrace}";
            }
            
            fullDetails = string.IsNullOrEmpty(details) 
                ? exceptionDetails 
                : $"{details}\n\n{exceptionDetails}";
        }
        
        errorView.ShowError(errorType, message, fullDetails);
        errorView.RetryRequested += () => RetryRequested?.Invoke();
        errorView.BackToLobbyRequested += () => BackToLobbyRequested?.Invoke();
        
        ErrorOccurred?.Invoke(errorView);
    }
    
    public void ShowEmptyState(EmptyStateType emptyType, string? message = null, string? actionText = null)
    {
        var emptyView = new EmptyStateView();
        emptyView.ShowEmptyState(emptyType, message, actionText);
        emptyView.ActionRequested += () => RetryRequested?.Invoke();
        
        EmptyStateRequired?.Invoke(emptyView);
    }
    
    public void HandleNetworkError(Exception exception, string context = "")
    {
        var message = "Unable to connect to the game server. Please check your network connection and try again.";
        if (!string.IsNullOrEmpty(context))
        {
            message = $"Network error during {context}. {message}";
        }
        
        ShowError(ErrorType.NetworkConnection, message, null, exception);
    }
    
    public void HandleSessionError(string sessionCode, Exception? exception = null)
    {
        var message = $"Session '{sessionCode}' could not be found or has ended. Please check the session code and try again.";
        ShowError(ErrorType.SessionNotFound, message, null, exception);
    }
    
    public void HandleDataError(string dataType, Exception? exception = null)
    {
        var message = $"There was a problem loading {dataType}. Some information may be missing or incorrect.";
        ShowError(ErrorType.InvalidData, message, null, exception);
    }
    
    public void HandleThemeError(Exception exception)
    {
        var message = "There was a problem loading the display theme. The app may not look as expected, but functionality should not be affected.";
        ShowError(ErrorType.ThemeError, message, null, exception);
    }
    
    public void HandleFontError(Exception exception)
    {
        var message = "Some fonts could not be loaded. Text may appear different than expected, but the app will continue to work normally.";
        ShowError(ErrorType.FontError, message, null, exception);
    }
    
    public async Task<T?> ExecuteWithErrorHandling<T>(
        Func<Task<T>> operation, 
        string operationName,
        T? fallbackValue = default)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            ShowError(ErrorType.Generic, 
                $"Error during {operationName}. Please try again.", 
                null, 
                ex);
            return fallbackValue;
        }
    }
    
    public async Task ExecuteWithErrorHandling(
        Func<Task> operation, 
        string operationName)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            ShowError(ErrorType.Generic, 
                $"Error during {operationName}. Please try again.", 
                null, 
                ex);
        }
    }
    
    public void ExecuteWithErrorHandling(
        Action operation, 
        string operationName)
    {
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            ShowError(ErrorType.Generic, 
                $"Error during {operationName}. Please try again.", 
                null, 
                ex);
        }
    }
}
