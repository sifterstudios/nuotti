using System;
using Avalonia;
using Avalonia.Controls;

namespace Nuotti.Projector.Services;

public class SafeAreaService
{
    private double _safeAreaMargin = 0.05; // 5% default
    private bool _showSafeAreaFrame = false;
    
    public double SafeAreaMargin 
    { 
        get => _safeAreaMargin;
        set => _safeAreaMargin = Math.Max(0, Math.Min(0.25, value)); // Clamp between 0% and 25%
    }
    
    public bool ShowSafeAreaFrame 
    { 
        get => _showSafeAreaFrame;
        set => _showSafeAreaFrame = value;
    }
    
    public Thickness GetSafeAreaMargin(Size windowSize)
    {
        var horizontalMargin = windowSize.Width * _safeAreaMargin;
        var verticalMargin = windowSize.Height * _safeAreaMargin;
        
        return new Thickness(horizontalMargin, verticalMargin, horizontalMargin, verticalMargin);
    }
    
    public Rect GetSafeAreaBounds(Size windowSize)
    {
        var margin = GetSafeAreaMargin(windowSize);
        return new Rect(
            margin.Left,
            margin.Top,
            windowSize.Width - margin.Left - margin.Right,
            windowSize.Height - margin.Top - margin.Bottom
        );
    }
    
    public void ApplySafeAreaToControl(Control control, Size windowSize)
    {
        var margin = GetSafeAreaMargin(windowSize);
        control.Margin = margin;
    }
}
