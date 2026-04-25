# Backlog

Items where the codebase doesn't yet satisfy [ENTERPRISE.md](ENTERPRISE.md) and the gap requires real refactor work, not just documentation. Items here are not current rules — `CLAUDE.md` describes what the project *does today*, this file describes what it *should grow into*.

Each item references the ENTERPRISE.md section that motivates it. Scope estimates are rough order of magnitude (S = days, M = a week, L = multiple weeks).

## Top three by impact

1. **External-adapter testing.** Six external clients, zero tests today. Highest blast radius if any provider's contract drifts. *(External API and Messaging Tests)*
2. **Real-dependency database tests.** Production runs SQL Server; migrations, transactions, and provider-specific queries are unverified before deploy. *(Database Testing Rules)*
3. **Test taxonomy / dependency-realism labels.** No tagging convention exists; the test suite can't be sliced for fast/slow CI runs or for category-specific gates. *(Test Taxonomy, Dependency Realism Labels)*

---

## Test infrastructure

### Real-dependency database tests — *L*

ENTERPRISE: *Real-Dependency Integration Test*, *Database Testing Rules*

- [ ] Pick a Testcontainers strategy (Testcontainers.MsSql is the obvious option; LocalDB is a fallback for Windows-only CI).
- [ ] Add a base test class that spins up a container per test class (or per fixture), applies migrations, and tears down.
- [ ] Establish seed/cleanup convention: per-test transactions that roll back, or per-test `Respawn`-style truncation.
- [ ] Cover migrations: a smoke test that applies all 165 migrations from empty.
- [ ] Cover the highest-traffic repositories first: `EFUserRepository`, `EFChartRepository`, `EFPhoenixRecordsRepository`, `EFTournamentRepository`.
- [ ] CI: gate this suite separately from PR fast tests; tag with `integration-real`.
- [ ] Update CI runner to support Docker (Azure Pipelines `windows-latest` may need adjustment or a Linux pool for the container).

Depends on: tagging convention (below).

### External-adapter / consumer tests — *M*

ENTERPRISE: *External API and Messaging Tests*

Six external clients have zero coverage. Recommended order, lowest-risk first:

- [ ] `PiuGameApi` (HTTP, typed `HttpClient`) — fake `HttpMessageHandler` returning canned HTML/JSON; assert request shape and response parsing.
- [ ] `OfficialSiteClient`, `PiuTrackerClient` — same pattern.
- [ ] `SendGridAdminNotificationClient` — fake the SendGrid client.
- [ ] `AzureBlobFileUploadClient` — Azurite emulator (real-dep) or interface-level test.
- [ ] `DiscordBotClient` — interface-level test against `IBotClient`; full Discord flow is out of scope.
- [ ] Tag fake-server tests as `component`; emulator-backed tests as `integration-real`.

Where possible, capture the contract: real responses go in test fixtures, not hand-written.

### MassTransit message-flow tests — *S*

ENTERPRISE: *External API and Messaging Tests*

- [ ] One end-to-end test per recurring scheduled message (`UpdateBountiesEvent`, `CalculateScoringDifficultyEvent`, etc.) — start the in-memory bus, publish, assert the consumer side effects via mocked Domain ports.
- [ ] Tests classify as `component` (the in-memory transport is production-real, but ports are mocked).
- [ ] Reference: `RecurringJobHostedService` is the publisher; saga classes are the consumers.

### Test taxonomy / dependency-realism labels — *S*

ENTERPRISE: *Dependency Realism Labels*, *Test Taxonomy*

- [ ] Decide convention: xUnit `[Trait("category", "<label>")]` is the most flexible; folder names are more discoverable. Recommend `[Trait]` so multiple labels can attach to one test class.
- [ ] Apply labels across all 57 existing tests: `unit` for `DomainTests/`, `component` for `ApplicationTests/`.
- [ ] Add `dotnet test --filter Category=unit` examples to `CLAUDE.md`'s Commands section.
- [ ] Add a CI fast-PR job that runs only `unit + component`.

Blocks: real-dependency DB tests, external-adapter tests (both need labels to gate independently).

### Property-based tests on value types — *S*

ENTERPRISE: *Property-Based Test*

- [ ] Add `FsCheck.Xunit` or `CsCheck` to `ScoreTracker.Tests`.
- [ ] Cover `PhoenixScore`, `XXScore`, `DifficultyLevel`, `Bpm`, `Name` with invariant tests (round-trip serialization, ordering, validation boundaries).
- [ ] Use deterministic seeds; report failing seed in output.

### Characterization tests as a workflow — *S* (recurring)

ENTERPRISE: *Characterization Test*, *Refactoring Rules*

- [ ] Pick one upcoming legacy refactor and add characterization tests around the existing behavior first.
- [ ] Establish naming: `<Type>CharacterizationTests.cs` or a `[Trait("category", "characterization")]` tag.
- [ ] Document the workflow as a PR-template checkbox: "Did this PR change legacy behavior? If yes, characterization tests added before behavior change?"

### Mutation testing on Domain — *S* (low priority)

ENTERPRISE: *Mutation Test* (marked "if applicable")

- [ ] Add Stryker.NET configured to mutate `ScoreTracker.Domain.ValueTypes` and `Domain.Services` only.
- [ ] Run on a schedule, not per-PR.
- [ ] Use the score as a signal, not a gate.

### E2E / acceptance tests — *L* (only if needed)

ENTERPRISE: *End-to-End / Acceptance Test*

Currently skipped — no critical user-visible flow is regressed often enough to justify the maintenance cost. Revisit when:
- A second deployment environment exists, or
- A regression escapes to production that component/slice tests would have caught.

Likely tooling: bUnit for Blazor Server components, Playwright for full browser flows.

