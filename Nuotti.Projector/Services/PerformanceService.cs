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
    private readonly Queue<(double FrameTimeMs, DateTimeOffset Timestamp)> _timedFrameTimes = new();
    private readonly Stopwatch _frameStopwatch = new();
    private readonly Timer _performanceTimer;
    
    private const int MaxFrameTimesSamples = 60; // Track last 60 frames
    private const double TargetFrameTimeMs = 16.67; // 60 FPS target
    private const double WarningThresholdMs = 16.0; // Warn if frame time > 16ms
    private const double CriticalThresholdMs = 22.0; // Critical if frame time > 22ms (45 FPS)
    private const int Last10SecondsWindow = 10; // Track metrics for last 10 seconds
    
    private bool _heavyAnimationsEnabled = true;
    private int _consecutiveSlowFrames = 0;
    private const int SlowFrameThreshold = 120; // 2 seconds at 60fps
    
#if DEBUG
    private DateTimeOffset _lastConsoleLogTime = DateTimeOffset.MinValue;
    private const int ConsoleLogIntervalSeconds = 10; // Log every 10 seconds in DEV mode
#endif
    
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
        var now = DateTimeOffset.UtcNow;
        
        lock (_lock)
        {
            _frameTimes.Enqueue(frameTime);
            
            // Keep only the last N samples
            while (_frameTimes.Count > MaxFrameTimesSamples)
            {
                _frameTimes.Dequeue();
            }
            
            // Track timed frame samples for 10-second window analysis
            _timedFrameTimes.Enqueue((frameTime, now));
            
            // Remove frames older than 10 seconds
            var cutoffTime = now.AddSeconds(-Last10SecondsWindow);
            while (_timedFrameTimes.Count > 0 && _timedFrameTimes.Peek().Timestamp < cutoffTime)
            {
                _timedFrameTimes.Dequeue();
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
            // Get metrics for last 10 seconds
            var last10SecFrames = _timedFrameTimes.ToArray();
            
            if (last10SecFrames.Length == 0 && _frameTimes.Count == 0)
            {
                metrics = new PerformanceMetrics(0, 0, 0, 0, 0, true);
            }
            else
            {
                double[] frameTimesArray;
                if (last10SecFrames.Length > 0)
                {
                    frameTimesArray = last10SecFrames.Select(f => f.FrameTimeMs).ToArray();
                }
                else
                {
                    frameTimesArray = _frameTimes.ToArray();
                }
                
                var avgFrameTime = frameTimesArray.Average();
                var maxFrameTime = frameTimesArray.Max();
                var minFrameTime = frameTimesArray.Min();
                var fps = 1000.0 / avgFrameTime;
                var longestFrame = maxFrameTime;
                
                metrics = new PerformanceMetrics(
                    fps,
                    avgFrameTime,
                    maxFrameTime,
                    minFrameTime,
                    longestFrame,
                    _heavyAnimationsEnabled
                );
            }
        }
        
        MetricsUpdated?.Invoke(metrics);
        
        // DEV-only console logging of performance stats (every 10 seconds)
#if DEBUG
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastConsoleLogTime).TotalSeconds >= ConsoleLogIntervalSeconds)
        {
            System.Console.WriteLine($"[Projector Performance] FPS: {metrics.Fps:F1}, AvgFrameTime: {metrics.AvgFrameTimeMs:F2}ms, LongestFrame(10s): {metrics.LongestFrameMs:F2}ms");
            _lastConsoleLogTime = now;
        }
#endif
    }
    
    public PerformanceMetrics GetCurrentMetrics()
    {
        lock (_lock)
        {
            // Get metrics for last 10 seconds
            var last10SecFrames = _timedFrameTimes.ToArray();
            
            if (last10SecFrames.Length == 0 && _frameTimes.Count == 0)
            {
                return new PerformanceMetrics(0, 0, 0, 0, 0, true);
            }
            
            // Use last 10 seconds if available, otherwise fall back to all frame times
            double[] frameTimesArray;
            if (last10SecFrames.Length > 0)
            {
                frameTimesArray = last10SecFrames.Select(f => f.FrameTimeMs).ToArray();
            }
            else
            {
                frameTimesArray = _frameTimes.ToArray();
            }
            
            var avgFrameTime = frameTimesArray.Average();
            var maxFrameTime = frameTimesArray.Max();
            var minFrameTime = frameTimesArray.Min();
            var fps = 1000.0 / avgFrameTime;
            var longestFrame = maxFrameTime;
            
            return new PerformanceMetrics(
                fps,
                avgFrameTime,
                maxFrameTime,
                minFrameTime,
                longestFrame,
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
    double LongestFrameMs, // Longest frame in the last 10 seconds
    bool HeavyAnimationsEnabled
);
