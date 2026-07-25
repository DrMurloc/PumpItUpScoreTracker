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
6. **E2E tests** (`ScoreTracker.Tests.E2E/`) — Playwright driving a headless Chromium against the **real web app on Kestrel** (real DI graph, real MassTransit in-memory bus, real migrations, real SQL Server via Testcontainers), with WireMock answering as phoenix.piugame.com from snapshotted pages. Reserved for **critical user workflows** whose breakage a lower rung can't see: the PIUGAME login flow, the score-import saga end to end, the tier-list page rendering.

Two hard rules on dependency realism:

- **SQL Server is never faked.** No EF in-memory provider, no SQLite substitute — those can't validate migrations, real SQL, indexes, or concurrency, and tests using them must not be called integration tests. Repository-level logic in the fast suite mocks the port instead.
- **Don't test through the browser what a lower rung covers.** The E2E suite exists for whole-workflow wiring (page → controller/circuit → handlers → bus → SQL → render), not for logic edge cases — a new scoring rule gets a unit test, not a browser test.

Tests are classified by **folder-as-tag**: the project/folder a test lives in *is* its classification — no per-test trait attributes to maintain.

## Running the tests

### Fast suites — no Docker required

```sh
# Unit + component + architecture tests
dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj

# API wire-shape approval tests
dotnet test ScoreTracker/ScoreTracker.Tests.Api/ScoreTracker.Tests.Api.csproj

# Blazor component tests (bUnit, mocked data)
dotnet test ScoreTracker/ScoreTracker.Tests.Components/ScoreTracker.Tests.Components.csproj
```

These are the PR gate. Run all three before opening a PR. The granularity ladder for UI
work: component behavior belongs in the bUnit suite with mocked data; Playwright (below)
is reserved for critical whole-workflow user journeys.

### Integration suite — Docker required

```sh
dotnet test ScoreTracker/ScoreTracker.Tests.Integration/ScoreTracker.Tests.Integration.csproj
```

**Docker Desktop must be running.** The suite provisions its own ephemeral SQL Server 2025 container via Testcontainers (first run pulls the image, ~2 min), applies every EF migration, runs the tests with Respawn wiping the `scores` schema between them, and tears the container down. No local SQL setup or connection string needed.

### E2E suite — Docker required, browser auto-installed

```sh
dotnet test ScoreTracker/ScoreTracker.Tests.E2E/ScoreTracker.Tests.E2E.csproj
```

Boots the whole stack once per run: an ephemeral SQL Server (Testcontainers), a WireMock server impersonating phoenix.piugame.com (snapshotted pages in `ScoreTracker.Tests.E2E/PiuGame/Fixtures/`), the real web app hosted on Kestrel through `WebApplicationFactory.UseKestrel`, and a headless Chromium (downloaded automatically on first run, ~150 MB). No real PIU account and no network access to piugame needed — logins and imports run entirely against the stub. Like the integration suite, it is opt-in locally: run it before a PR that touches login, import, or page-level flows.

## The exploration workbench — manual only, never CI

`ScoreTracker.ExplorationTests` is a **manual-only workbench**, not a test suite: a sanctioned place to crawl live pages (**read-only unless the owner explicitly asks for a mutation**), download files, and try formulas against real data/images (OCR experiments will land here). It is named in no `dotnet test` step in `azure-pipelines.yml`, so it **never runs in CI**; it builds with the solution and is excluded from Sonar. Every test is config-gated and **skips** when its secrets are absent, so the assembly is inert by default. Nothing here is a feature guarantee — a formula that must hold belongs in `ScoreTracker.Tests` or `Tests.Integration`.

### Live-site probes — PIU account required

`ExplorationTests/LiveSite/` exercises every scraper method the score-import flow depends on **against the real phoenix.piugame.com** — the code most likely to break in production, since PIU changes their site without notice. GET-only crawls (read-only). They need a real PIU account and **skip automatically when none is configured**.

To run them locally, configure credentials once in the shared user-secrets store (the same one the Aspire AppHost uses):

```sh
dotnet user-secrets set "PiuTest:Username" "..." --project ScoreTracker/ScoreTracker.AppHost
dotnet user-secrets set "PiuTest:Password" "..." --project ScoreTracker/ScoreTracker.AppHost
```

(`PIU_TEST_USERNAME` / `PIU_TEST_PASSWORD` environment variables also work and take precedence.) Then:

```sh
dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj --filter "FullyQualifiedName~PiuGameLiveSiteTests"
```

### Catalog probes — a populated database required

`ExplorationTests/Catalog/` checks the parts of the domain that are hardcoded against catalog data. `TitleChartResolutionProbeTests` walks every chart-specific title in both title lists and asserts at least one real chart satisfies its `AppliesToChart` — the guard against a title naming a song the way the official requirement page abbreviates it ("Phalanx" for `Phalanx "RS2018 edit"`), which matches nothing and leaves the title frozen at zero forever with no error. CI cannot run this: its database is a migrated, empty schema, so every title would "fail" for the wrong reason. **Run it after editing a title's song/type/level, and after a mix's chart levels shift.** Read-only (SELECTs only).