### Contract tests — *M* (only if a consumer appears)

ENTERPRISE: *Contract Test*

Currently skipped — the codebase consumes external APIs but exposes only a thin MVC surface; there are no known external consumers of this app's API. Revisit if:
- A second service starts consuming this app's API, or
- A breaking change in `PiuGameApi` causes a production incident that contract verification would have caught.

---

## Architecture cleanups

### Remove `Data → Application` reference — *M*

ENTERPRISE: *Architecture Priorities*. Also flagged in [ARCHITECTURE.md](ARCHITECTURE.md) under known divergences.

- [ ] Audit the six `Data/` files that import `ScoreTracker.Application` (`EFTournamentRepository`, `EFRandomizerRepository`, `EFPlayerStatsRepository`, `EFPlayerHistoryRepository`, `EFPhoenixRecordsRepository`, `OfficialSiteClient`).
- [ ] For each, identify what's being pulled from Application. Likely candidates: command/query types being constructed, MediatR types, helper utilities.
- [ ] Move shared types to Domain or duplicate where appropriate.
- [ ] Drop the project reference in `ScoreTracker.Data.csproj`.

Risk: low — known divergence, no business impact.

### Move command-shaped messages out of `Domain/Events/` — *S*

ENTERPRISE: *DDD Policy*. Also flagged in [ARCHITECTURE.md](ARCHITECTURE.md).

- [ ] Move `ProcessPassTierListCommand` and `ProcessScoresTiersListCommand` to `Application/Commands/` (or a new `Application/Messages/` if they remain MassTransit-shaped).
- [ ] Update consumers and publishers (likely just `RecurringJobHostedService` and the saga consumers).
- [ ] Verify scheduled messages still fire after the move.

### MediatR-in-Domain decision — *M* (or zero, if accepted as carve-out)

ENTERPRISE: *Architecture Priorities*

Two paths:
- **Option A (status quo)**: keep the carve-out documented in `CLAUDE.md`. Zero work.
- **Option B (full compliance)**: move `IRequest`/`INotification` types out of Domain. Domain `Records/` and `Events/` types currently used as MediatR messages would need to move to Application. Larger refactor; check whether any are referenced by Domain-internal code.

No urgency. Revisit only if multiple projects on the team adopt strict ENTERPRISE compliance and this becomes the odd one out.

### MassTransit version skew — *S*

Flagged in [ARCHITECTURE.md](ARCHITECTURE.md).

- [ ] `Web` uses `MassTransit.Extensions.DependencyInjection 7.3.1`; rest uses `MassTransit 8.5.7`. Consolidate on v8 DI extensions.
- [ ] Verify in-memory transport setup still works after the upgrade.

### Automatic migration application — *S*

Flagged in [ARCHITECTURE.md](ARCHITECTURE.md).

- [ ] Decide policy: auto-migrate on startup (simpler) vs. require an explicit migration step in deploys (safer).
- [ ] If auto-migrate: add `db.Database.Migrate()` to `Program.cs` startup with appropriate error handling.
- [ ] If explicit: document the deploy-time migration command.

### `PersonalProgress` vertical-slice cleanup — *M*

Flagged in [ARCHITECTURE.md](ARCHITECTURE.md).

- [ ] Decide: fold `PersonalProgress` back into `Application`, or formalize the vertical-slice split for other features.
- [ ] If folding back: move `PlayerRatingSaga` and queries into `Application/`; drop the project.
- [ ] If formalizing: document the split rule in `CLAUDE.md` and stop treating it as experimental.

### ID generation seam — *S*

ENTERPRISE: *Test Data Rules* ("stable IDs")

- [ ] Introduce `IGuidGenerator` (or similar) port in `Domain/SecondaryPorts/`.
- [ ] Audit Domain factories for `Guid.NewGuid()` calls (e.g. `User(...)`, `TournamentConfiguration(...)`).
- [ ] Decide whether to thread the port through or accept that Domain construction is a controlled boundary.
- [ ] Update test conventions: the `IGuidGenerator` mock becomes part of handler tests where ID assertions matter.

Lower priority — current ID assertions in tests use `Assert.NotEqual(Guid.Empty, ...)` style which works without a seam.

---

## Process / docs

### CI fast-PR vs. nightly split — *S*

Depends on: dependency-realism labels.

- [ ] Define which labels run on PR (`unit`, `component`) vs. nightly (`integration-real`, `external`, `mutation`).
- [ ] Update `azure-pipelines.yml` to add the filter; possibly split into two pipelines.

### Test running documentation — *S*

ENTERPRISE: *Project CLAUDE.md Requirements*

Once labels exist, expand `CLAUDE.md` Commands section with concrete filter examples:
- [ ] `dotnet test --filter Category=unit` — fast PR check.
- [ ] `dotnet test --filter Category=component` — handler/saga checks.
- [ ] `dotnet test --filter Category=integration-real` — DB and emulator-backed checks (requires Docker).

### Local-dev bring-up script — *S*

- [ ] `scripts/dev-up.sh` (or PowerShell equivalent) that starts SQL Server in a container with a known connection string.
- [ ] Document in `CLAUDE.md` *Local external dependencies*.

---

## Out of scope

Not on this backlog because they're either covered elsewhere or intentionally deferred:

- **Domain-vs-integration-event distinction.** Single bounded context with in-memory transport — the distinction has no current application. Revisit only when a second context appears.
- **Snapshot / approval tests.** Limited applicability in this codebase; the "tier list" and "letter difficulty" outputs *could* benefit, but semantic assertions work fine today.
- **MediatR pipeline behaviors for cross-cutting concerns (validation, logging, transactions).** Removed from ENTERPRISE.md; not a project gap. Add when a concrete need appears, not as policy compliance.
