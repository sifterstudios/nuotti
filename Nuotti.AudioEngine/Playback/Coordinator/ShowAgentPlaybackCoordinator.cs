using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.AudioEngine.Playback.Coordinator;

public enum PlaybackLifecycle
{
    Idle,
    Preparing,
    Ready,
    Scheduled,
    Playing,
    Completed,
    Stopped,
    Failed
}

public enum PlaybackFault
{
    None,
    NotPrepared,
    DuplicateStart,
    StaleGeneration,
    Underrun,
    DriverLost,
    ProcessLost,
    EmergencyStop
}

public sealed record VerifiedPlaybackAssets(
    string SongPackageRevisionId,
    string BackingPath,
    string? ClickPath,
    long BackingOffsetFrames,
    int SampleRate);

public sealed record PlaybackAnchorRecord(
    string PlaybackInstanceId,
    string SongPackageRevisionId,
    int SampleRate,
    long Frame,
    TimeSpan EngineMonotonicTimestamp,
    DateTimeOffset BackendUtcCorrelation,
    string State,
    double Rate,
    long Sequence,
    long ControlGeneration);

public sealed record JournalEntry(
    TimeSpan At,
    PlaybackLifecycle State,
    string Message,
    PlaybackFault Fault = PlaybackFault.None);

public sealed record CoordinatorResult(Outcome Outcome, PlaybackLifecycle State, PlaybackFault Fault, string? Detail = null);

public interface IMonotonicClock
{
    TimeSpan Elapsed { get; }
    void Advance(TimeSpan delta);
}

public interface ISharedTimelineAudio
{
    bool IsPrimed { get; }
    bool IsRunning { get; }
    long FramePosition { get; }
    long BackingOffsetFrames { get; }
    long ClickOffsetFrames { get; }
    void Prime(VerifiedPlaybackAssets assets);
    void ScheduleStart(TimeSpan plannedLead);
    /// <summary>Simulates the first ASIO callback; returns the measured start offset from schedule.</summary>
    TimeSpan ReportFirstCallback();
    void Stop();
    void InjectUnderrun();
    void InjectDriverLoss();
}

public interface IPlaybackJournal
{
    IReadOnlyList<JournalEntry> Entries { get; }
    void Append(JournalEntry entry);
}

public interface IAnchorEmitter
{
    IReadOnlyList<PlaybackAnchorRecord> Anchors { get; }
    void Emit(PlaybackAnchorRecord anchor);
}

