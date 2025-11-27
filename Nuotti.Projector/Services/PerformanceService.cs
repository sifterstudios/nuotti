using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nuotti.Projector.Services;

public class PerformanceService
{
    private readonly object _lock = new();
    private readonly Queue<double> _frameTimes = new();
    private readonly Stopwatch _frameStopwatch = new();
    private readonly Timer _performanceTimer;
    
    private const int MaxFrameTimesSamples = 60; // Track last 60 frames
    private const double TargetFrameTimeMs = 16.67; // 60 FPS target
    private const double WarningThresholdMs = 16.0; // Warn if frame time > 16ms
    private const double CriticalThresholdMs = 22.0; // Critical if frame time > 22ms (45 FPS)
    
    private bool _heavyAnimationsEnabled = true;
    private int _consecutiveSlowFrames = 0;
    private const int SlowFrameThreshold = 120; // 2 seconds at 60fps
    
    public event Action<PerformanceMetrics>? MetricsUpdated;
    public event Action<bool>? HeavyAnimationsToggled;
    
    public bool HeavyAnimationsEnabled => _heavyAnimationsEnabled;
    
    public PerformanceService()
    {
        _frameStopwatch.Start();
        
        // Update performance metrics every second
        _performanceTimer = new Timer(UpdateMetrics, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }
    
    public void RecordFrameStart()
    {
        _frameStopwatch.Restart();
    }
    
    public void RecordFrameEnd()
    {
        var frameTime = _frameStopwatch.Elapsed.TotalMilliseconds;
        
        lock (_lock)
        {
            _frameTimes.Enqueue(frameTime);
            
            // Keep only the last N samples
            while (_frameTimes.Count > MaxFrameTimesSamples)
            {
                _frameTimes.Dequeue();
            }
            
            // Check for performance issues
            CheckPerformanceThresholds(frameTime);
        }
    }
    
    private void CheckPerformanceThresholds(double frameTime)
    {
        if (frameTime > CriticalThresholdMs)
        {
            _consecutiveSlowFrames++;
            
            // If we have too many slow frames, disable heavy animations
            if (_consecutiveSlowFrames >= SlowFrameThreshold && _heavyAnimationsEnabled)
            {
                _heavyAnimationsEnabled = false;
                HeavyAnimationsToggled?.Invoke(false);
            }
        }
        else
        {
            _consecutiveSlowFrames = 0;
            
            // Re-enable heavy animations if performance improves
            if (!_heavyAnimationsEnabled && frameTime < WarningThresholdMs)
            {
                _heavyAnimationsEnabled = true;
                HeavyAnimationsToggled?.Invoke(true);
            }
        }
    }
    
    private void UpdateMetrics(object? state)
    {
        PerformanceMetrics metrics;
        
        lock (_lock)
        {
            if (_frameTimes.Count == 0)
            {
                metrics = new PerformanceMetrics(0, 0, 0, 0, true);
            }
            else
            {
                var frameTimesArray = _frameTimes.ToArray();
                var avgFrameTime = frameTimesArray.Average();
                var maxFrameTime = frameTimesArray.Max();
                var minFrameTime = frameTimesArray.Min();
                var fps = 1000.0 / avgFrameTime;
                
                metrics = new PerformanceMetrics(
                    fps,
                    avgFrameTime,
                    maxFrameTime,
                    minFrameTime,
                    _heavyAnimationsEnabled
                );
            }
        }
        
        MetricsUpdated?.Invoke(metrics);
    }
    
    public PerformanceMetrics GetCurrentMetrics()
    {
        lock (_lock)
        {
            if (_frameTimes.Count == 0)
            {
                return new PerformanceMetrics(0, 0, 0, 0, true);
            }
            
            var frameTimesArray = _frameTimes.ToArray();
            var avgFrameTime = frameTimesArray.Average();
            var maxFrameTime = frameTimesArray.Max();
            var minFrameTime = frameTimesArray.Min();
            var fps = 1000.0 / avgFrameTime;
            
            return new PerformanceMetrics(
                fps,
                avgFrameTime,
                maxFrameTime,
                minFrameTime,
                _heavyAnimationsEnabled
            );
        }
    }
    
    public bool ShouldWarnAboutPerformance()
    {
        lock (_lock)
        {
            if (_frameTimes.Count < 30) return false; // Need enough samples
            
            var recentFrames = _frameTimes.TakeLast(30).ToArray();
            var avgRecentFrameTime = recentFrames.Average();
            
            return avgRecentFrameTime > WarningThresholdMs;
        }
    }
    
    public void Dispose()
    {
        _performanceTimer?.Dispose();
    }
}

public record PerformanceMetrics(
    double Fps,
    double AvgFrameTimeMs,
    double MaxFrameTimeMs,
    double MinFrameTimeMs,
    bool HeavyAnimationsEnabled
);
