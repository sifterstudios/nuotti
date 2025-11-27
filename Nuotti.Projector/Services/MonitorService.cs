using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Services;

public class MonitorService
{
    public List<MonitorInfo> GetAvailableMonitors()
    {
        var monitors = new List<MonitorInfo>();
        
        try
        {
            var screens = Screen.All;
            var primaryScreen = Screen.Primary;
            
            foreach (var screen in screens)
            {
                var monitor = new MonitorInfo
                {
                    Id = screen.Handle.ToString(),
                    Name = screen.DisplayName ?? $"Display {monitors.Count + 1}",
                    Width = (int)screen.Bounds.Width,
                    Height = (int)screen.Bounds.Height,
                    X = (int)screen.Bounds.X,
                    Y = (int)screen.Bounds.Y,
                    IsPrimary = screen.Equals(primaryScreen)
                };
                monitors.Add(monitor);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting monitors: {ex.Message}");
            // Fallback to primary screen only
            if (Screen.Primary != null)
            {
                var primary = Screen.Primary;
                monitors.Add(new MonitorInfo
                {
                    Id = "primary",
                    Name = "Primary Display",
                    Width = (int)primary.Bounds.Width,
                    Height = (int)primary.Bounds.Height,
                    X = (int)primary.Bounds.X,
                    Y = (int)primary.Bounds.Y,
                    IsPrimary = true
                });
            }
        }
        
        return monitors;
    }
    
    public MonitorInfo? GetMonitorById(string monitorId)
    {
        return GetAvailableMonitors().FirstOrDefault(m => m.Id == monitorId);
    }
    
    public MonitorInfo GetPrimaryMonitor()
    {
        var monitors = GetAvailableMonitors();
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault() ?? new MonitorInfo
        {
            Id = "fallback",
            Name = "Fallback Display",
            Width = 1920,
            Height = 1080,
            IsPrimary = true
        };
    }
}
