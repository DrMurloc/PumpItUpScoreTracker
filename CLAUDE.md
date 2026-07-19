# PumpItUpScoreTracker

Blazor Server + MVC API for tracking Pump It Up scores, leaderboards, tournaments, and player progression. Bounded-context vertical slices on an onion core: pure `Domain`, MediatR-based `Application`, EF Core `Infrastructure` (`Data`), Blazor/MVC `Presentation` (`Web`), wired through `CompositionRoot`. Async work runs on MassTransit with an in-memory transport.

## Imports

- `@docs/ARCHITECTURE.md` — architecture philosophy (bounded-context verticals, DDD + onion + hexagonal) and the code map. This file (CLAUDE.md) holds the machine-enforceable conventions that realize that philosophy.

@docs/ARCHITECTURE.md

## Documentation set

Reader-facing docs live in `docs/` (README.md stays at the root). Keep them current **in the same PR** as the change that invalidates them:

- [README.md](README.md) — intro + doc index
- [docs/HOW-TO-RUN.md](docs/HOW-TO-RUN.md) — prerequisites, Aspire local run, the /Dev/Populate harness, optional secrets
- [docs/HOW-TO-TEST.md](docs/HOW-TO-TEST.md) — test philosophy + suite commands
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — update in the same PR that changes a structural pattern
- [docs/UX-GUIDELINES.md](docs/UX-GUIDELINES.md) — the design system (per-mix themes, color tokens, the two semantic ramps) + the ten UX rules; update in the same PR that changes a UI pattern
- [docs/DATABASE-SCHEMA.md](docs/DATABASE-SCHEMA.md) — new tables get a row
- [docs/API.md](docs/API.md) — the API surface map; Swagger is shape truth
- [docs/SCHEDULED-JOBS.md](docs/SCHEDULED-JOBS.md) — new recurring jobs get a row
- [docs/TECHNOLOGIES.md](docs/TECHNOLOGIES.md) — new stack pieces get an entry
- [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) — the owner's contribution policies
- [docs/DOMAIN.md](docs/DOMAIN.md) — PIU domain glossary (Mix, Chart, Phoenix score, Pumbility, Tier list, UCS, Saga)
- [docs/LOCALIZATION-INTAKE-TEMPLATE.md](docs/LOCALIZATION-INTAKE-TEMPLATE.md) — volunteer intake form for bootstrapping a new locale; Claude instantiates it per language and consumes the filled form (instructions inside the file)

Decision rationale for the vertical architecture is [docs/adr/ADR-001-subdomain-verticals.md](docs/adr/ADR-001-subdomain-verticals.md) — code comments cite it as `ADR-001 D2/D3/D4/Q8`. The maintainer is redoing product/roadmap docs post-Phoenix-2-announcement; until they exist, feature-fit and priority questions go to the owner, not a doc.

## Commands

Run from the repo root (the solution lives at `ScoreTracker/ScoreTracker.sln`).

