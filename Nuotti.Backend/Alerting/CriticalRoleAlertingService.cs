using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;

namespace Nuotti.Backend.Alerting;

/// <summary>
/// Monitors sessions for missing critical roles (Engine, Projector) and emits alerts.
/// </summary>
public class CriticalRoleAlertingService : IDisposable
{
    private readonly ISessionStore _sessionStore;
    private readonly IGameStateStore _gameStateStore;
    private readonly ILogger<CriticalRoleAlertingService> _logger;
    private readonly NuottiOptions _options;
    private readonly HttpClient? _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly System.Threading.ITimer _checkTimer;

    // Track last alert time per session+role to avoid spam
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAlertTime = new();

    public CriticalRoleAlertingService(
        ISessionStore sessionStore,
        IGameStateStore gameStateStore,
        IOptions<NuottiOptions> options,
        ILogger<CriticalRoleAlertingService> logger,
        IHttpClientFactory? httpClientFactory = null,
        TimeProvider? timeProvider = null)
    {
        _sessionStore = sessionStore;
        _gameStateStore = gameStateStore;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _httpClient = httpClientFactory?.CreateClient("Alerting");

        // Check every 5 seconds for missing roles
        var checkInterval = TimeSpan.FromSeconds(5);
        _checkTimer = _timeProvider.CreateTimer(CheckForMissingRoles, null, checkInterval, checkInterval);
    }

    private async void CheckForMissingRoles(object? state)
    {
        try
        {
            var now = _timeProvider.GetUtcNow();
            var missingThresholdSeconds = _options.MissingRoleAlertThresholdSeconds;
            var webhookUrl = _options.AlertingWebhookUrl;
            var webhookEnabled = !string.IsNullOrWhiteSpace(webhookUrl);

            // Discover sessions from GameStateStore (sessions with game state)
            var allSessions = GetSessionsFromGameState();

            foreach (var sessionCode in allSessions)
            {
                var tracking = _trackedSessions.GetOrAdd(sessionCode, _ => new SessionRoleTracking { LastChecked = now });
                tracking.LastChecked = now;

                var counts = _sessionStore.GetCounts(sessionCode);

                // Check for missing Engine
                if (counts.Engine == 0)
                {
                    tracking.EngineMissingSince ??= now;
                    var missingDuration = (now - tracking.EngineMissingSince.Value).TotalSeconds;
                    if (missingDuration >= missingThresholdSeconds)
                    {
                        await CheckAndAlertMissingRole(sessionCode, "Engine", now, missingThresholdSeconds, webhookEnabled, webhookUrl);
                    }
                }
                else
                {
                    // Engine is present - reset tracking
                    tracking.EngineMissingSince = null;
                }

                // Check for missing Projector
                if (counts.Projector == 0)
                {
                    tracking.ProjectorMissingSince ??= now;
                    var missingDuration = (now - tracking.ProjectorMissingSince.Value).TotalSeconds;
                    if (missingDuration >= missingThresholdSeconds)
                    {
                        await CheckAndAlertMissingRole(sessionCode, "Projector", now, missingThresholdSeconds, webhookEnabled, webhookUrl);
                    }
                }
                else
                {
                    // Projector is present - reset tracking
                    tracking.ProjectorMissingSince = null;
                }
            }

            // Clean up tracking for sessions that no longer exist
            CleanupOldSessions(allSessions, now);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error checking for missing roles: {Message}", ex.Message);
        }
    }

    private void CleanupOldSessions(IEnumerable<string> activeSessions, DateTimeOffset now)
    {
        var activeSet = activeSessions.ToHashSet();
        var sessionsToRemove = _trackedSessions.Keys.Where(s => !activeSet.Contains(s)).ToList();

        foreach (var session in sessionsToRemove)
        {
            _trackedSessions.TryRemove(session, out _);
        }
    }

