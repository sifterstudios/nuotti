# Epic I - Testing the Testing: Master Plan

This document outlines the master plan for implementing comprehensive testing infrastructure and test coverage across the Nuotti project. Each task (I1-I23) should result in a single commit.

## Status Legend
- ✅ **Completed**: Task is fully implemented and committed
- 🚧 **In Progress**: Task is currently being worked on
- ⏳ **Pending**: Task is planned but not yet started
- 🔗 **Depends on**: Task has dependencies on other tasks

---

## Task Overview

### Foundation & Infrastructure (I1, I19, I20)

#### ✅ I1 — Test projects scaffolding & coverage config
**Status**: Completed  
**Issue**: #169  
**Description**: Set up test project structure, code coverage configuration, and CI integration.

**Deliverables**:
- Create `tests/Nuotti.UnitTests`, `tests/Nuotti.IntegrationTests`, `tests/Nuotti.E2E` projects
- Configure `.runsettings` for code coverage (Coverlet)
- Add test categories/traits
- Update `.github/workflows/test.yml` to run test suites and report coverage
- Add projects to solution file

**Files Created/Modified**:
- `tests/Nuotti.UnitTests/Nuotti.UnitTests.csproj`
- `tests/Nuotti.IntegrationTests/Nuotti.IntegrationTests.csproj`
- `tests/Nuotti.E2E/Nuotti.E2E.csproj`
- `.runsettings` (updated)
- `.github/workflows/test.yml` (updated)
- `Nuotti.sln` (updated)

---

#### ✅ I19 — Fixtures/builders
**Status**: Completed  
**Issue**: #189  
**Description**: Create test fixtures, builders, and sample data for consistent test setup.

**Deliverables**:
- Create `CommandBuilder`, `EventBuilder`, `GameStateBuilder` in `tests/Nuotti.UnitTests/TestHelpers/`
- Create `FixtureHelpers.cs` for common test data
- Add `contracts-fixtures/` directory with sample JSON files:
  - `sample-song.json`
  - `sample-game-state.json`
  - `sample-players.json`
- Update test project to copy fixtures to output directory

**Files Created/Modified**:
- `tests/Nuotti.UnitTests/TestHelpers/CommandBuilder.cs`
- `tests/Nuotti.UnitTests/TestHelpers/EventBuilder.cs`
- `tests/Nuotti.UnitTests/TestHelpers/GameStateBuilder.cs`
- `tests/Nuotti.UnitTests/TestHelpers/FixtureHelpers.cs`
- `tests/Nuotti.UnitTests/Fixtures/contracts-fixtures/sample-song.json`
- `tests/Nuotti.UnitTests/Fixtures/contracts-fixtures/sample-game-state.json`
- `tests/Nuotti.UnitTests/Fixtures/contracts-fixtures/sample-players.json`
- `tests/Nuotti.UnitTests/Nuotti.UnitTests.csproj` (updated)

---

#### ✅ I20 — Linting/formatting
**Status**: Completed  
**Issue**: #190  
**Description**: Set up code style enforcement with `.editorconfig`, ESLint, Prettier, and pre-commit hooks.

**Deliverables**:
- Create `.editorconfig` at repository root
- Create `web/.eslintrc.json` and `web/.prettierrc.json`
- Create `tools/pre-commit.ps1` and `tools/pre-commit.sh`
- Update `.github/workflows/test.yml` to include linting checks

**Files Created/Modified**:
- `.editorconfig`
- `web/.eslintrc.json`
- `web/.prettierrc.json`
- `tools/pre-commit.ps1`
- `tools/pre-commit.sh`
- `.github/workflows/test.yml` (updated)

---

### Contract & Reducer Tests (I2, I3, I4, I5)

#### ✅ I2 — Contract serialization round-trip tests
**Status**: Completed  
**Issue**: #170  
**Description**: Verify that all DTOs (commands, events, snapshots) serialize/deserialize correctly with both REST (camelCase) and SignalR (PascalCase) options.

**Deliverables**:
- Create `Nuotti.Contracts.Tests/V1/Serialization/RoundTripTests.cs`
- Test round-trip serialization for:
  - Commands (all phase change commands, `QuestionPushed`, `PlayTrack`, `StopTrack`, etc.)
  - Events (`GamePhaseChanged`, `AnswerSubmitted`, `CorrectAnswerRevealed`, etc.)
  - Snapshots (`GameStateSnapshot`, `SessionCreated`, etc.)