/// <summary>
/// Stage-grade Show Agent playback coordinator. Owns lifecycle, shared-timeline scheduling,
/// measured-anchor supersession, acknowledgements, journal, and safe fault outcomes.
/// Audio hardware stays behind <see cref="ISharedTimelineAudio"/>.
/// </summary>
public sealed class ShowAgentPlaybackCoordinator(
    IMonotonicClock clock,
    ISharedTimelineAudio audio,
    IPlaybackJournal journal,
    IAnchorEmitter anchors,
    TimeSpan? defaultStartLead = null)
{
    public static TimeSpan DefaultStartLead { get; } = TimeSpan.FromMilliseconds(750);

    readonly TimeSpan _startLead = defaultStartLead ?? DefaultStartLead;
    string? _activeInstanceId;
    ControlGeneration _activeGeneration = ControlGeneration.Initial;
    string? _revisionId;
    int _sampleRate = 48_000;
    long _anchorSequence;
    TimeSpan? _plannedStartAt;
    DateTimeOffset _backendUtc;

    public PlaybackLifecycle State { get; private set; } = PlaybackLifecycle.Idle;
    public PlaybackFault LastFault { get; private set; } = PlaybackFault.None;
    public string? ActivePlaybackInstanceId => _activeInstanceId;
    public ControlGeneration ActiveControlGeneration => _activeGeneration;

    public CoordinatorResult Prepare(VerifiedPlaybackAssets assets)
    {
        State = PlaybackLifecycle.Preparing;
        journal.Append(new(clock.Elapsed, State, "prepare-begin"));
        try
        {
            audio.Prime(assets);
            _revisionId = assets.SongPackageRevisionId;
            _sampleRate = assets.SampleRate;
            State = PlaybackLifecycle.Ready;
            LastFault = PlaybackFault.None;
            journal.Append(new(clock.Elapsed, State, "prepare-ready"));
            return new(Outcome.Applied, State, LastFault);
        }
        catch (Exception ex)
        {
            return Fail(PlaybackFault.DriverLost, $"prepare-failed: {ex.Message}");
        }
    }

    public CoordinatorResult Start(
        PlaybackIdentity identity,
        DateTimeOffset backendUtcCorrelation,
        ControlGeneration? expectedGeneration = null)
    {
        if (_activeInstanceId == identity.PlaybackInstanceId
            && _activeGeneration.Value == identity.ControlGeneration.Value
            && State is PlaybackLifecycle.Scheduled or PlaybackLifecycle.Playing)
        {
            return new(Outcome.Duplicate, State, PlaybackFault.DuplicateStart, "Duplicate Start.");
        }

        if (State is not (PlaybackLifecycle.Ready or PlaybackLifecycle.Scheduled))
            return Reject(PlaybackFault.NotPrepared, "Start requires Ready (prepared) state.");

        if (expectedGeneration is { } gen && gen.Value != _activeGeneration.Value && _activeInstanceId is not null)
            return Reject(PlaybackFault.StaleGeneration, "Stale control generation.");

        _activeInstanceId = identity.PlaybackInstanceId;
        _activeGeneration = identity.ControlGeneration;
        _backendUtc = backendUtcCorrelation;
        _plannedStartAt = clock.Elapsed + _startLead;
        audio.ScheduleStart(_startLead);
        State = PlaybackLifecycle.Scheduled;
        journal.Append(new(clock.Elapsed, State, $"scheduled lead={_startLead.TotalMilliseconds:0}ms"));
        EmitAnchor("Scheduled", frame: 0, rate: 0);
        return new(Outcome.Applied, State, PlaybackFault.None);
    }

    /// <summary>
    /// Called when the shared ASIO stream delivers its first callback. The measured time supersedes
    /// the planned scheduled anchor.
    /// </summary>
    public CoordinatorResult OnMeasuredAsioStart()
    {
        if (State != PlaybackLifecycle.Scheduled || _activeInstanceId is null)
            return Reject(PlaybackFault.NotPrepared, "Measured start requires Scheduled state.");

        var measuredLead = audio.ReportFirstCallback();
        State = PlaybackLifecycle.Playing;
        journal.Append(new(clock.Elapsed, State,
            $"measured-asio-start planned-at={_plannedStartAt?.TotalMilliseconds:0}ms measured-lead={measuredLead.TotalMilliseconds:0}ms"));
        EmitAnchor("Playing", frame: audio.FramePosition, rate: 1);
        return new(Outcome.Applied, State, PlaybackFault.None);
    }

    public CoordinatorResult Stop()
    {
        audio.Stop();
        State = PlaybackLifecycle.Stopped;
        LastFault = PlaybackFault.None;
        journal.Append(new(clock.Elapsed, State, "stop"));
        EmitAnchor("Completed", frame: audio.FramePosition, rate: 0);
        return new(Outcome.Applied, State, LastFault);
    }

    public CoordinatorResult EmergencyStop()
    {
        audio.Stop();
        State = PlaybackLifecycle.Stopped;
        LastFault = PlaybackFault.EmergencyStop;
        journal.Append(new(clock.Elapsed, State, "emergency-stop", LastFault));
        EmitAnchor("Completed", frame: audio.FramePosition, rate: 0);
        return new(Outcome.Applied, State, LastFault, "Emergency stop — output silenced.");
    }

    public CoordinatorResult OnUnderrun()
    {
        audio.Stop();
        return Fail(PlaybackFault.Underrun, "Shared-timeline underrun.");
    }

    public CoordinatorResult OnDriverLost()
    {
        audio.InjectDriverLoss();
        audio.Stop();
        return Fail(PlaybackFault.DriverLost, "ASIO driver/process lost.");
    }

    public CoordinatorResult OnProcessLost()
    {
        audio.Stop();
        return Fail(PlaybackFault.ProcessLost, "Audio process lost.");
    }

    /// <summary>Shared frame counter positions for backing and click buses.</summary>
    public (long SharedFrame, long BackingFrame, long ClickFrame) TimelinePosition()
    {
        var shared = audio.FramePosition;
        return (shared, Math.Max(0, shared - audio.BackingOffsetFrames), Math.Max(0, shared - audio.ClickOffsetFrames));
    }

    CoordinatorResult Fail(PlaybackFault fault, string detail)
    {
        State = PlaybackLifecycle.Failed;
        LastFault = fault;
        journal.Append(new(clock.Elapsed, State, detail, fault));
        return new(Outcome.Rejected, State, fault, detail);
    }

    CoordinatorResult Reject(PlaybackFault fault, string detail)
    {
        LastFault = fault;
        journal.Append(new(clock.Elapsed, State, detail, fault));
        return new(Outcome.Rejected, State, fault, detail);
    }

    void EmitAnchor(string state, long frame, double rate)
    {
        if (_activeInstanceId is null || _revisionId is null) return;
        anchors.Emit(new PlaybackAnchorRecord(
            _activeInstanceId,
            _revisionId,
            _sampleRate,
            frame,
            clock.Elapsed,
            _backendUtc,
            state,
            rate,
            ++_anchorSequence,
            _activeGeneration.Value));
    }
}