    private List<string> GetSessionsFromGameState()
    {
        // Use reflection to get sessions from GameStateStore's internal _states dictionary
        // This is a workaround - ideally GameStateStore would have GetAllSessions() method
        try
        {
            var field = _gameStateStore.GetType().GetField("_states",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(_gameStateStore) is System.Collections.Concurrent.ConcurrentDictionary<string, object> statesDict)
            {
                return statesDict.Keys.ToList();
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("Failed to get sessions from GameStateStore: {Message}", ex.Message);
        }

        // Fallback: also check tracked sessions that might have connections but no game state yet
        return _trackedSessions.Keys.ToList();
    }

    private async Task CheckAndAlertMissingRole(
        string sessionCode,
        string role,
        DateTimeOffset now,
        int thresholdSeconds,
        bool webhookEnabled,
        string? webhookUrl)
    {
        var alertKey = $"{sessionCode}:{role}";
        var lastAlert = _lastAlertTime.GetOrAdd(alertKey, DateTimeOffset.MinValue);
        var timeSinceLastAlert = (now - lastAlert).TotalSeconds;

        // Only alert once per interval (avoid spam)
        var alertIntervalSeconds = thresholdSeconds;
        if (timeSinceLastAlert < alertIntervalSeconds)
        {
            return;
        }

        // Get actual missing duration from tracking
        var tracking = _trackedSessions.TryGetValue(sessionCode, out var t) &&
            ((role == "Engine" && t.EngineMissingSince.HasValue) || (role == "Projector" && t.ProjectorMissingSince.HasValue))
            ? (role == "Engine" ? t.EngineMissingSince : t.ProjectorMissingSince)!.Value
            : now;
        var actualMissingDuration = (now - tracking).TotalSeconds;

        // Log structured warning
        _logger.LogWarning(
            "Critical role missing. Session={SessionCode}, Role={Role}, MissingDurationSeconds={MissingDurationSeconds:F1}, ThresholdSeconds={ThresholdSeconds}",
            sessionCode, role, actualMissingDuration, thresholdSeconds);

        // Update last alert time
        _lastAlertTime[alertKey] = now;

        // Send webhook if enabled
        if (webhookEnabled && _httpClient != null && !string.IsNullOrWhiteSpace(webhookUrl))
        {
            try
            {
                await SendWebhookAlertAsync(sessionCode, role, webhookUrl);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to send webhook alert for missing {Role} in session {SessionCode}: {Message}",
                    role, sessionCode, ex.Message);
            }
        }
    }

    private async Task SendWebhookAlertAsync(string sessionCode, string role, string webhookUrl)
    {
        if (_httpClient == null) return;

        // Get session summary
        var counts = _sessionStore.GetCounts(sessionCode);
        var sessionSummary = new
        {
            timestamp = DateTimeOffset.UtcNow,
            sessionCode = sessionCode,
            missingRole = role,
            roleCounts = new
            {
                performer = counts.Performer,
                projector = counts.Projector,
                engine = counts.Engine,
                audiences = counts.Audiences
            },
            gameState = _gameStateStore.TryGet(sessionCode, out var snapshot) ? new
            {
                phase = snapshot.Phase.ToString(),
                songIndex = snapshot.SongIndex,
                currentSong = snapshot.CurrentSong?.Title ?? "None"
            } : null
        };

        var json = JsonSerializer.Serialize(sessionSummary, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(webhookUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Webhook alert returned {StatusCode} for missing {Role} in session {SessionCode}",
                response.StatusCode, role, sessionCode);
        }
    }


    // Track sessions we've seen and when roles went missing
    private readonly ConcurrentDictionary<string, SessionRoleTracking> _trackedSessions = new();

    private sealed class SessionRoleTracking
    {
        public DateTimeOffset? EngineMissingSince { get; set; }
        public DateTimeOffset? ProjectorMissingSince { get; set; }
        public DateTimeOffset LastChecked { get; set; } = DateTimeOffset.UtcNow;
    }

    public void Dispose()
    {
        _checkTimer?.Dispose();
        _httpClient?.Dispose();
    }
}

