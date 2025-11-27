using System;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Nuotti.Projector.Services;

public class CursorService : IDisposable
{
    private readonly Window _window;
    private Timer? _hideTimer;
    private bool _isHidden;
    private bool _isManuallyHidden;
    private readonly TimeSpan _hideDelay = TimeSpan.FromSeconds(3);
    
    public bool IsVisible => !_isHidden && !_isManuallyHidden;
    
    public CursorService(Window window)
    {
        _window = window;
        _window.PointerMoved += OnPointerMoved;
        _window.KeyDown += OnKeyDown;
    }
    
    public void StartAutoHide()
    {
        ResetTimer();
    }
    
    public void StopAutoHide()
    {
        _hideTimer?.Dispose();
        _hideTimer = null;
        ShowCursor();
    }
    
    public void HideCursor()
    {
        if (!_isHidden && !_isManuallyHidden)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _window.Cursor = new Cursor(StandardCursorType.None);
                _isHidden = true;
            });
        }
    }
    
    public void ShowCursor()
    {
        if (_isHidden && !_isManuallyHidden)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _window.Cursor = Cursor.Default;
                _isHidden = false;
            });
        }
    }
    
    public void ToggleVisibility()
    {
        _isManuallyHidden = !_isManuallyHidden;
        
        Dispatcher.UIThread.Post(() =>
        {
            if (_isManuallyHidden)
            {
                _window.Cursor = new Cursor(StandardCursorType.None);
            }
            else
            {
                _window.Cursor = Cursor.Default;
                _isHidden = false;
                ResetTimer(); // Resume auto-hide
            }
        });
    }
    
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isManuallyHidden)
        {
            ShowCursor();
            ResetTimer();
        }
    }
    
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isManuallyHidden)
        {
            ShowCursor();
            ResetTimer();
        }
    }
    
    private void ResetTimer()
    {
        _hideTimer?.Dispose();
        _hideTimer = new Timer(OnTimerElapsed, null, _hideDelay, Timeout.InfiniteTimeSpan);
    }
    
    private void OnTimerElapsed(object? state)
    {
        HideCursor();
    }
    
    public void Dispose()
    {
        _hideTimer?.Dispose();
        if (_window != null)
        {
            _window.PointerMoved -= OnPointerMoved;
            _window.KeyDown -= OnKeyDown;
        }
    }
}
