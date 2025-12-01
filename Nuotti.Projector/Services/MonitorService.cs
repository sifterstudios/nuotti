using System;
using System.Collections.Generic;
using System.Linq;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Services;

public class MonitorService
{
    public List<MonitorInfo> GetAvailableMonitors()
    {
        var monitors = new List<MonitorInfo>();
        
        try
        {
            // For now, create a simple fallback monitor since Screen API is complex in Avalonia
            monitors.Add(new MonitorInfo
            {
                Id = "primary",
                Name = "Primary Display",
                Width = 1920,
                Height = 1080,
                X = 0,
                Y = 0,
                IsPrimary = true
            });
            
            // Add a secondary display for testing
            monitors.Add(new MonitorInfo
            {
                Id = "secondary",
                Name = "Secondary Display",
                Width = 1920,
                Height = 1080,
                X = 1920,
                Y = 0,
                IsPrimary = false
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting monitors: {ex.Message}");
            // Fallback to primary screen only
            monitors.Add(new MonitorInfo
            {
                Id = "fallback",
                Name = "Fallback Display",
                Width = 1920,
                Height = 1080,
                X = 0,
                Y = 0,
                IsPrimary = true
            });
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