- Use both `ContractsJson.RestOptions` and `ContractsJson.HubOptions`
- Use Verify for snapshot testing

**Files Created/Modified**:
- `Nuotti.Contracts.Tests/V1/Serialization/RoundTripTests.cs`

---

#### ✅ I3 — Reducer logic happy path tests
**Status**: Completed  
**Issue**: #171  
**Description**: Test the full happy path of phase transitions and state updates in `GameReducer`.

**Deliverables**:
- Extend `Nuotti.Contracts.Tests/V1/Reducer/GameReducerTests.cs`
- Test complete phase flow: Lobby → Start → Play → Guessing → Reveal → Intermission
- Verify tallies reset on phase change to `Start`
- Verify `HintIndex` is not modified by `GamePhaseChanged` events

**Files Created/Modified**:
- `Nuotti.Contracts.Tests/V1/Reducer/GameReducerTests.cs` (extended)

---

#### ✅ I4 — Reducer logic guard tests
**Status**: Completed  
**Issue**: #172  
**Description**: Test that illegal phase transitions are rejected by the reducer.

**Deliverables**:
- Create `Nuotti.Contracts.Tests/V1/Reducer/GameReducerGuardTests.cs`
- Test phase mismatch detection (event `CurrentPhase` != state `Phase`)
- Verify reducer returns error without mutating state
- Add ProblemDetails snapshot test for invalid transitions

**Files Created/Modified**:
- `Nuotti.Contracts.Tests/V1/Reducer/GameReducerGuardTests.cs`

---

#### ✅ I5 — Scoring tests
**Status**: Completed  
**Issue**: #173  
**Description**: Test scoring logic: correct answer awards, cumulative scores, tie handling.

**Deliverables**:
- Create or extend `Nuotti.Contracts.Tests/V1/Reducer/GameReducerScoringTests.cs`
- Test `CorrectAnswerRevealed` awards +1 to audiences with matching answer
- Test cumulative scoring across multiple rounds
- Verify ties are handled deterministically (e.g., by player ID)

**Files Created/Modified**:
- `Nuotti.Contracts.Tests/V1/Reducer/GameReducerScoringTests.cs` (created or extended)

---

### Backend Integration Tests (I6, I7, I8, I9, I10, I11)

#### ✅ I6 — Idempotency tests
**Status**: Completed  
**Issue**: #174  
**Description**: Test that duplicate commands are idempotent (no double application).

**Deliverables**:
- Extend `Nuotti.Backend.Tests/IdempotencyStoreTests.cs`
- Test duplicate `CommandId` replay within TTL returns `202 Accepted` without state change
- Test two identical commands result in single state change

**Files Created/Modified**:
- `Nuotti.Backend.Tests/IdempotencyStoreTests.cs` (extended)

---

#### ✅ I7 — Role guard tests
**Status**: Completed  
**Issue**: #175  
**Description**: Test that commands issued by wrong role are rejected with 403 Forbidden.

**Deliverables**:
- Create `Nuotti.Backend.Tests/RoleGuardTests.cs`
- Test REST API endpoints reject wrong role (e.g., Audience trying to `PushQuestion`)
- Test SignalR Hub methods reject wrong role (e.g., Performer trying to `SubmitAnswer`)
- Verify `NuottiProblem` payload with `ReasonCode.UnauthorizedRole`

**Files Created/Modified**:
- `Nuotti.Backend.Tests/RoleGuardTests.cs`

---

#### ✅ I8 — API contract tests
**Status**: Completed  
**Issue**: #176  
**Description**: Test REST API endpoints return correct status codes and ProblemDetails.

**Deliverables**:
- Extend `Nuotti.Backend.Tests/ApiEndpointsTests.cs`
- Test `CreateSession`, `GetSessionCounts`, `PushQuestion`, `PlayTrack`, `StopTrack`, `UploadSetlistManifest`, `GetStatus`
- Use `Verify` for snapshot testing of API payloads
- Test health endpoints (`/health/live`, `/health/ready`)

**Files Created/Modified**:
- `Nuotti.Backend.Tests/ApiEndpointsTests.cs` (extended)

---

