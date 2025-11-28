# Nuotti Operations Runbook

This document provides a comprehensive guide for operating and troubleshooting the Nuotti system.

## Table of Contents

1. [Service Overview](#service-overview)
2. [Reading Logs](#reading-logs)
3. [Exporting Diagnostics](#exporting-diagnostics)
4. [Interpreting Metrics](#interpreting-metrics)
5. [Health Checks](#health-checks)
6. [Common Errors and Fixes](#common-errors-and-fixes)
7. [Endpoint Reference](#endpoint-reference)
8. [Configuration](#configuration)

---

## Service Overview

Nuotti consists of several services:

- **Backend** (`Nuotti.Backend`): ASP.NET Core API with SignalR hubs
- **Performer** (`Nuotti.Performer`): Blazor Server app for game control
- **Audience** (`Nuotti.Audience`): Blazor WASM app for players
- **Projector** (`Nuotti.Projector`): Avalonia desktop app for display
- **AudioEngine** (`Nuotti.AudioEngine`): Console app for audio playback

---

## Reading Logs

### Log Format

All services use structured JSON logging with Serilog. Logs include:

- `service`: Service name (e.g., "Nuotti.Backend")
- `version`: Service version
- `session`: Session code (when available)
- `role`: User role (when available)
- `connectionId`: Connection identifier (when available)
- `@timestamp`: ISO 8601 timestamp
- Additional context-specific fields

### Log Locations

#### Backend
- **Console**: Structured JSON output to stdout
- **Audit logs**: `AppData\Roaming\Nuotti\Logs\Nuotti.Backend\audit-YYYYMMDD.log`
  - Separate file sink for audit entries (commands/events)
  - 30-day retention, daily rotation
  - 100MB per file limit

#### Performer
- **Console**: Structured JSON output to stdout
- **File logs**: `AppData\Roaming\Nuotti\Logs\Nuotti.Performer\Nuotti.Performer-YYYYMMDD.log`
  - 7-day retention, daily rotation
  - 100MB per file limit

#### AudioEngine
- **Console**: Structured JSON output to stdout
- **File logs**: `AppData\Roaming\Nuotti\Logs\Nuotti.AudioEngine\Nuotti.AudioEngine-YYYYMMDD.log`
  - 7-day retention, daily rotation
  - 100MB per file limit

#### Projector & Audience
- **Console**: Structured JSON output to stdout
- File logging not enabled by default

### Log Levels

Configurable via:
- `appsettings.json`: `Logging:LogLevel:Default`
- Environment variable: `NUOTTI_LOG_LEVEL`
- Dynamic endpoint (DEV only): `POST /dev/log-level` with body `{ "level": "Debug" }`

Valid levels: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`

### Filtering Logs

Use structured log fields for filtering:

```bash
# Filter by session
cat logs/*.log | jq 'select(.session == "MYSESSION")'

# Filter by correlation ID
cat logs/*.log | jq 'select(.["correlation.id"] == "abc-123")'

# Filter errors only
cat logs/*.log | jq 'select(.level == "Error")'

# Filter audit entries
cat audit-*.log | jq 'select(.["audit.type"] == "command_applied")'
```

### Correlation IDs

All logs include correlation IDs when available:
- HTTP requests: Extracted from `X-Correlation-Id` header or generated
- Commands: `CommandId` used as correlation ID
- Events: `CorrelationId` and `CausedByCommandId` included

Use correlation IDs to trace a command through:
1. Command received
2. Event published
3. Event processed
4. State updated

---

## Exporting Diagnostics

### Diagnostics Bundle

Export a comprehensive diagnostics bundle:

```bash
# Export bundle for a specific session
curl -X POST "http://localhost:5240/diagnostics/export?session=MYSESSION&logFileCount=10" \
  --output nuotti-diagnostics.zip

# Export bundle without session context
curl -X POST "http://localhost:5240/diagnostics/export?logFileCount=5" \
  --output nuotti-diagnostics.zip
```

**Query Parameters:**
- `session` (optional): Session code to include status.json
- `logFileCount` (optional, default: 5): Number of recent log files to include

**Bundle Contents:**
- `about.json`: Service version, build info, feature flags
- `metrics.json`: Current metrics snapshot
- `status-{session}.json`: Game state snapshot (if session provided)
- `config-redacted.json`: Configuration with sensitive values redacted
- `logs-info.txt`: Instructions for locating log files
- `manifest.json`: Bundle metadata

**Note**: Log files are not automatically included. The bundle provides instructions on where to find logs. Manually add log files to the bundle if needed.

---

## Interpreting Metrics

### Backend Metrics

**Endpoint**: `GET /metrics`

**Key Metrics:**

```json
{
  "activeConnections": {
    "total": 10,
    "byRole": {
      "performer": 1,
      "projector": 1,
      "engine": 1,
      "audiences": 7
    }
  },
  "answersPerMinute": 42.5,
  "commandLatency": {
    "p50": 12.3,
    "p95": 45.6,
    "p99": 78.9
  }
}
```

**Interpretation:**
- `activeConnections.total`: Total active SignalR connections
- `activeConnections.byRole`: Connections grouped by role
- `answersPerMinute`: Rate of answer submissions
- `commandLatency.p50/p95/p99`: Latency percentiles (milliseconds)

**Normal Values:**
- Command latency p95 < 100ms: Good
- Command latency p95 > 500ms: Investigate
- Answers per minute: Varies by session activity

### AudioEngine Metrics

**Endpoint**: `GET /metrics` (on AudioEngine HTTP server)

**Key Metrics:**

```json
{
  "isPlaying": false,
  "currentFile": null,
  "outputLatencyMs": 0,
  "playbackStartCount": 5,
  "playbackStopCount": 4,
  "playbackFailureCount": 0,
  "failureRate": 0.0,
  "averageTrackDurationSeconds": 180.5
}
```

**Interpretation:**
- `isPlaying`: Currently playing audio
- `currentFile`: Current audio file path (may be redacted)
- `outputLatencyMs`: Audio output latency
- `playbackStartCount`: Total playback start events
- `playbackStopCount`: Total playback stop events
- `playbackFailureCount`: Total playback failures
- `failureRate`: Ratio of failures to starts (0.0 = no failures)
- `averageTrackDurationSeconds`: Average duration of completed tracks

**Normal Values:**
- Failure rate < 0.05 (5%): Acceptable
- Failure rate > 0.1 (10%): Investigate audio device/backend issues

---

## Health Checks

### Backend

**Liveness**: `GET /health/live`
- Always returns 200 if service is running

**Readiness**: `GET /health/ready`
- Returns 200 if SignalR hub and session store are functional
- Returns 503 if dependencies are unavailable

### AudioEngine

**Liveness**: `GET /health/live`
- Always returns 200 if service is running

**Readiness**: `GET /health/ready`
- Returns 200 if metrics are available (player is initialized)
- Returns 503 if player is not available

---

## Common Errors and Fixes

### Error: "Invalid state transition"

**Status**: 409 Conflict  
**Reason Code**: `InvalidStateTransition`

**Cause**: Command attempted in wrong game phase.

**Example**: Trying to start game when already in progress.

**Fix**: Check current phase via `/status/{session}` and ensure command is allowed in that phase.

**Log Location**: Backend logs with correlation ID

---

### Error: "Unauthorized Role"

**Status**: 403 Forbidden  
**Reason Code**: `UnauthorizedRole`

**Cause**: Command issued by wrong role (e.g., Audience trying to change phase).

**Fix**: Ensure commands are issued by the correct role (Performer for phase changes).

**Log Location**: Backend logs with correlation ID

---

### Error: "Duplicate command"

**Status**: 409 Conflict  
**Reason Code**: `DuplicateCommand`

**Cause**: Command with same CommandId already processed (idempotency check).

**Fix**: This is expected behavior for retries. Check if the command was already applied successfully.

**Log Location**: Backend logs with correlation ID

---

### Warning: "Critical role missing"

**Log Level**: Warning  
**Cause**: Engine or Projector missing from session for > threshold seconds (default: 30s).

**Fix**:
1. Check if Engine/Projector services are running
2. Verify SignalR connection to Backend
3. Check network connectivity
4. Review Backend logs for connection errors

**Alerting**: Configured webhook will receive alert if `NUOTTI_ALERTINGWEBHOOKURL` is set.

**Log Location**: Backend logs with structured warning

---

### Error: Configuration validation failed

**Status**: Startup failure  
**Cause**: Invalid configuration values (negative timeouts, invalid ranges, etc.).

**Fix**:
1. Check error message for specific validation failure
2. Review hints in error message for configuration keys
3. Fix configuration in `appsettings.json` or environment variables
4. Restart service

**Log Location**: Startup logs with detailed validation errors

---

### Warning: Time drift detected

**Log Level**: Warning  
**Cause**: Server time differs from NTP time by > 250ms.

**Fix**:
1. Sync system clock: `w32tm /resync` (Windows) or `sudo ntpdate -s time.nist.gov` (Linux)
2. Check NTP service status
3. Verify network connectivity to NTP servers

**Log Location**: Backend startup logs

---

### Error: Routing ERROR (AudioEngine)

**Log Level**: Error  
**Cause**: Audio routing configuration exceeds device channel count.

**Fix**:
1. Check device channels: Review AudioEngine startup logs
2. Adjust routing in `engine.json`: Ensure channel indices ≤ device channels
3. Restart AudioEngine

**Log Location**: AudioEngine startup logs

---

## Endpoint Reference

### Backend Endpoints

#### Information
- `GET /about`: Service version, build info, enabled feature flags
- `GET /time`: Server time for drift detection
- `GET /metrics`: Current metrics snapshot
- `GET /status/{session}`: Game state snapshot for session

#### Diagnostics
- `POST /diagnostics/export?session={session}&logFileCount={count}`: Export diagnostics bundle

#### Health
- `GET /health/live`: Liveness probe
- `GET /health/ready`: Readiness probe

#### Development (DEV only)
- `POST /dev/log-level`: Change log level dynamically
  ```json
  { "level": "Debug" }
  ```

#### API
- `POST /api/sessions/{name}`: Create new session
- `GET /api/sessions/{session}/counts`: Get connection counts by role
- `POST /v1/message/phase/{command}/{session}`: Phase change commands

### AudioEngine Endpoints

- `GET /metrics`: Current playback metrics
- `GET /about`: Version and build info
- `GET /health/live`: Liveness probe
- `GET /health/ready`: Readiness probe

---

## Configuration

### Backend Configuration

**File**: `appsettings.json` or environment variables with `NUOTTI_` prefix

**Key Settings:**

```json
{
  "Nuotti": {
    "SessionIdleTimeoutSeconds": 900,
    "SessionEvictionIntervalSeconds": 30,
    "IdempotencyTtlSeconds": 600,
    "IdempotencyMaxPerSession": 128,
    "MissingRoleAlertThresholdSeconds": 30,
    "AlertingWebhookUrl": null,
    "Features": {
      "ExperimentalFeature": false
    }
  }
}
```

**Environment Variables:**
- `NUOTTI_SESSIONIDLETIMEOUTSECONDS`: Session idle timeout (seconds)
- `NUOTTI_MISSINGROLEALERTTHRESHOLDSECONDS`: Alert threshold (seconds)
- `NUOTTI_ALERTINGWEBHOOKURL`: Webhook URL for alerts (optional)
- `NUOTTI_FEATURES__FEATURENAME`: Feature flag values
- `NUOTTI_LOG_LEVEL`: Log level override

### AudioEngine Configuration

**File**: `engine.json` or environment variables with `NUOTTI_ENGINE__` prefix

**Key Settings:**

```json
{
  "Routing": {
    "Tracks": [1, 2],
    "Click": [3]
  },
  "Click": {
    "Level": 0.5,
    "Bpm": 120
  },
  "Safety": {
    "PathAllowlist": [],
    "HttpMaxSizeBytes": 10485760
  }
}
```

**Validation**: Configuration is validated at startup. Invalid config causes service to exit with code 1.

---

## Troubleshooting Workflow

1. **Check Service Health**
   - Verify `/health/live` returns 200
   - Verify `/health/ready` returns 200

2. **Review Metrics**
   - Check `/metrics` for anomalies
   - Compare with historical values

3. **Check Logs**
   - Review recent error/warning logs
   - Filter by correlation ID if available
   - Check audit logs for command/event flow

4. **Export Diagnostics**
   - Generate diagnostics bundle
   - Include relevant log files
   - Share with support team

5. **Verify Configuration**
   - Check `/about` for version and feature flags
   - Validate configuration settings
   - Review configuration validation errors at startup

6. **Check Dependencies**
   - Verify all services are running
   - Check SignalR connections
   - Verify network connectivity

---

## Quick Reference

### Log File Locations

| Service | Log Directory |
|---------|---------------|
| Backend (Audit) | `AppData\Roaming\Nuotti\Logs\Nuotti.Backend\` |
| Performer | `AppData\Roaming\Nuotti\Logs\Nuotti.Performer\` |
| AudioEngine | `AppData\Roaming\Nuotti\Logs\Nuotti.AudioEngine\` |

### Key Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /about` | Version and build info |
| `GET /metrics` | Current metrics |
| `GET /health/ready` | Readiness check |
| `POST /diagnostics/export` | Export diagnostics bundle |

### Alert Thresholds

| Condition | Default Threshold |
|-----------|-------------------|
| Missing Engine/Projector | 30 seconds |
| Time drift warning | 250ms |
| Command latency (p95) | Monitor if > 500ms |
| Playback failure rate | Monitor if > 10% |

---

## Related Documentation

- [Testing Guide](testing.md): Test execution and debugging
- [Contracts V1](contracts-v1.md): API contracts and message formats
- [Service Defaults](../ServiceDefaults/README.md): Shared service configuration

---

**Last Updated**: 2024-01-XX  
**Maintained By**: Sifter Studios