Configuration (skips automatically when absent) — point it at the prod-synced local Aspire database or a read replica:

```sh
dotnet user-secrets set "CatalogProbe:ConnectionString" "Server=127.0.0.1,14330;Database=ScoreTracker;User Id=sa;Password=...;TrustServerCertificate=true" --project ScoreTracker/ScoreTracker.AppHost
```

(`SCORETRACKER_CATALOG_CONNECTION` also works and takes precedence.) Then:

```sh
dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj --filter "FullyQualifiedName~TitleChartResolutionProbe"
```

The offline half of that guard — spelling drift between the two title lists, plus the corrected names pinned against regression — is `ScoreTracker.Tests/DomainTests/TitleSongNameTests`, and does run on every PR.

### Discord canary — testing bot required, manual runs only

`ExplorationTests/DiscordCanary/` posts the sample Components V2 score cards to the owner's private lab channel with the **testing** bot and reads them back over REST — catching what component tests can't: Discord API contract drift, emoji-id resolution, and token/permission validity. It is the one exploration test that **writes** to a remote by design (the owner's own lab channel). **Run it manually when a change touches Discord or Communities code.** Messages are left in the channel on purpose: it doubles as a visual gallery of what the cards looked like on every run.

Configuration (skips automatically when absent): `Discord:BotToken` + `DiscordTest:CanaryChannelId` in the shared AppHost user-secrets store, or `DISCORD_CANARY_TOKEN` / `DISCORD_CANARY_CHANNEL` environment variables. Then:

```sh
dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj --filter "FullyQualifiedName~DiscordCanaryTests"
```

**Real-session showcase** (same folder): `RealSessionShowcaseTests` replays a real play session out of the **local development database** through the production pipeline — it picks the best-looking historical day from the player's records, computes honest highlight flags and folder lamps against today's data (Score Quality is skipped; a local database has no comparable-player cohort), stamps journal/highlight/milestone rows so the card's deep link opens that session on the Sessions page, and publishes the captured event on the real in-memory bus so the real CommunitySaga + renderer + testing bot produce the card. Use it to preview card-design changes with real data. It additionally needs `DiscordTest:ExampleConnectionString` (the local Aspire SQL database) in user-secrets, temporarily attaches the lab channel to the World community, hard-gates that no other Discord channel exists in that database before publishing, and detaches afterwards. It **mutates the local database** (demo session rows) — never point it anywhere but local dev.

```sh
dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj --filter "FullyQualifiedName~RealSessionShowcaseTests"
```

### What CI runs

Every PR and every merge to `main` runs the five automated suites on [Azure Pipelines](https://dev.azure.com/joneccker/ScoreTracker) — the fast suites (unit/component, API approval, bUnit components) on a Windows agent, the integration and E2E suites on parallel Linux agents with Docker. **The exploration workbench never runs in CI**: no `dotnet test` step names it, and its live-site and Discord tests are config-gated anyway (CI provisions no PIU account or Discord token, so they would skip). Scraper drift is caught by running the live-site probes locally when the scraper changes, not by the gate — a flaky real site must never be able to fail a PR. Merges to `main` additionally build the deployable artifact and wait at a manual approval gate before deploying.

## Conventions

- **Frameworks**: xUnit + Moq (+ bUnit in `Tests.Components` only). Don't introduce other doubling libraries (FakeItEasy, NSubstitute, AutoFixture) without prior approval.
- **Naming**: test names describe *behavior*, not implementation — `ApproveClearsNeedsApprovalAndSnapshotsVerificationType`, not `CallsRepository`.
- **Builders**: construct test data with the fluent builders in `ScoreTracker.Tests/TestData/` (`UserBuilder`, `ChartBuilder`, …). Add a builder once you hand-construct the same type in two tests.
- **Clock seam**: never call `DateTime.Now` / `DateTimeOffset.UtcNow` in Application code or tests — inject `IDateTimeOffsetAccessor`, and in tests use `FakeDateTime.At(...)` from `TestHelpers/`.
- **Randomness seam**: inject `IRandomNumberGenerator`; never `Random.Shared` in Application code.
- **Coverage exclusions**: pure data shapes (commands, queries, events, records, exceptions, vertical `Contracts/` records) are marked `[ExcludeFromCodeCoverage]` so coverage reflects logic. Never exclude `Domain/Models`, `Domain/Services`, `Domain/ValueTypes`, or `Application/Handlers`.
- **Don't weaken assertions to make a change pass.** If an existing test looks wrong, isolate the test change from the behavior change and explain why.
