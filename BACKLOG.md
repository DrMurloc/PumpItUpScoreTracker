# Backlog

Items where the codebase isn't yet ready for open-source contributions or doesn't yet satisfy [ENTERPRISE.md](ENTERPRISE.md), and the gap requires real refactor work — not just documentation. Items here are not current rules; `CLAUDE.md` describes what the project *does today*, this file describes what it *should grow into*.

Scope estimates are rough order of magnitude (S = days, M = a week, L = multiple weeks).

## Current priority: open-source readiness

The immediate goal is getting this codebase into a good state for community contributions. The phased list below is the contribution-velocity priority. Items in BACKLOG that don't help that goal directly are deferred — they remain valuable for code quality but are off the critical path until OSS readiness lands.

| Phase | Theme | Scope |
|---|---|---|
| 1 | Local dev without prod credentials — docker-compose, dev-mode fakes, auto-migrate | M |
| 2 | Confidence floor for incoming PRs — taxonomy labels, characterization workflow, reference handler tests | S–M |
| 3 | Community signaling — CHANGELOG, good-first-issue labels | S |

Phase 1/3 detail is in [Open-source readiness](#open-source-readiness) below. Phase 2 detail lives in [Test infrastructure](#test-infrastructure) (existing items, reordered for OSS goal). Items deferred until after OSS readiness lands are recapped in [Deferred for OSS goal](#deferred-for-oss-goal).

---

## Open-source readiness

New work specific to enabling external contributions. Existing test-infra and architecture-cleanup items live in their original sections below.

### Phase 1 — Local dev without prod credentials — *M*

The single biggest practical barrier today. Booting the app currently requires SQL Server + Discord/Google/Facebook OAuth + SendGrid + Azure Blob. Subsumes the older *Local-dev bring-up script* item under Process/docs.

- [x] **`docker-compose.yml`** for SQL Server + Azurite with documented connection strings and a one-line `docker compose up`.
- [x] **`appsettings.Development.example.json`** with placeholders for every secret consumed (SQL, Discord/Google/Facebook OAuth, SendGrid, Azure Blob).
- [x] **"Local mode" / DevAuth backdoor** so the app boots without OAuth credentials. `DevAuth` config (`DevAuthConfiguration.Enabled`) gates two `LoginController` endpoints: `POST /Login/Dev` (sign in as picked user) and `POST /Login/Dev/Bootstrap` (create + sign in as a fresh "Dev User" for empty-DB cases). `Login.razor` renders a Dev Login card when enabled. Endpoints return 404 when disabled.
- [x] **`scripts/seed-export.{sh,ps1,sql}`** — automates building a public-only seed `.bak` from a prod `.bacpac` (or `.bak`) export. Auto-detects source format, restores into the dev SQL container, drops non-public users (cascading FKs purge their data) plus auth/credential tables, backs up the pruned DB, copies it out, and cleans up. See [scripts/README.md](scripts/README.md). Re-run any time after a schema change or refresh request.
- [ ] **Hosting**: upload a produced `seed-YYYY-MM-DD.bak` to a public location. GitHub Releases recommended (free, public, versioned). Maintainer-owned.
- [ ] **`scripts/dev-up.{sh,ps1}`** that downloads the hosted seed and restores it into the dev container. The contributor-facing bring-up step.
- [ ] **Local-dev "how to run" docs** in `CONTRIBUTING.md` (or `README.md` — pick one home for general bring-up instructions and link from the other). Cover the common flows a contributor needs end-to-end:
  - [ ] EF migrations: `dotnet ef database update --project ScoreTracker/ScoreTracker.Data --startup-project ScoreTracker/ScoreTracker` (and add-migration counterpart). Replaces the dropped auto-migrate path — contributors run this explicitly after `docker compose up -d`.
  - [ ] Build: `dotnet build ScoreTracker/ScoreTracker.sln -c Release`.
  - [ ] Tests: `dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj`.
  - [ ] Docker compose lifecycle: `docker compose up -d`, `docker compose down`, `docker compose down -v` (wipe volumes).
  - [ ] DevAuth flow: enable in `appsettings.Development.json`, use the Dev Login card, what the bootstrap button does.

### Phase 3 — Community signaling — *S*

After Phase 1 lands.

- [ ] **`CHANGELOG.md`** with a light release/version policy. Even an informal "what changed" log helps contributors track impact.
- [ ] **Curated "good first issue" labels** on real, scoped GitHub Issues. Process work, not a doc deliverable.
- [ ] **Link `BACKLOG.md` from CONTRIBUTING** as "areas where help is most welcome." Existing BACKLOG becomes the on-ramp.

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

### Test taxonomy / dependency-realism labels — *S* — **Phase 2**

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

### Characterization tests as a workflow — *S* (recurring) — **Phase 2**

ENTERPRISE: *Characterization Test*, *Refactoring Rules*. Promoted to Phase 2 of OSS readiness — this is how a contributor safely touches legacy code.

- [ ] Pick one upcoming legacy refactor and add characterization tests around the existing behavior first.
- [ ] Establish naming: `<Type>CharacterizationTests.cs` or a `[Trait("category", "characterization")]` tag.
- [ ] Document the workflow as a PR-template checkbox: "Did this PR change legacy behavior? If yes, characterization tests added before behavior change?"

### Reference handler tests — *S* — **Phase 2**

There is exactly one handler test today (`CreateUserHandlerTests`). Contributors copy from existing examples, so a sparse reference set keeps the bar artificially low.

- [ ] Add 5–10 reference handler tests covering high-traffic flows (score upload, user create, leaderboard read, tournament submission).
- [ ] Follow the canonical pattern documented in `CLAUDE.md` *Component tests* — mock Domain ports, real handler, `Verify` side effects with `It.Is<T>` + `Times.Once`.
- [ ] Cross-reference these from `CONTRIBUTING.md` as the "copy from these" set.

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

- [ ] Decide policy: auto-migrate on startup (simpler) vs. require an explicit migration step in deploys (safer). Tried Dev-env auto-migrate during Phase 1 OSS work and rolled it back — startup-time `Database.Migrate()` had issues against the dev SQL Server container. Current state: contributors run `dotnet ef database update` manually (see Phase 1 docs item).
- [ ] If auto-migrate (revisited): add `db.Database.Migrate()` to `Program.cs` startup with appropriate error handling.
- [ ] If explicit (status quo): make sure the manual migration command is documented.

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

### Fork-PR CI feedback — *S* (deferred)

Fork PRs don't trigger Azure Pipelines today. Adding pre-merge CI for forks adds no signal until CI runs gates a contributor *can't* easily reproduce locally — `dotnet build` + `dotnet test` on a contributor's machine produces the same answer the pipeline would. Until then, the contract documented in [CONTRIBUTING.md](CONTRIBUTING.md) stands: contributors run tests locally; maintainers verify after merge.

Revisit when at least one CI-only quality gate exists, e.g.:
- Coverage threshold enforced in CI.
- Static analysis or security scans wired into the pipeline.
- Real-dependency integration tests (containerized DB, etc.) — see *Real-dependency database tests*.
- Mutation-test runs — see *Mutation testing on Domain*.

When that lands:
- [ ] Add a `pr:` trigger block to `azure-pipelines.yml` (`pr: branches: include: [main]`).
- [ ] In Azure DevOps → Pipelines → Triggers → Pull request validation, enable *Build pull requests from forks of this repository*.
- [ ] Audit secret variables in the pipeline; disable any that shouldn't be exposed on fork builds. The current build needs none.

Depends on: at least one CI-only quality gate from the *Test infrastructure* section.

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

### Local-dev bring-up script — *S* — **superseded by Phase 1**

The Phase 1 `docker-compose.yml` covers this — a separate shell script isn't needed once compose is in place.

- [ ] `scripts/dev-up.sh` (or PowerShell equivalent) that starts SQL Server in a container with a known connection string.
- [ ] Document in `CLAUDE.md` *Local external dependencies*.

---

## Deferred for OSS goal

These items remain valuable but are not on the OSS-readiness critical path. Revisit after Phases 1–3 land. Each retains its detail in the relevant section above.

**Code-quality test work** (deferred — protects maintainers from provider drift, but doesn't unblock first-time contributors)
- *External-adapter / consumer tests* (was top-3 #1 by ENTERPRISE-compliance impact).
- *Real-dependency database tests* (was top-3 #2; high CI cost — Docker on Linux pool).
- *MassTransit message-flow tests*.
- *Property-based tests on value types*.
- *Mutation testing on Domain*.
- *E2E / acceptance tests* (already deferred).
- *Contract tests* (already deferred — no external consumer of this app's API exists).

**CI infrastructure** (deferred — pre-merge CI on fork PRs only adds value once it runs gates contributors can't reproduce locally)
- *Fork-PR CI feedback* — wait until coverage / security scans / real-dep integration tests / mutation tests give CI runs non-redundant signal.

**Architecture cleanups** (deferred — internal improvements, no contribution-velocity impact)
- *Remove `Data → Application` reference*.
- *Move command-shaped messages out of `Domain/Events/`*.
- *MediatR-in-Domain decision* (already a documented carve-out).
- *MassTransit version skew*.
- *`PersonalProgress` vertical-slice cleanup*.
- *ID generation seam*.

**Phase 1 dependency** (not deferred, just sequenced)
- *Automatic migration application* — execute as the dev-only auto-migrate path during Phase 1.

---

## Out of scope

Not on this backlog because they're either covered elsewhere or intentionally deferred:

- **Domain-vs-integration-event distinction.** Single bounded context with in-memory transport — the distinction has no current application. Revisit only when a second context appears.
- **Snapshot / approval tests.** Limited applicability in this codebase; the "tier list" and "letter difficulty" outputs *could* benefit, but semantic assertions work fine today.
- **MediatR pipeline behaviors for cross-cutting concerns (validation, logging, transactions).** Removed from ENTERPRISE.md; not a project gap. Add when a concrete need appears, not as policy compliance.
