# Flaky Test Mitigation

This document describes strategies for handling flaky tests in the Nuotti test suite.

## Deterministic Seeds

Tests that use randomness should use deterministic seeds to ensure reproducible behavior. This helps identify when a test failure is due to actual bugs vs. random variation.

### Using Deterministic Seeds

**In SimKit tests:**
```csharp
var options = new AudienceOptions
{
    RandomSeed = 123  // Fixed seed for reproducibility
};
var actor = new AudienceActor(factory, baseUri, session, "TestUser", options);
```

**In Unit tests:**
```csharp
using Nuotti.UnitTests.TestHelpers;

var random = DeterministicRandom.Create(seed: 42);
// or
var random = DeterministicRandom.CreateFromTestName(nameof(MyTest));
```

**Using TestSeedHelper:**
```csharp
using Nuotti.UnitTests.TestHelpers;

var random = TestSeedHelper.CreateRandomForTest(
    nameof(MyTest), 
    seed => Console.WriteLine($"Test seed: {seed}")
);
```

## Marking Flaky Tests

Tests that are known to be flaky should be marked with traits:

```csharp
[Fact]
[Trait("Flaky", "true")]
[Trait("Category", "E2E")]
public async Task Potentially_Flaky_Test()
{
    // Test implementation
}
```

To skip flaky tests in CI:
```bash
dotnet test --filter "Flaky!=true"
```

## Retry Strategy

For E2E tests that may be flaky due to timing or network issues:

1. **Increase timeouts** rather than retrying
2. **Use deterministic delays** where possible
3. **Mark as flaky** if retries are needed
4. **Document the root cause** in test comments

## Known Flaky Tests

None currently. If a test becomes flaky:

1. Mark it with `[Trait("Flaky", "true")]`
2. Add a comment explaining why it's flaky
3. Update this document with the test name and issue
4. Create a GitHub issue to track fixing the root cause

## Best Practices

1. **Always use deterministic seeds** for Random instances in tests
2. **Log seeds** when debugging flaky tests (see `TestSeedHelper.CreateRandomForTest`)
3. **Use fixed delays** instead of random delays in E2E tests when possible
4. **Isolate tests** - each test should be independent
5. **Clean up resources** - ensure proper disposal in `IAsyncLifetime.DisposeAsync`

## CI Configuration

The test workflow (`.github/workflows/test.yml`) runs all tests including flaky ones. To exclude flaky tests in CI, add:

```yaml
- name: Run Tests (excluding flaky)
  run: dotnet test --filter "Flaky!=true"
```

## Debugging Flaky Tests

When a test fails intermittently:

1. Check if it uses randomness - add a deterministic seed
2. Check timing issues - increase delays or use `Task.Delay` with longer timeouts
3. Check resource cleanup - ensure all resources are disposed
4. Check test isolation - ensure tests don't share state
5. Log the seed used - add logging to identify patterns

Example:
```csharp
[Fact]
public async Task MyTest()
{
    var seed = TestSeedHelper.GetSeedForTest(nameof(MyTest));
    Console.WriteLine($"Test seed: {seed}"); // Log for debugging
    var random = new Random(seed);
    // ... test implementation
}
```