- **Run locally**: `dotnet run --project ScoreTracker/ScoreTracker.AppHost` (requires Docker Desktop). Aspire provisions a deterministic SQL Server container (port + sa password pinned in `ScoreTracker.AppHost/appsettings.json`, persistent volume), applies EF migrations automatically (`AutoMigrate` flows from AppHost), and enables the DevAuth login backdoor. Optional local secrets (real OAuth creds etc.) live in **AppHost user-secrets** and flow through to the Web app; the dev-harness API token is pasted on the /Dev/Populate page instead. See [docs/HOW-TO-RUN.md](docs/HOW-TO-RUN.md).
- **Build**: `dotnet build ScoreTracker/ScoreTracker.sln -c Debug`. **Build Debug, not Release** — the Aspire local run (and Visual Studio) uses Debug artifacts, and a Release build leaves the running app on a mismatched configuration, which surfaces as spurious Visual Studio errors and runtime exceptions that look like real bugs. Use `-c Release` only to reproduce a CI failure, and only when the app is not running.
- **Typecheck**: covered by `dotnet build` (no separate step).
- **Lint/static analysis**: none locally configured. In CI: SonarQube Cloud analyzes PRs and main from the pipeline (quality gate + PR decoration, EF Migrations excluded); CodeQL scans via GitHub default setup; Dependabot covers dependency alerts + weekly grouped version bumps.
- **Unit tests**: `dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj` — covers `DomainTests/` (unit), `ApplicationTests/` (component), and `ArchitectureTests/` (ratchets). No Docker required.
- **Component / module tests**: same command, same project.
- **Contract tests (API wire-shape approval)**: `dotnet test ScoreTracker/ScoreTracker.Tests.Api/ScoreTracker.Tests.Api.csproj` — pins the exact JSON wire shapes of the public `api/*` endpoints that partner tools consume (controller + mocked `IMediator`, golden JSON inline). A failure here means a public API contract changed — that is breaking-change review territory, not a test to casually update. The `dev/export/*` endpoints are explicitly OUTSIDE this contract: they serialize raw table rows for the local dev harness, are hidden from Swagger, shift with the schema (including breaking changes), and integrators must not build against them — the stable surface is `api/*` only.
- **Real-dependency integration tests**: `dotnet test ScoreTracker/ScoreTracker.Tests.Integration/ScoreTracker.Tests.Integration.csproj` — spins up an ephemeral SQL Server container via Testcontainers.MsSql, applies migrations, runs the suite. Requires Docker Desktop (or equivalent).
- **E2E / Playwright tests**: `dotnet test ScoreTracker/ScoreTracker.Tests.E2E/ScoreTracker.Tests.E2E.csproj` — Playwright (headless Chromium, auto-installed on first run) driving the real web app hosted on Kestrel via `WebApplicationFactory.UseKestrel`, against an ephemeral SQL Server (Testcontainers) with WireMock impersonating phoenix.piugame.com from snapshot fixtures (`ScoreTracker.Tests.E2E/PiuGame/Fixtures/`). Requires Docker Desktop. Reserved for critical whole-workflow paths (PIUGAME login, score import, tier lists) — logic edge cases belong in lower suites. The app's PiuGame endpoints are configurable via the `PiuGame` config section (`PiuGameConfiguration` in OfficialMirror's `Wiring/`, defaults = production hosts).
- **Mutation tests**: not present.

CI: Azure Pipelines (`azure-pipelines.yml`), multi-stage dotnet-CLI YAML. Every PR and merge to `main` builds and runs all four suites (fast suites on Windows; integration and E2E on parallel Linux jobs with Docker); merges to `main` continue into an approval-gated production deploy that applies the EF migration bundle before zip-deploying the app.

## Architecture conventions

The machine-enforceable realization of the philosophy in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Architecture tests ratchet most of these; treat the rest as just as binding.

### Layer graph and per-layer package allowlist

```
SharedKernel ◄── Domain ◄── Application ◄── Data ◄── verticals ◄── Web ◄── CompositionRoot
```

(Verticals reference `Data` transitionally for the shared DbContext; only `Randomizer` still references `Application` transitionally. `Web` and `CompositionRoot` reference every vertical.)

