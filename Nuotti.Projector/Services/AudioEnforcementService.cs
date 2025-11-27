using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nuotti.Projector.Services;

public class AudioEnforcementService : IDisposable
{
    private readonly Timer _monitoringTimer;
    private readonly List<string> _blockedAudioProcesses = new();
    private bool _isMonitoring = false;
    private bool _audioDetected = false;
    
    public event Action<AudioViolation>? AudioViolationDetected;
    
    public bool IsAudioBlocked => _isMonitoring;
    public bool HasDetectedAudio => _audioDetected;
    
    public AudioEnforcementService()
    {
        // Initialize blocked process list
        InitializeBlockedProcesses();
        
        // Start monitoring every 5 seconds
        _monitoringTimer = new Timer(MonitorAudioProcesses, null, 
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        
        _isMonitoring = true;
    }
    
    private void InitializeBlockedProcesses()
    {
        // Common audio processes that should not be running on a projector
        _blockedAudioProcesses.AddRange(new[]
        {
            "spotify",
            "winamp",
            "vlc",
            "wmplayer",
            "itunes",
            "musicbee",
            "foobar2000",
            "audacity",
            "discord",
            "skype",
            "teams",
            "zoom",
            "chrome", // Can play audio
            "firefox", // Can play audio
            "msedge", // Can play audio
            "steam", // Gaming platform with audio
            "obs64", // Streaming software
            "obs32"
        });
    }
    
    private void MonitorAudioProcesses(object? state)
    {
        if (!_isMonitoring) return;
        
        try
        {
            var runningProcesses = Process.GetProcesses();
            var violations = new List<AudioViolation>();
            
            foreach (var process in runningProcesses)
            {
                try
                {
                    var processName = process.ProcessName.ToLowerInvariant();
                    
                    // Check if this is a blocked audio process
                    if (_blockedAudioProcesses.Contains(processName))
                    {
                        violations.Add(new AudioViolation
                        {
                            ViolationType = AudioViolationType.BlockedProcess,
                            ProcessName = process.ProcessName,
                            ProcessId = process.Id,
                            Description = $"Audio-capable process '{process.ProcessName}' is running"
                        });
                        
                        _audioDetected = true;
                    }
                    
                    // Check for audio-related window titles (basic heuristic)
                    if (!string.IsNullOrEmpty(process.MainWindowTitle))
                    {
                        var title = process.MainWindowTitle.ToLowerInvariant();
                        if (title.Contains("music") || title.Contains("audio") || 
                            title.Contains("sound") || title.Contains("player") ||
                            title.Contains("spotify") || title.Contains("youtube"))
                        {
                            violations.Add(new AudioViolation
                            {
                                ViolationType = AudioViolationType.AudioWindow,
                                ProcessName = process.ProcessName,
                                ProcessId = process.Id,
                                WindowTitle = process.MainWindowTitle,
                                Description = $"Window with audio-related title: '{process.MainWindowTitle}'"
                            });
                            
                            _audioDetected = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ignore individual process access errors
                    Console.WriteLine($"[audio-enforcement] Process check error: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
            
            // Report violations
            foreach (var violation in violations)
            {
                AudioViolationDetected?.Invoke(violation);
            }
            
            // Platform-specific audio system checks
            CheckPlatformAudioSystems();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[audio-enforcement] Monitoring error: {ex.Message}");
        }
    }
    
    private void CheckPlatformAudioSystems()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CheckWindowsAudioSystems();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                CheckLinuxAudioSystems();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                CheckMacOSAudioSystems();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[audio-enforcement] Platform audio check error: {ex.Message}");
        }
    }
    
    private void CheckWindowsAudioSystems()
    {
        // Check for Windows audio services
        var audioServices = new[] { "AudioSrv", "AudioEndpointBuilder", "Audiosrv" };
        
        foreach (var serviceName in audioServices)
        {
            try
            {
                var processes = Process.GetProcessesByName(serviceName);
                if (processes.Length > 0)
                {
                    // Audio services are running - this is expected but we log it
                    Console.WriteLine($"[audio-enforcement] Windows audio service '{serviceName}' detected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[audio-enforcement] Service check error for {serviceName}: {ex.Message}");
            }
        }
    }
    
    private void CheckLinuxAudioSystems()
    {
        // Check for common Linux audio systems
        var audioProcesses = new[] { "pulseaudio", "pipewire", "alsa", "jack" };
        
        foreach (var processName in audioProcesses)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    Console.WriteLine($"[audio-enforcement] Linux audio system '{processName}' detected");
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[audio-enforcement] Linux audio check error for {processName}: {ex.Message}");
            }
        }
    }
    
    private void CheckMacOSAudioSystems()
    {
        // Check for macOS audio processes
        var audioProcesses = new[] { "coreaudiod", "Audio MIDI Setup" };
        
        foreach (var processName in audioProcesses)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    Console.WriteLine($"[audio-enforcement] macOS audio system '{processName}' detected");
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[audio-enforcement] macOS audio check error for {processName}: {ex.Message}");
            }
        }
    }
    
    public void AddBlockedProcess(string processName)
    {
        if (!_blockedAudioProcesses.Contains(processName.ToLowerInvariant()))
        {
            _blockedAudioProcesses.Add(processName.ToLowerInvariant());
        }
    }
    
    public void RemoveBlockedProcess(string processName)
    {
        _blockedAudioProcesses.Remove(processName.ToLowerInvariant());
    }
    
    public void StartMonitoring()
    {
        _isMonitoring = true;
        Console.WriteLine("[audio-enforcement] Audio monitoring started");
    }
    
    public void StopMonitoring()
    {
        _isMonitoring = false;
        Console.WriteLine("[audio-enforcement] Audio monitoring stopped");
    }
    
    public AudioEnforcementReport GenerateReport()
    {
        return new AudioEnforcementReport
        {
            IsMonitoring = _isMonitoring,
            HasDetectedAudio = _audioDetected,
            BlockedProcessCount = _blockedAudioProcesses.Count,
            BlockedProcesses = _blockedAudioProcesses.ToArray(),
            Platform = RuntimeInformation.OSDescription,
            LastCheckTime = DateTime.UtcNow
        };
    }
    
    public void Dispose()
    {
        _monitoringTimer?.Dispose();
        _isMonitoring = false;
    }
}

public class AudioViolation
{
    public AudioViolationType ViolationType { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string? WindowTitle { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

public enum AudioViolationType
{
    BlockedProcess,
    AudioWindow,
    SystemAudioService,
    UnknownAudioSource
}

public class AudioEnforcementReport
{
    public bool IsMonitoring { get; set; }
    public bool HasDetectedAudio { get; set; }
    public int BlockedProcessCount { get; set; }
    public string[] BlockedProcesses { get; set; } = Array.Empty<string>();
    public string Platform { get; set; } = string.Empty;
    public DateTime LastCheckTime { get; set; }
}
