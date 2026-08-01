using System;
using Nuotti.AudioEngine.Playback.Coordinator;

namespace Nuotti.AudioEngine.Tests;

public sealed class FakeMonotonicClock : IMonotonicClock
{
    public TimeSpan Elapsed { get; private set; }
    public void Advance(TimeSpan delta) => Elapsed += delta;
}

public sealed class FakeSharedTimelineAudio : ISharedTimelineAudio
{
    public bool IsPrimed { get; private set; }
    public bool IsRunning { get; private set; }
    public long FramePosition { get; private set; }
    public long BackingOffsetFrames { get; private set; }
    public long ClickOffsetFrames { get; private set; }
    public TimeSpan? ScheduledLead { get; private set; }
    public TimeSpan MeasuredLead { get; set; } = TimeSpan.FromMilliseconds(760);
    public bool UnderrunInjected { get; private set; }
    public bool DriverLostInjected { get; private set; }

    public void Prime(VerifiedPlaybackAssets assets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assets.BackingPath);
        IsPrimed = true;
        BackingOffsetFrames = assets.BackingOffsetFrames;
        FramePosition = 0;
        IsRunning = false;
    }

    public void ScheduleStart(TimeSpan plannedLead)
    {
        if (!IsPrimed) throw new InvalidOperationException("Not primed.");
        ScheduledLead = plannedLead;
    }

    public TimeSpan ReportFirstCallback()
    {
        if (ScheduledLead is null) throw new InvalidOperationException("Not scheduled.");
        IsRunning = true;
        FramePosition = 0;
        return MeasuredLead;
    }

    public void Stop() => IsRunning = false;

    public void InjectUnderrun()
    {
        UnderrunInjected = true;
        IsRunning = false;
    }

    public void InjectDriverLoss()
    {
        DriverLostInjected = true;
        IsRunning = false;
    }

    public void AdvanceFrames(long frames)
    {
        if (IsRunning) FramePosition += frames;
    }
}