| Project | Allowed external packages | Forbidden |
|---|---|---|
| `ScoreTracker.SharedKernel` | `MediatR` only | Everything else — the kernel references nothing. Namespaces: `ScoreTracker.SharedKernel.ValueTypes/Enums/Models`. |
| `ScoreTracker.Domain` | `MediatR`, `Microsoft.Extensions.Logging.Abstractions` (+ project ref to `SharedKernel`) | Anything else. No EF, no `HttpClient`, no MassTransit, no ASP.NET, no Azure/Discord/SendGrid. |
| `ScoreTracker.Application` | + `MassTransit.Abstractions`, `Microsoft.Extensions.Caching.Memory` | EF Core, ASP.NET, `HttpClient`, vendor SDKs. Application must never know it's behind a web server. |
| `ScoreTracker.Data` | + `Microsoft.EntityFrameworkCore.SqlServer`, `Azure.Storage.Blobs`, `Discord.Net`, `HtmlAgilityPack`, `SendGrid`, `SkiaSharp` (+ Linux native assets) + `QRCoder` (the share-card renderer) + `Azure.Security.KeyVault.Keys` / `Azure.Identity` (the remembered-credential KEK envelope, `IKeyEnvelope`) | ASP.NET. |
| `ScoreTracker.Web` | + `MudBlazor`, OAuth providers, `Swashbuckle`, MassTransit DI, `Hangfire.AspNetCore`, `Hangfire.SqlServer` | EF Core directly (must go through ports). |
| Vertical assemblies: `ScoreTracker.Ucs`, `ScoreTracker.ScoreLedger`, `ScoreTracker.OfficialMirror`, `ScoreTracker.Catalog`, `ScoreTracker.Randomizer`, `ScoreTracker.ChartIntelligence`, `ScoreTracker.WeeklyChallenge`, `ScoreTracker.EventCompetition`, `ScoreTracker.Communities`, `ScoreTracker.PlayerProgress`, `ScoreTracker.Identity` (UCS is the template) | `MediatR`, `MassTransit.Abstractions` (+ full `MassTransit` for verticals with bus consumers — `IRegistrationConfigurator`, used by the `AddXxxConsumers` hooks, lives in the full package), `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Caching.Memory`; `OfficialMirror` additionally `Microsoft.Extensions.Http` + `HtmlAgilityPack` (the PiuGame ACL) (+ project refs `Domain`, `Data` transitionally for the shared DbContext; only `Randomizer` still refs `Application` transitionally (`GetRandomChartsQuery`, gated on the Match-subsystem deletion); cross-vertical reads go through published ports (`IScoreReader`, `IPlayerStatsReader`, `ITitleRepository`, `IUserReader`), never SQL joins) | Everything else. Only `Contracts/` and `Wiring/` namespaces may be public (arch-test enforced, `VerticalBoundaryTests`); EF entities and internal domain types never cross the boundary. Verticals must not be referenced by `Application` (cycles through `Data → Application`). **MassTransit's `AddConsumers` scan skips internal types** — verticals with bus consumers expose an `AddXxxConsumers(IRegistrationConfigurator)` hook called from `Program.cs`'s `AddMassTransit` block (tripwire-tested). |
| `ScoreTracker.CompositionRoot` | DI extensions only (+ `Microsoft.EntityFrameworkCore.Design`, private, for the design-time factory) | Business logic. |
| `ScoreTracker.Tests` | + `xunit`, `Moq` | Other doubling libraries (see Test conventions). References `ScoreTracker.Application` (for handler tests) and `ScoreTracker.Data` (for parser approval tests against `PiuGameApi`). Does not reference `ScoreTracker.Web`. |
| `ScoreTracker.Tests.Api` | + `xunit`, `Moq` | One of the three sanctioned `ScoreTracker.Web`-referencing test projects — it pins the public API wire shapes, which live in Web's controllers/DTOs. Same doubling rules as `ScoreTracker.Tests`. |
| `ScoreTracker.Tests.Components` | + `xunit`, `Moq`, `bunit` | The second sanctioned `ScoreTracker.Web`-referencing test project — bUnit renders Blazor components with mocked data (owner directive: lowest test granularity possible; component behavior belongs here, not in Playwright). Fast suite, no Docker. |
| `ScoreTracker.Tests.Integration` | + `xunit`, `Moq`, `Testcontainers.MsSql`, `Respawn` | Mocking the port whose implementation is the subject of the test (e.g., do not mock `IPhoenixRecordRepository` when testing `EFPhoenixRecordsRepository`). Mocking incidental collaborator ports for setup — or `IPiuGameApi`-style external clients in slice tests — is fine. |
| `ScoreTracker.Tests.E2E` | + `xunit`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Playwright`, `WireMock.Net`, `Testcontainers.MsSql`, `Respawn` | The third sanctioned `ScoreTracker.Web`-referencing test project — it hosts the real app on Kestrel. No Moq: external boundaries are stubbed at the wire (WireMock for the PIU site) or with a hand-rolled DI fake (`FakeFileUploadClient` for Azure blob, the one boundary a wire stub can't cover). Seed data goes in via raw DbContext/SQL, never through repositories. |
| `ScoreTracker.Tests.Exploration` | + `xunit`, `Tesseract` (OCR — sanctioned here only) | The interactive sandbox assembly — snapshot-grabbing iteration against real local inputs (score-image extraction, vendor-schema checks). **Never wired into CI** (the pipeline invokes suites by explicit csproj); tests skip gracefully when local inputs are absent; read-only against external systems unless a mutative exploration is explicitly agreed. Not a regression gate — no other project may reference it. |

Adding a package outside its allowed layer is a violation. Adding a project reference that isn't in the graph above is a violation.

### Use case dispatch

- **Message taxonomy — folder + name + interface distinguish the kinds** (arch-test enforced, `MessageTaxonomyTests`):
  - **Queries** — `*Query` records implementing `IQuery<T>` (`ScoreTracker.SharedKernel.Messaging`), in `Application/Queries/` or a vertical's `Contracts/Queries/`. Read-only; never travel the bus.
  - **Commands (MediatR)** — `*Command` records implementing `IRequest`/`IRequest<T>`, in `Application/Commands/` or a vertical's `Contracts/Commands/`.
  - **Commands (bus triggers)** — `*Command` plain records (not `IRequest`) in `Application/Messages/`, published via `IBus` by `RecurringJobRunner`/admin pages. Imperative by design — do not rename to past-tense or move to `Domain/Events/`.
  - **Events** — `*Event` past-tense fact records (never `IRequest`) in `Domain/Events/` (bus), `Application/Events/` (`INotification`), or a vertical's `Contracts/Events/` (bus; publishing vertical owns the event once no Application consumer remains).
  - Handlers are `IRequestHandler<,>` / `IConsumer<>` implementations in `Application/Handlers/` or vertical `Application/` folders.
- **Razor pages, Blazor components, and MVC controllers dispatch exclusively via `IMediator`.** No `DbContext`, repository, or `HttpClient` is injected into Web code. (Exception: `Accessors/` types in Web that *implement* Domain ports.)
- Background work is published over `IBus.Publish(...)` — past-tense **facts** from `Domain/Events/` or vertical `Contracts/Events/`; imperative **trigger messages** from `Application/Messages/`. In-process notifications use `IMediator.Publish(INotification)`.
- **Recurring scheduled work uses Hangfire** with SQL Server storage (auto-created `HangFire` schema, durable across restarts). Each recurring job is a one-line `IBus.Publish(...)` on `RecurringJobRunner` ([RecurringJobRunner.cs](ScoreTracker/ScoreTracker/HostedServices/RecurringJobRunner.cs)); registrations live in [Program.cs](ScoreTracker/ScoreTracker/Program.cs) via `RecurringJob.AddOrUpdate<RecurringJobRunner>(...)`. Cron expressions are UTC. Do not introduce a second scheduler library; do not reintroduce hosted-timer / self-rescheduling-bus patterns. The Hangfire dashboard is mounted at `/hangfire` and gated on `User.IsAdmin` via [HangfireDashboardAuthorization.cs](ScoreTracker/ScoreTracker/Security/HangfireDashboardAuthorization.cs). Adding a new recurring job = a method on `RecurringJobRunner` + one `RecurringJob.AddOrUpdate` line + a row in [docs/SCHEDULED-JOBS.md](docs/SCHEDULED-JOBS.md). The `PreventRecurringJobs` config flag, when `true`, parks every job on a yearly Jan-1 cron (`0 0 1 1 *`) instead of its real cadence — so they stay visible in the `/hangfire` dashboard for manual **Trigger now** without auto-firing during local dev/E2E.

### Ports and abstractions

- All external boundaries (persistence, HTTP, blob, email, Discord) cross via Domain ports. Naming: `I*Repository`, `I*Client`, `I*Accessor`.
- Shared ports live in `ScoreTracker.Domain.SecondaryPorts/`; vertical-internal ports live inside the vertical.
- EF-backed implementations: `EF<Port>` in `ScoreTracker.Data.Repositories/` or the owning vertical's `Infrastructure/` (e.g. `EFUserRepository : IUserRepository`). **One implementation per port.**
- Non-EF integrations: concrete types in `ScoreTracker.Data.Clients/` and `ScoreTracker.Data.Apis/`.
- DI wiring: `CompositionRoot.RegistrationExtensions.AddInfrastructure` reflects over `ScoreTracker.Data` types and binds every `Domain.SecondaryPorts.*` interface they implement as **transient by default**. `IBotClient` is the only singleton. Adding a port = no DI wiring needed if the implementation lives in `Data` and follows naming. Other lifetimes require an explicit registration. Vertical services register through their `AddXxx()` hooks.
- Cross-cutting Web-bound ports (`HttpContextUserAccessor : ICurrentUserAccessor`, `DateTimeOffsetAccessor : IDateTimeOffsetAccessor`) live in `ScoreTracker.Web.Accessors/` because they depend on ASP.NET.

### Domain models

- `sealed record` is the default for entities and value-shaped types.
- Most models are intentionally lean (e.g. `User`, `Song`, `Chart` are property bags). Rich behavior is acceptable when invariants demand it — `TournamentSession` is the reference example (`Approve`, `AddPhoto`, `RemovePhoto`, `SetVerificationType` enforce the approval flow).
- **Value-type construction**: immutable structs/records with `static <Type> From(<input>)` factories that throw a domain exception on invalid input. Implicit conversions from primitives may delegate to `From`. Reference: [Name.cs](ScoreTracker/ScoreTracker.SharedKernel/ValueTypes/Name.cs). Bypassing `From` is a violation.
- Domain exceptions live in `Domain/Exceptions/` and inherit a domain base (e.g. `InvalidNameException`).
- **MediatR is deliberately permitted in `ScoreTracker.Domain`** (with `Logging.Abstractions`); any other outside dependency in Domain is a violation.
- **"Saga" is a feature-grouped class, not a state machine** — one MassTransit `IConsumer<>` plus related MediatR handlers for a feature. Never a `MassTransitStateMachine`.

### One DbContext

- `ChartAttemptDbContext` is the only `DbContext`. Adding a second context requires explicit discussion.
- Entities owned by an unextracted slice get `DbSet`s on the context. **Entities owned by an extracted vertical live in the vertical** (internal, `<Vertical>/Infrastructure/Entities/`) and are registered via its `IDbModelContribution` in `Wiring/`; the vertical's repositories use `Set<TEntity>()`. Every vertical's contribution must be listed in `CompositionRoot.VerticalModelContributions.All()` — omitting one makes scaffolded migrations silently drop that vertical's tables.
- Repositories take `IDbContextFactory<ChartAttemptDbContext>` and create scoped contexts.
- Migrations live in `ScoreTracker.Data/Migrations/`. Scaffold from `ScoreTracker.Data` with `dotnet ef migrations add <Name> --startup-project ../ScoreTracker.CompositionRoot` (the design-time factory lives in CompositionRoot so the model includes vertical contributions). **Application**: production applies migrations via the self-contained EF bundle in the deploy pipeline's gated stage; local dev auto-migrates at startup through the AppHost's `AutoMigrate` flag. The app never migrates at startup in production — it only logs pending-migration drift.
- New tables get a row in [docs/DATABASE-SCHEMA.md](docs/DATABASE-SCHEMA.md).

## UI conventions

The machine-enforceable subset of [docs/UX-GUIDELINES.md](docs/UX-GUIDELINES.md):

- **No color literals in `Pages/`, `Components/`, or `Shared/`** — no hex strings, no MudBlazor `Colors.*` constants (arch-test enforced, `UiColorTokenTests`, shrink-only allowlist). A brand color belongs in a `MixPalette` (`Services/Theming/MixThemes.cs`); a color that carries data meaning belongs in a semantic token group with a `ThemeScales` accessor. Markup reads `var(--mix-*/--rarity-*/--diff-*/--plate-*/--slot-*)`.
- **Theme resolution**: `/Account` override → selected mix → Phoenix; dark-only (both Mud palettes deliberately identical). `MixThemes` is the single source: it builds the `MudTheme` and emits the CSS custom properties.
- **Two semantic ramps, fixed meaning, per-mix hues**: rarity (grey → silver → emerald → gold → sapphire → prism; never red; glow classes carry the monotonic ordering) and difficulty (`TierListCategory` green→red). Consumers call `ThemeScales.RarityStyle/BandFor/DifficultyColor/PlateColor/SlotColor` — never re-implement band cutoffs. Legacy difficulty chips (`--slot-*`, pre-Exceed slots/HDB/levelled co-ops) render as CSS, never image bubbles, and never borrow the difficulty ramp — old scales don't translate ([docs/design/legacy-mixes.md](docs/design/legacy-mixes.md)).
- **One concept, one component**: `DifficultyBubble`, `LetterGradeIcon`, `ScoreBreakdown`, `UserLabel`. New visual concept = new shared component in `Components/`.
- **Density**: the Comfortable/Compact/Table preference persists **per page** via `Density__<Page>` UiSettings keys (landed with the tier-list overhaul; `Universal__Density` was retired unshipped). The three sanctioned modes are fixed — never invent a fourth.
- **Localization**: every UI string goes through `L[…]`; new keys land in **all** locales in the same pass, following each `docs/LOCALIZATION-<locale>.md` glossary (Mix: ko `시리즈`, ja `バージョン`, es/pt `versión`/`versão`, fr/it `Mix`).

## Test conventions

See [docs/HOW-TO-TEST.md](docs/HOW-TO-TEST.md) for the philosophy; these are the agent-facing specifics.

### Frameworks and doubles

- **xUnit `2.9.3` + Moq `4.20.72`.** Do not introduce alternative double libraries (`FakeItEasy`, `NSubstitute`, `AutoFixture`) without explicit approval.
- Unit and component tests live in `ScoreTracker.Tests/`, mirroring source by namespace. Real-dependency integration tests live in `ScoreTracker.Tests.Integration/` (Testcontainers.MsSql + Respawn).
- **Double vocabulary**: Dummy / Stub / Fake / Spy / Mock / Simulator — talk about the *role*, not the library type. A `Mock<T>` set up only with `ReturnsAsync` and never `Verify`'d is a *stub*; a `Mock<T>` whose `Verify` calls are the assertion is a *mock*; `FakeDateTime.At(...)` returns a stub even though it's named "fake."

### Test categorization

**Folder-as-tag**: each test project's path (and subfolders within) encodes the classification. Per-test `[Trait]` attributes are not used and not required when adding a test to an existing folder.

| Folder | Layer | Size | Dependency mode |
|---|---|---|---|
| `ScoreTracker.Tests/DomainTests/` | Unit | Small | None (rare test double for clock/RNG seams) |
| `ScoreTracker.Tests/ApplicationTests/` | Component | Small | Test doubles (mocked ports) |
| `ScoreTracker.Tests/ArchitectureTests/` | Unit (ratchets — rules are added, never removed) | Small | None |
| `ScoreTracker.Tests.Api/` | Approval (API wire shape) | Small | Test doubles |
| `ScoreTracker.Tests.Components/` | Component (Blazor rendering/behavior, bUnit) | Small | Test doubles (mocked services + data) |
| `ScoreTracker.Tests.Integration/` | Integration | Medium | Ephemeral infra (SQL Server via Testcontainers). `LiveSite/` + `DiscordCanary/` subfolders are env-gated smoke tests that skip when unconfigured; the Discord canary is manual-run only |
| `ScoreTracker.Tests.E2E/` | E2E (critical workflows) | Large | Ephemeral infra (SQL Server, Kestrel-hosted app, WireMock PIU stub, headless Chromium) |
| `ScoreTracker.Tests.Exploration/` | Exploration (interactive iteration, snapshots) | — (never in CI) | Machine-local inputs (photos, tessdata, real vendor endpoints read-only); skips when unconfigured |

PR gate: the fast suites (`ScoreTracker.Tests`, `ScoreTracker.Tests.Api`, `ScoreTracker.Tests.Components`) run without Docker; the integration and E2E suites need Docker. CI runs all five on every PR.

**UI test granularity ladder (owner directive 2026-07-12)**: always the lowest level that can catch the defect — domain/handler logic in `ScoreTracker.Tests`, component rendering/behavior in `ScoreTracker.Tests.Components` (bUnit, mocked data), Playwright (`Tests.E2E`) only for critical whole-workflow user journeys.

### Unit tests — `DomainTests/`

- **Subjects**: value types, domain services, pure functions, simulators, policies, edge cases.
- **Naming**: `<TypeName>Tests.cs`, one class per subject. Test method names describe behavior (`ApproveClearsNeedsApprovalAndSnapshotsVerificationType`), not implementation (`CallsRepository`). Natural-language form preferred over a strict `Given_When_Then` template.
- **Form**: xUnit `[Fact]` / `[Theory]`. Use real domain objects; mocking is the exception, not the default.
- **No external dependencies** — no Moq usage if avoidable, no DbContext, no HTTP, no time/RNG calls (use the seams below).
- Reference: [NameTests.cs](ScoreTracker/ScoreTracker.Tests/DomainTests/NameTests.cs).

### Component tests — `ApplicationTests/`

- **Subjects**: MediatR handlers and "Sagas" (feature-grouped consumer + handler classes).
- **Naming**: `<HandlerOrSaga>Tests.cs`. Test method names describe behavior, not method names.
- **Pattern** (canonical, follow verbatim):
  1. Construct a `Mock<TPort>` for each Domain port the handler depends on.
  2. Construct the real handler with `mock.Object` dependencies.
  3. Call `handler.Handle(new TCommand(...), CancellationToken.None)`.
  4. `Assert` on the returned value where applicable.
  5. `Verify` side-effect calls with `It.Is<T>(predicate)` and `Times.Once` (or appropriate).
- **Bus assertions**: mock `MassTransit.IBus` and `Verify` `Publish` calls — the publish *is* the observable behavior, so `Verify`-only assertions are appropriate there.
- Reference: [CreateUserHandlerTests.cs](ScoreTracker/ScoreTracker.Tests/ApplicationTests/CreateUserHandlerTests.cs).

### Test data

- **Builders** live in `ScoreTracker.Tests/TestData/` as `internal sealed class <Type>Builder` with `WithX(...)` methods returning `this`, a terminal `Build()`, and (optionally) an implicit conversion operator to the built type. Sensible defaults for every field. Reference: [UserBuilder.cs](ScoreTracker/ScoreTracker.Tests/TestData/UserBuilder.cs). Add a builder when you find yourself constructing the same type by hand in two tests.
- **Clock seam**: `IDateTimeOffsetAccessor` (Domain port). In tests, use `FakeDateTime.At(<DateTimeOffset>)` from `TestHelpers/`. **Never call `DateTime.Now`, `DateTimeOffset.Now`, or `*.UtcNow` from Application code or tests.**
- **Randomness seam**: `IRandomNumberGenerator` (Domain port). Inject and mock. Do not call `Random.Shared` or `new Random()` from Application code.
- **IDs**: no formal ID seam yet. Handlers may use `Guid.NewGuid()` only inside Domain factories (e.g. `User(...)`); direct calls in handlers are forbidden by convention. If a handler needs new IDs, introduce an `IGuidGenerator`-style port first.

### Coverage exclusions

- Mark new commands, queries, events, records, exceptions, view projections, and enum-helper static classes with `[ExcludeFromCodeCoverage]`. `GlobalUsings.cs` already exposes `System.Diagnostics.CodeAnalysis`.
- Excluded folders: `Application/Commands`, `Application/Queries`, `Application/Events`, `Application/Messages`, `Domain/Records`, `Domain/Events`, `Domain/Views`, `Domain/Exceptions`, `Domain/Enums` (helpers only), vertical `Contracts/` records.
- **Never** exclude code in `Domain/Models`, `Domain/Services`, `Domain/ValueTypes`, or `Application/Handlers` — that's where coverage matters.

### Dependency realism

- **MassTransit in-memory transport is the production transport.** Tests that go through `IBus`/`IConsumer<>` exercise the real transport, not a fake. Do not call a test `Integration` just because it touches the bus.
- **SQL Server is never replaced** with EF in-memory or SQLite — substitutes can't validate migrations, provider-specific SQL, indexes, constraints, or concurrency, and must not be called real-DB integration tests. Tests in `ScoreTracker.Tests/` mock the repository port; tests in `ScoreTracker.Tests.Integration/` run against a real SQL Server engine provisioned by Testcontainers.MsSql, with Respawn resetting the `scores` schema between tests.

## Known divergences

Deliberate or transitional — read before flagging a violation.

- **`ScoreTracker.Data` references `ScoreTracker.Application`.** Onion-direction divergence. Slated for removal — do not lean on it for new code.
- **`Randomizer` references `Application`** transitionally (`GetRandomChartsQuery` lives there because MatchSaga sends it; RandomizerSaga is its handler), unpinned when the deprecated Match subsystem is deleted (gated on an owner announcement). Relocated from `Catalog` at the randomizer overhaul — Catalog is clean.
- **No domain-vs-integration-event distinction.** Single bounded context with an in-memory transport makes the distinction moot today. Revisit if a second bounded context appears.
