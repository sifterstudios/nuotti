using System;
using System.Linq;
using FluentAssertions;
using Nuotti.AudioEngine.Playback.Coordinator;
using Nuotti.Contracts.V1.Protocol;
using Xunit;

namespace Nuotti.AudioEngine.Tests;

public class ShowAgentPlaybackCoordinatorTests
{
    readonly FakeMonotonicClock _clock = new();
    readonly FakeSharedTimelineAudio _audio = new();
    readonly InMemoryPlaybackJournal _journal = new();
    readonly InMemoryAnchorEmitter _anchors = new();

    ShowAgentPlaybackCoordinator Create() => new(_clock, _audio, _journal, _anchors);

    static VerifiedPlaybackAssets Assets() => new(
        "rev_song_1", "/cache/backing.wav", "/cache/click.wav", BackingOffsetFrames: 480, SampleRate: 48_000);

    static PlaybackIdentity Id(string instance = "play-1", long gen = 1) =>
        new(instance, new ControlGeneration(gen));

    [Fact]
    public void Prepare_primes_verified_assets_to_Ready()
    {
        var c = Create();
        var result = c.Prepare(Assets());
        result.Outcome.Should().Be(Outcome.Applied);
        result.State.Should().Be(PlaybackLifecycle.Ready);
        _audio.IsPrimed.Should().BeTrue();
        _journal.Entries.Should().Contain(e => e.Message == "prepare-ready");
    }

    [Fact]
    public void Start_without_Prepare_is_rejected()
    {
        var c = Create();
        var result = c.Start(Id(), DateTimeOffset.UtcNow);
        result.Outcome.Should().Be(Outcome.Rejected);
        result.Fault.Should().Be(PlaybackFault.NotPrepared);
        _audio.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Start_schedules_planned_lead_then_measured_ASIO_anchor_supersedes()
    {
        var c = Create();
        c.Prepare(Assets());
        var backendUtc = DateTimeOffset.Parse("2026-08-01T12:00:00Z");
        var start = c.Start(Id(), backendUtc);
        start.State.Should().Be(PlaybackLifecycle.Scheduled);
        _anchors.Anchors.Should().ContainSingle(a => a.State == "Scheduled" && a.Frame == 0);

        _clock.Advance(TimeSpan.FromMilliseconds(760));
        _audio.MeasuredLead = TimeSpan.FromMilliseconds(760);
        var measured = c.OnMeasuredAsioStart();
        measured.Outcome.Should().Be(Outcome.Applied);
        measured.State.Should().Be(PlaybackLifecycle.Playing);

        _anchors.Anchors.Should().Contain(a => a.State == "Playing");
        var playing = _anchors.Anchors.Last(a => a.State == "Playing");
        playing.PlaybackInstanceId.Should().Be("play-1");
        playing.ControlGeneration.Should().Be(1);
        playing.BackendUtcCorrelation.Should().Be(backendUtc);
        _journal.Entries.Should().Contain(e => e.Message.Contains("measured-asio-start"));
    }

    [Fact]
    public void Backing_and_click_share_one_frame_counter_with_offsets()
    {
        var c = Create();
        c.Prepare(Assets());
        c.Start(Id(), DateTimeOffset.UtcNow);
        c.OnMeasuredAsioStart();
        _audio.AdvanceFrames(1000);

        var (shared, backing, click) = c.TimelinePosition();
        shared.Should().Be(1000);
        backing.Should().Be(1000 - 480);
        click.Should().Be(1000);
        _audio.BackingOffsetFrames.Should().Be(480);
        _audio.ClickOffsetFrames.Should().Be(0);
    }

    [Fact]
    public void Duplicate_Start_same_identity_is_Duplicate_without_restart()
    {
        var c = Create();
        c.Prepare(Assets());
        c.Start(Id(), DateTimeOffset.UtcNow).Outcome.Should().Be(Outcome.Applied);
        var dup = c.Start(Id(), DateTimeOffset.UtcNow);
        dup.Outcome.Should().Be(Outcome.Duplicate);
        dup.Fault.Should().Be(PlaybackFault.DuplicateStart);
        _anchors.Anchors.Count(a => a.State == "Scheduled").Should().Be(1);
    }

    [Fact]
    public void Underrun_and_driver_loss_fail_safely_and_silence_output()
    {
        var c = Create();
        c.Prepare(Assets());
        c.Start(Id(), DateTimeOffset.UtcNow);
        c.OnMeasuredAsioStart();

        var underrun = c.OnUnderrun();
        underrun.State.Should().Be(PlaybackLifecycle.Failed);
        underrun.Fault.Should().Be(PlaybackFault.Underrun);
        _audio.IsRunning.Should().BeFalse();

        c.Prepare(Assets());
        c.Start(Id("play-2", 2), DateTimeOffset.UtcNow);
        c.OnMeasuredAsioStart();
        var lost = c.OnDriverLost();
        lost.Fault.Should().Be(PlaybackFault.DriverLost);
        _audio.IsRunning.Should().BeFalse();
        _audio.DriverLostInjected.Should().BeTrue();
    }

    [Fact]
    public void Emergency_stop_silences_even_while_Playing()
    {
        var c = Create();
        c.Prepare(Assets());
        c.Start(Id(), DateTimeOffset.UtcNow);
        c.OnMeasuredAsioStart();

        var stop = c.EmergencyStop();
        stop.Outcome.Should().Be(Outcome.Applied);
        stop.State.Should().Be(PlaybackLifecycle.Stopped);
        stop.Fault.Should().Be(PlaybackFault.EmergencyStop);
        _audio.IsRunning.Should().BeFalse();
        _journal.Entries.Should().Contain(e => e.Fault == PlaybackFault.EmergencyStop);
    }

    [Fact]
    public void Coordinated_Stop_halts_shared_timeline()
    {
        var c = Create();
        c.Prepare(Assets());
        c.Start(Id(), DateTimeOffset.UtcNow);
        c.OnMeasuredAsioStart();
        _audio.AdvanceFrames(100);
        c.Stop();
        _audio.IsRunning.Should().BeFalse();
        c.State.Should().Be(PlaybackLifecycle.Stopped);
    }

    [Fact]
    public void Process_loss_fails_safely()
    {
        var c = Create();
        c.Prepare(Assets());
        c.Start(Id(), DateTimeOffset.UtcNow);
        c.OnMeasuredAsioStart();

        var lost = c.OnProcessLost();
        lost.Outcome.Should().Be(Outcome.Rejected);
        lost.State.Should().Be(PlaybackLifecycle.Failed);
        lost.Fault.Should().Be(PlaybackFault.ProcessLost);
        _audio.IsRunning.Should().BeFalse();
    }
}
