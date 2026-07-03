# How to Test

## Testing philosophy

This repo follows the classic **test pyramid**: many fast, isolated tests at the bottom; progressively fewer as tests get bigger, slower, and more integrated. The governing rule:

> **Use the lowest-level test that would catch the regression.** Move up a layer only when the lower layer would mock away the thing that might break.

The rungs, bottom to top:

1. **Unit tests** (`ScoreTracker.Tests/DomainTests/`) — value types, domain services, pure logic. Real domain objects, no mocks where avoidable, no I/O, no clock/RNG calls (seams below). This is where dense branching logic gets exhaustive coverage.
2. **Component tests** (`ScoreTracker.Tests/ApplicationTests/`) — MediatR handlers and sagas. Mock the Domain *ports* (repositories, `IBus`, clock), construct the real handler, assert returned values and `Verify` side effects. Note the MassTransit in-memory transport **is the production transport** — a test through `IBus` is exercising the real thing.
3. **Architecture tests** (`ScoreTracker.Tests/ArchitectureTests/`) — ratchets that enforce the layer rules, vertical public surfaces, and message taxonomy. Rules are added, never removed.
4. **API wire-shape approval tests** (`ScoreTracker.Tests.Api/`) — pin the exact JSON contracts of the public `api/*` endpoints. A failure means a partner-facing contract changed: that's breaking-change review territory, not a test to casually re-approve.
5. **Real-database integration tests** (`ScoreTracker.Tests.Integration/`) — run against a real SQL Server provisioned by Testcontainers, with migrations applied and Respawn resetting state between tests. For what only a real engine can verify: migrations, provider-specific SQL, constraints, transactions.

Two hard rules on dependency realism:

- **SQL Server is never faked.** No EF in-memory provider, no SQLite substitute — those can't validate migrations, real SQL, indexes, or concurrency, and tests using them must not be called integration tests. Repository-level logic in the fast suite mocks the port instead.
- **Don't test through the browser what a lower rung covers.** There is no E2E suite today; the pyramid's upper floors are the approval and integration suites.

Tests are classified by **folder-as-tag**: the project/folder a test lives in *is* its classification — no per-test trait attributes to maintain.

## Running the tests

### Fast suites — no Docker required

```sh
# Unit + component + architecture tests
dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj

# API wire-shape approval tests
dotnet test ScoreTracker/ScoreTracker.Tests.Api/ScoreTracker.Tests.Api.csproj
```

These are the PR gate. Run both before opening a PR.

### Integration suite — Docker required

```sh
dotnet test ScoreTracker/ScoreTracker.Tests.Integration/ScoreTracker.Tests.Integration.csproj
```

**Docker Desktop must be running.** The suite provisions its own ephemeral SQL Server 2025 container via Testcontainers (first run pulls the image, ~2 min), applies every EF migration, runs the tests with Respawn wiping the `scores` schema between them, and tears the container down. No local SQL setup or connection string needed.

### What CI runs

Every PR and every merge to `main` runs all three suites on [Azure Pipelines](https://dev.azure.com/joneccker/ScoreTracker) — the fast suites on a Windows agent, the integration suite on a Linux agent with Docker. Merges to `main` additionally build the deployable artifact and wait at a manual approval gate before deploying.

## Conventions

- **Frameworks**: xUnit + Moq. Don't introduce other doubling libraries (FakeItEasy, NSubstitute, AutoFixture) without prior approval.
- **Naming**: test names describe *behavior*, not implementation — `ApproveClearsNeedsApprovalAndSnapshotsVerificationType`, not `CallsRepository`.
- **Builders**: construct test data with the fluent builders in `ScoreTracker.Tests/TestData/` (`UserBuilder`, `ChartBuilder`, …). Add a builder once you hand-construct the same type in two tests.
- **Clock seam**: never call `DateTime.Now` / `DateTimeOffset.UtcNow` in Application code or tests — inject `IDateTimeOffsetAccessor`, and in tests use `FakeDateTime.At(...)` from `TestHelpers/`.
- **Randomness seam**: inject `IRandomNumberGenerator`; never `Random.Shared` in Application code.
- **Coverage exclusions**: pure data shapes (commands, queries, events, records, exceptions, vertical `Contracts/` records) are marked `[ExcludeFromCodeCoverage]` so coverage reflects logic. Never exclude `Domain/Models`, `Domain/Services`, `Domain/ValueTypes`, or `Application/Handlers`.
- **Don't weaken assertions to make a change pass.** If an existing test looks wrong, isolate the test change from the behavior change and explain why.
