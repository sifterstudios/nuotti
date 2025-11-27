using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Services;

public class HotplugService : IDisposable
{
    private readonly Timer _monitoringTimer;
    private readonly MonitorService _monitorService;
    private List<MonitorInfo> _lastKnownMonitors = new();
    private bool _isMonitoring = false;
    
    public event Action<HotplugEvent>? MonitorChanged;
    public event Action<MonitorInfo>? MonitorConnected;
    public event Action<MonitorInfo>? MonitorDisconnected;
    public event Action<List<MonitorInfo>>? MonitorListChanged;
    
    public bool IsMonitoring => _isMonitoring;
    public IReadOnlyList<MonitorInfo> CurrentMonitors => _lastKnownMonitors.AsReadOnly();
    
    public HotplugService(MonitorService monitorService)
    {
        _monitorService = monitorService;
        
        // Start monitoring every 2 seconds
        _monitoringTimer = new Timer(CheckForMonitorChanges, null, 
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        
        _isMonitoring = true;
        
        // Initialize with current monitors
        _ = Task.Run(InitializeCurrentMonitors);
    }
    
    private async Task InitializeCurrentMonitors()
    {
        try
        {
            _lastKnownMonitors = _monitorService.GetAvailableMonitors().ToList();
            Console.WriteLine($"[hotplug] Initialized with {_lastKnownMonitors.Count} monitors");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[hotplug] Initialization error: {ex.Message}");
        }
    }
    
    private void CheckForMonitorChanges(object? state)
    {
        if (!_isMonitoring) return;
        
        try
        {
            var currentMonitors = _monitorService.GetAvailableMonitors().ToList();
            
            // Compare with last known state
            var changes = DetectChanges(_lastKnownMonitors, currentMonitors);
            
            if (changes.Any())
            {
                ProcessMonitorChanges(changes, currentMonitors);
                _lastKnownMonitors = currentMonitors;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[hotplug] Monitor check error: {ex.Message}");
        }
    }
    
    private List<HotplugEvent> DetectChanges(List<MonitorInfo> oldMonitors, List<MonitorInfo> newMonitors)
    {
        var events = new List<HotplugEvent>();
        
        // Find disconnected monitors
        foreach (var oldMonitor in oldMonitors)
        {
            if (!newMonitors.Any(m => m.Id == oldMonitor.Id))
            {
                events.Add(new HotplugEvent
                {
                    EventType = HotplugEventType.Disconnected,
                    Monitor = oldMonitor,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        // Find newly connected monitors
        foreach (var newMonitor in newMonitors)
        {
            if (!oldMonitors.Any(m => m.Id == newMonitor.Id))
            {
                events.Add(new HotplugEvent
                {
                    EventType = HotplugEventType.Connected,
                    Monitor = newMonitor,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        // Check for configuration changes (resolution, position)
        foreach (var newMonitor in newMonitors)
        {
            var oldMonitor = oldMonitors.FirstOrDefault(m => m.Id == newMonitor.Id);
            if (oldMonitor != null)
            {
                if (oldMonitor.Width != newMonitor.Width || 
                    oldMonitor.Height != newMonitor.Height ||
                    oldMonitor.X != newMonitor.X || 
                    oldMonitor.Y != newMonitor.Y)
                {
                    events.Add(new HotplugEvent
                    {
                        EventType = HotplugEventType.ConfigurationChanged,
                        Monitor = newMonitor,
                        PreviousMonitor = oldMonitor,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }
        
        return events;
    }
    
    private void ProcessMonitorChanges(List<HotplugEvent> changes, List<MonitorInfo> currentMonitors)
    {
        foreach (var change in changes)
        {
            var eventDescription = change.EventType switch
            {
                HotplugEventType.Connected => $"Monitor connected: {change.Monitor.Name} ({change.Monitor.Width}x{change.Monitor.Height})",
                HotplugEventType.Disconnected => $"Monitor disconnected: {change.Monitor.Name}",
                HotplugEventType.ConfigurationChanged => $"Monitor configuration changed: {change.Monitor.Name}",
                _ => $"Monitor event: {change.EventType}"
            };
            
            Console.WriteLine($"[hotplug] {eventDescription}");
            
            // Fire specific events
            switch (change.EventType)
            {
                case HotplugEventType.Connected:
                    MonitorConnected?.Invoke(change.Monitor);
                    break;
                case HotplugEventType.Disconnected:
                    MonitorDisconnected?.Invoke(change.Monitor);
                    break;
            }
            
            // Fire general change event
            MonitorChanged?.Invoke(change);
        }
        
        // Fire list changed event
        MonitorListChanged?.Invoke(currentMonitors);
    }
    
    public MonitorInfo? FindSafeMonitorFallback(string? preferredMonitorId = null)
    {
        var monitors = _lastKnownMonitors;
        
        if (monitors.Count == 0)
        {
            return null;
        }
        
        // Try preferred monitor first
        if (!string.IsNullOrEmpty(preferredMonitorId))
        {
            var preferred = monitors.FirstOrDefault(m => m.Id == preferredMonitorId);
            if (preferred != null)
            {
                return preferred;
            }
        }
        
        // Fall back to primary monitor
        var primary = monitors.FirstOrDefault(m => m.IsPrimary);
        if (primary != null)
        {
            return primary;
        }
        
        // Fall back to first available monitor
        return monitors.First();
    }
    
    public bool IsMonitorAvailable(string monitorId)
    {
        return _lastKnownMonitors.Any(m => m.Id == monitorId);
    }
    
    public HotplugReport GenerateReport()
    {
        return new HotplugReport
        {
            IsMonitoring = _isMonitoring,
            MonitorCount = _lastKnownMonitors.Count,
            Monitors = _lastKnownMonitors.ToArray(),
            PrimaryMonitor = _lastKnownMonitors.FirstOrDefault(m => m.IsPrimary),
            LastCheckTime = DateTime.UtcNow
        };
    }
    
    public void StartMonitoring()
    {
        _isMonitoring = true;
        Console.WriteLine("[hotplug] Monitor hotplug detection started");
    }
    
    public void StopMonitoring()
    {
        _isMonitoring = false;
        Console.WriteLine("[hotplug] Monitor hotplug detection stopped");
    }
    
    public void Dispose()
    {
        _monitoringTimer?.Dispose();
        _isMonitoring = false;
    }
}

public class HotplugEvent
{
    public HotplugEventType EventType { get; set; }
    public MonitorInfo Monitor { get; set; } = new();
    public MonitorInfo? PreviousMonitor { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum HotplugEventType
{
    Connected,
    Disconnected,
    ConfigurationChanged
}

public class HotplugReport
{
    public bool IsMonitoring { get; set; }
    public int MonitorCount { get; set; }
    public MonitorInfo[] Monitors { get; set; } = Array.Empty<MonitorInfo>();
    public MonitorInfo? PrimaryMonitor { get; set; }
    public DateTime LastCheckTime { get; set; }
}
