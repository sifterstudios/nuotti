using Nuotti.Projector.Models;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Nuotti.Projector.Services;

public class MonitorService
{
    public List<MonitorInfo> GetAvailableMonitors()
    {
        var monitors = new List<MonitorInfo>();

        try
        {
            // For now, return a single safe primary monitor placeholder.
            // NOTE: Returning a fake "secondary" monitor can cause the window
            // to be positioned off-screen at startup when auto-fullscreen is enabled.
            // Keep only the primary until real screen enumeration is implemented.
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