#### 🚧 I9 — SignalR Hub in-proc tests
**Status**: In Progress  
**Issue**: #177  
**Description**: Extend in-process SignalR Hub tests for group membership, broadcast scoping, and session isolation.

**Deliverables**:
- Extend `Nuotti.Backend.Tests/QuizHubInProcTests.cs`
- Test group membership (session groups, role groups)
- Test broadcast scoping (messages only go to correct session group)
- Test session isolation (multiple sessions don't interfere)

**Files Created/Modified**:
- `Nuotti.Backend.Tests/QuizHubInProcTests.cs` (extended)

**Dependencies**: I1 (test infrastructure)

---

#### ⏳ I10 — Rate limiting & debounce enforcement tests
**Status**: Pending  
**Issue**: #178  
**Description**: Test rate limiting and debouncing (429 on rapid `SubmitAnswer`, Play/Stop limits).

**Deliverables**:
- Create `Nuotti.Backend.Tests/RateLimitingTests.cs` (if not exists) or extend existing
- Test `SubmitAnswer` debounce (500ms window) returns 429 on rapid submissions
- Test Play/Stop rate limits (if applicable)
- Verify `ConnectionRateLimiter` behavior

**Files Created/Modified**:
- `Nuotti.Backend.Tests/RateLimitingTests.cs` (created or extended)

**Dependencies**: I1, I9

---

#### ⏳ I11 — Reconnect & state resync tests
**Status**: Pending  
**Issue**: #179  
**Description**: Test reconnection scenarios: simulated disconnects, `/status` fetch for resync.

**Deliverables**:
- Create `Nuotti.Backend.Tests/ReconnectTests.cs`
- Test client reconnects and receives latest state via SignalR
- Test `/status/{session}` endpoint returns current snapshot
- Test state resync after disconnect/reconnect

**Files Created/Modified**:
- `Nuotti.Backend.Tests/ReconnectTests.cs`

**Dependencies**: I1, I8, I9

---

### E2E Tests (I12, I13, I16, I17)

#### ⏳ I12 — E2E single-song happy path test
**Status**: Pending  
**Issue**: #180  
**Description**: Create end-to-end test using SimKit + Playwright for single-song flow.

**Deliverables**:
- Create `tests/Nuotti.E2E/SingleSongHappyPathTests.cs`
- Use SimKit to drive backend state
- Use Playwright to verify Audience UI updates
- Test complete flow: Join → Start → Play → Guessing → Reveal → Intermission
- Run in headless browser mode

**Files Created/Modified**:
- `tests/Nuotti.E2E/SingleSongHappyPathTests.cs`

**Dependencies**: I1, I19 (fixtures)

---

#### ⏳ I13 — E2E multi-song flow test
**Status**: Pending  
**Issue**: #181  
**Description**: Create E2E test for multi-song flow with hints and scoring verification.

**Deliverables**:
- Create `tests/Nuotti.E2E/MultiSongFlowTests.cs`
- Test multiple songs in sequence
- Verify hints are displayed correctly
- Verify scoring accumulates across songs
- Test intermission between songs

**Files Created/Modified**:
- `tests/Nuotti.E2E/MultiSongFlowTests.cs`

**Dependencies**: I1, I12, I19

---

#### ⏳ I16 — Visual regression tests for Projector screens
**Status**: Pending  
**Issue**: #184  
**Description**: Create Playwright snapshot tests for Projector screens at 1080p/4K.

**Deliverables**:
- Extend `Nuotti.Projector.Tests/ProjectorVisualTests.cs` (if exists) or create new
- Test all game phases render correctly (Lobby, Start, Play, Guessing, Reveal, Intermission)
- Capture screenshots at 1920x1080 and 3840x2160
- Use Playwright's screenshot comparison
- Store baseline images in `Screenshots/Baselines/`

**Files Created/Modified**:
- `Nuotti.Projector.Tests/ProjectorVisualTests.cs` (extended or created)

**Dependencies**: I1, I19

**Note**: `Nuotti.Projector.Tests` already exists with visual tests. May need to extend or verify coverage.

---

#### ⏳ I17 — Mobile E2E tests for Audience UX
**Status**: Pending  
**Issue**: #185  
**Description**: Create mobile E2E tests for Audience UX with iPhone/Android emulation and reconnect scenarios.

**Deliverables**:
- Create `tests/Nuotti.E2E/MobileAudienceTests.cs`
- Use Playwright device emulation (iPhone, Android)
- Test Audience UI on mobile viewports
- Test reconnect scenarios on mobile
- Test touch interactions

**Files Created/Modified**:
- `tests/Nuotti.E2E/MobileAudienceTests.cs`

**Dependencies**: I1, I11, I12

---

### AudioEngine Tests (I14, I15)

#### ⏳ I14 — AudioEngine player tests
**Status**: Pending  
**Issue**: #182  
**Description**: Test AudioEngine player with mocked `IProcessRunner` (arg builders, stop behavior).

**Deliverables**:
- Extend `Nuotti.AudioEngine.Tests/SystemPlayerTests.cs` (if exists) or create new
- Mock `IProcessRunner` to verify command-line arguments
- Test Play/Stop behavior
- Test error handling (invalid URLs, process failures)

**Files Created/Modified**:
- `Nuotti.AudioEngine.Tests/SystemPlayerTests.cs` (extended or created)

**Dependencies**: I1, I19

**Note**: `Nuotti.AudioEngine.Tests` already exists. May need to extend existing tests.

---

#### ⏳ I15 — Engine path/URL validation tests
**Status**: Pending  
**Issue**: #183  
**Description**: Test engine path/URL validation (file:// roots, http(s) HEAD, invalid scheme rejection).

**Deliverables**:
- Create or extend `Nuotti.AudioEngine.Tests/RoutingValidationTests.cs`
- Test `file://` paths with valid roots
- Test `http://` and `https://` URLs with HEAD preflight
- Test invalid schemes are rejected
- Test path validation logic

**Files Created/Modified**:
- `Nuotti.AudioEngine.Tests/RoutingValidationTests.cs` (created or extended)

**Dependencies**: I1, I14

**Note**: `RoutingValidationTests.cs` may already exist. Verify and extend as needed.

---

### Load & Chaos Tests (I18)

#### ⏳ I18 — SimKit load & chaos tests
**Status**: Pending  
**Issue**: #186  
**Description**: Create SimKit load tests (200 audiences baseline) and chaos tests (disconnects, delays).

**Deliverables**:
- Create `Nuotti.SimKit.Tests/LoadTests.cs`
- Test 200 concurrent audiences baseline
- Create `Nuotti.SimKit.Tests/ChaosTests.cs`
- Test random disconnects, network delays, message reordering
- Add nightly CI workflow for load tests

**Files Created/Modified**:
- `Nuotti.SimKit.Tests/LoadTests.cs`
- `Nuotti.SimKit.Tests/ChaosTests.cs`
- `.github/workflows/nightly-load-tests.yml` (new)

**Dependencies**: I1, I11, I19

**Note**: `Nuotti.SimKit.Tests` already exists. May have some chaos tests already.

---

### Test Quality & Documentation (I21, I22, I23)

#### ⏳ I21 — Flaky test mitigation
**Status**: Pending  
**Issue**: #187  
**Description**: Add retry logic, deterministic seeds, and flake tracking.

**Deliverables**:
- Add retry attributes to flaky tests (xUnit `Retry` or custom)
- Use deterministic random seeds in tests
- Add flake tracking (e.g., mark tests with `[Trait("Category", "Flaky")]`)
- Document known flaky tests

**Files Created/Modified**:
- Test files with flaky tests (add retry attributes)
- `tests/Nuotti.UnitTests/TestHelpers/DeterministicRandom.cs` (if needed)
- `.github/workflows/test.yml` (may need retry configuration)

**Dependencies**: I1

---

#### ⏳ I22 — Smoke script
**Status**: Pending  
**Issue**: #188  
**Description**: Create PowerShell/Bash script for local dev sanity check.

**Deliverables**:
- Create `tools/smoke-test.ps1` and `tools/smoke-test.sh`
- Run quick subset of tests (unit + integration)
- Verify backend starts and responds to health checks
- Exit with non-zero code on failure

**Files Created/Modified**:
- `tools/smoke-test.ps1`
- `tools/smoke-test.sh`

**Dependencies**: I1

---

#### ⏳ I23 — Test documentation
**Status**: Pending  
**Issue**: #191  
**Description**: Create comprehensive test documentation with run instructions and troubleshooting.

**Deliverables**:
- Create `docs/testing.md`
- Document how to run each test suite
- Document test categories and traits
- Document troubleshooting common issues
- Document CI/CD test execution
- Link to this master plan

**Files Created/Modified**:
- `docs/testing.md`

**Dependencies**: All previous tasks (for comprehensive documentation)

---

## Implementation Order & Dependencies

### Phase 1: Foundation (✅ Complete)
- ✅ I1: Test projects scaffolding
- ✅ I19: Fixtures/builders
- ✅ I20: Linting/formatting

### Phase 2: Core Logic Tests (✅ Complete)
- ✅ I2: Contract serialization
- ✅ I3: Reducer happy path
- ✅ I4: Reducer guards
- ✅ I5: Scoring

### Phase 3: Backend Integration (🚧 In Progress)
- ✅ I6: Idempotency
- ✅ I7: Role guards
- ✅ I8: API contracts
- 🚧 I9: SignalR Hub tests (in progress)
- ⏳ I10: Rate limiting (depends on I9)
- ⏳ I11: Reconnect/resync (depends on I8, I9)

### Phase 4: E2E Tests (⏳ Pending)
- ⏳ I12: Single-song E2E (depends on I1, I19)
- ⏳ I13: Multi-song E2E (depends on I12)
- ⏳ I16: Visual regression (depends on I1, I19)
- ⏳ I17: Mobile E2E (depends on I11, I12)

### Phase 5: AudioEngine Tests (⏳ Pending)
- ⏳ I14: Player tests (depends on I1, I19)
- ⏳ I15: Path/URL validation (depends on I14)

### Phase 6: Load & Quality (⏳ Pending)
- ⏳ I18: SimKit load/chaos (depends on I1, I11, I19)
- ⏳ I21: Flaky test mitigation (depends on I1)
- ⏳ I22: Smoke script (depends on I1)
- ⏳ I23: Test documentation (depends on all)

---

## Commit Strategy

Each task (I1-I23) should result in **one commit** with a clear message:

```
feat(tests): I1 - Test projects scaffolding & coverage config

- Create Nuotti.UnitTests, Nuotti.IntegrationTests, Nuotti.E2E projects
- Configure .runsettings for code coverage
- Update CI workflow to run test suites
- Add test categories/traits

Closes #169
```

**Commit Message Format**:
```
feat(tests): I{N} - {Task Title}

{Description of changes}

Closes #{IssueNumber}
```

---

## Testing Standards

### Test Naming
- Use descriptive test names: `{Method}_{Scenario}_Should{ExpectedResult}`
- Examples: `GameReducer_ValidPhaseTransition_ShouldSucceed`, `SubmitAnswer_RapidSubmissions_ShouldReturn429`

### Test Organization
- Group related tests in the same class
- Use `[Fact]` for xUnit tests
- Use `[Trait("Category", "...")]` for test categories

### Coverage Goals
- Unit tests: >80% coverage for reducer logic
- Integration tests: Cover all API endpoints and SignalR methods
- E2E tests: Cover critical user flows

### Snapshot Testing
- Use `Verify` library for snapshot testing of DTOs and API responses
- Store snapshots in `**/Snapshots/` directories
- Commit snapshots to version control

---

## CI/CD Integration

### Test Execution
- Unit tests: Fast, run on every PR
- Integration tests: Medium speed, run on every PR
- E2E tests: Slower, run on PR and main branch
- Load tests: Run nightly (separate workflow)

### Coverage Reporting
- Collect coverage using Coverlet
- Generate reports in Cobertura format
- Upload as artifacts in CI
- (Optional) Integrate with codecov.io or similar)

---

## Notes

- Some test projects already exist (e.g., `Nuotti.Projector.Tests`, `Nuotti.AudioEngine.Tests`). Tasks may involve extending existing tests rather than creating new ones.
- The `Nuotti.SimKit.Tests` project already exists and may have some chaos tests. I18 should extend or verify coverage.
- Visual regression tests for Projector (I16) may already be partially implemented in `Nuotti.Projector.Tests`.
- Always verify existing test coverage before creating new tests to avoid duplication.

---

## Progress Tracking

- **Completed**: 8/23 tasks (35%)
- **In Progress**: 1/23 tasks (4%)
- **Pending**: 14/23 tasks (61%)

Last Updated: 2024-12-19

