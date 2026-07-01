# Architecture

> Last verified against commit `9aa78a8` on 2026-04-25. If you change structural patterns, update this file in the same PR.

## Overview

Pump It Up Score Tracker is a Blazor Server web app (with MVC API controllers) for tracking Pump It Up scores, leaderboards, tournaments, and player progression. It follows an onion architecture: a pure `Domain` core, a `MediatR`-based `Application` layer, an EF Core `Infrastructure` layer, and a Blazor/MVC `Presentation` layer wired through a `CompositionRoot`. Asynchronous work is dispatched over MassTransit on an in-memory transport.

This file describes the code **as it is**. The working domain-boundary map feeding the planned rearchitecture is [CONTEXTS.md](CONTEXTS.md); product direction and audiences are in [PRODUCT.md](PRODUCT.md).

## Solution layout

```
ScoreTracker.sln
├── Core (solution folder)
│   ├── ScoreTracker.SharedKernel    — PIU Game Model: value types, enums, Chart/Song,
│   │                                  scoring engine, IQuery marker (rearch P2)
│   ├── ScoreTracker.Domain          — entities, ports, domain services
│   ├── ScoreTracker.Application     — MediatR handlers, MassTransit consumers
│   ├── ScoreTracker.PersonalProgress— vertical-slice experiment (player rating logic)
│   ├── ScoreTracker.Catalog         — Game Content Catalog vertical (growing): chart
│   │                                  randomizer today; content writes/skills/videos
│   │                                  follow (ADR-001 Q2/Q4/Q5)
│   ├── ScoreTracker.ChartIntelligence— Chart Intelligence vertical: tier lists,
│   │                                  scoring/letter difficulties, difficulty +
│   │                                  preference + co-op votes
│   ├── ScoreTracker.OfficialMirror  — Official Game Mirror vertical: the PiuGame ACL
│   │                                  (parsers + typed HttpClient), leaderboard/avatar
│   │                                  mirror, world rankings, score import saga
│   ├── ScoreTracker.ScoreLedger     — Score Ledger vertical: Phoenix + XX best
│   │                                  attempts, score event journal, IScoreReader
│   │                                  impl, score batch/publish pipeline
│   └── ScoreTracker.Ucs             — first subdomain vertical (rearch P5 template):
│                                      public Contracts/ + Wiring/, internal
│                                      Domain/Application/Infrastructure
├── Infrastructure (solution folder)
│   └── ScoreTracker.Data            — EF Core, repositories, external API clients
└── Presentation (solution folder)
    ├── ScoreTracker.Web             — Blazor Server pages + MVC API controllers
    ├── ScoreTracker.CompositionRoot — DI extension that wires Infrastructure
    └── ScoreTracker.Tests           — xUnit tests
```

All projects target `net10.0` with nullable + implicit usings enabled.

### Project reference graph

```
SharedKernel ◄── Domain ◄──── Application ◄──── Data ────┐
                    ▲              ▲                ▲    │
                    │              │                │    │
                    └── PersonalProgress ◄──────────┘    │
                                   ▲                     │
                                   │                     ▼
                                   └──────── Web ◄── CompositionRoot
```

- `SharedKernel` references nothing (MediatR carve-out only). Holds the PIU Game Model: all value types, enums, the value-type exceptions, `Chart`/`Song`, `LifebarSimulator`, the `ScoringConfiguration` engine, and the `IQuery<T>` marker. **Types keep their original `ScoreTracker.Domain.*` namespaces until the P6 teardown** — assembly home and namespace are deliberately decoupled during the rearch to avoid churn.
- `Domain` references `SharedKernel` only.
- `Application` references `Domain` and `PersonalProgress`.
- `PersonalProgress` references `Domain` only.
- `Data` references `Application` and `Domain`. **The Application reference is a known divergence — see [Known divergences](#known-divergences-and-tech-debt).**
- `CompositionRoot` references `Application`, `Data`, and every vertical (currently `Ucs`).
- `Web` references `CompositionRoot`, `PersonalProgress`, and every vertical (currently `Ucs`).
- `Tests` references `Application`, `Data`, and every vertical (currently `Ucs`).
- **Vertical assemblies** (rearch P5; template: `ScoreTracker.Ucs`; also extracted: `ScoreTracker.ScoreLedger`) reference `Domain` (shared ports, contract events) and — transitionally, until Data dissolves at P6 — `Data` (the shared `ChartAttemptDbContext`). `ScoreLedger` additionally references `Application` transitionally: `UpdatePhoenixBestAttemptCommand` and `GetPhoenixRecordsQuery` are sent by Application sagas, so those records stay in `Application` (and the Ledger implements their handlers) until the OfficialMirror extraction. Within a vertical only the `Contracts/` namespace (commands, queries, DTO records) and `Wiring/` (the `AddXxx()` DI extension + `IDbModelContribution`) are public; the internal `Domain/Application/Infrastructure` layers are compiler-enforced (`internal` + `InternalsVisibleTo` for the test projects and Moq's `DynamicProxyGenAssembly2`). **MassTransit's `AddConsumers` assembly scan skips internal types** (MediatR's scan does not) — a vertical with bus consumers exposes an `AddXxxConsumers(IRegistrationConfigurator)` hook in `Wiring/` that `Program.cs` calls inside `AddMassTransit`. Ratchets: `VerticalBoundaryTests` (public-surface + MediatR/MassTransit discovery tripwires), `MessageTaxonomyTests` (scans vertical contracts). `Application` must not reference verticals (it would cycle through `Data → Application`), which is why `UcsLeaderboardPlacedEvent` stays in `Domain/Events` until its consumer (`CommunitySaga`) extracts.

## Layers

### Core — `ScoreTracker.Domain`

- **Responsibility** — Domain entities, value types, enums, exceptions, secondary ports (interfaces), and pure domain services.
- **Key types / namespaces**
  - `ScoreTracker.Domain.Models.*` — aggregates and entities (e.g. [`User`](ScoreTracker/ScoreTracker.Domain/Models/User.cs), [`Chart`](ScoreTracker/ScoreTracker.Domain/Models/Chart.cs), [`Song`](ScoreTracker/ScoreTracker.Domain/Models/Song.cs), [`TournamentSession`](ScoreTracker/ScoreTracker.Domain/Models/TournamentSession.cs)).
  - `ScoreTracker.Domain.ValueTypes.*` — strongly-typed primitives (e.g. [`Name`](ScoreTracker/ScoreTracker.Domain/ValueTypes/Name.cs), [`PhoenixScore`](ScoreTracker/ScoreTracker.Domain/ValueTypes/PhoenixScore.cs), [`DifficultyLevel`](ScoreTracker/ScoreTracker.Domain/ValueTypes/DifficultyLevel.cs), [`Bpm`](ScoreTracker/ScoreTracker.Domain/ValueTypes/Bpm.cs)).
  - `ScoreTracker.Domain.Records.*` — read-shaped records used as query results (e.g. `PlayerStatsRecord`, `LeaderboardRecord`).
  - `ScoreTracker.Domain.Enums.*` — domain enums (`MixEnum`, `ChartType`, `PhoenixLetterGrade`, etc.).
  - `ScoreTracker.Domain.SecondaryPorts.*` — repository and client interfaces (`IChartRepository`, `IUserRepository`, `IBotClient`, `ICurrentUserAccessor`, `IDateTimeOffsetAccessor`, …). All persistence and integration is invoked through these.
  - `ScoreTracker.Domain.Services.*` — domain services with `Contracts` for interfaces (`IUserAccessService`, `IWorldRankingService`).
  - `ScoreTracker.Domain.Events.*` — past-tense fact records published over MassTransit. (Imperative bus *trigger* messages live in `ScoreTracker.Application.Messages` — see Eventing.)
  - `ScoreTracker.Domain.Exceptions.*` — domain exceptions (`InvalidNameException`, `ChartNotFoundException`, …).
  - `ScoreTracker.Domain.Views.*` — projection types used by the Match feature.
- **Dependencies** — `MediatR` and `Microsoft.Extensions.Logging.Abstractions` only, plus the `ScoreTracker.SharedKernel` project. No EF, no MassTransit (the abstractions live one layer up), no ASP.NET. Note: the `ValueTypes`, `Enums`, value-type exceptions, and `Chart`/`Song`/`LifebarSimulator`/`ScoringConfiguration` now *compile in SharedKernel* while keeping their `ScoreTracker.Domain.*` namespaces.
- **Conventions**
  - Value types are immutable structs/records with static `From(...)` factories that throw a domain exception on invalid input.
  - Ports use the `I*Repository`, `I*Client`, `I*Accessor` naming.
  - Records under `Records/` are flat, read-only DTO-style shapes used for query results (not entities).

### Application — `ScoreTracker.Application` (+ `ScoreTracker.PersonalProgress`)

- **Responsibility** — Orchestrate domain operations in response to MediatR requests and MassTransit messages. Holds command/query types and the handlers that fulfil them.
- **Key types / namespaces**
  - `ScoreTracker.Application.Commands.*` — `IRequest`/`IRequest<T>` records (e.g. [`CreateUserCommand`](ScoreTracker/ScoreTracker.Application/Commands/CreateUserCommand.cs), `UpdatePhoenixBestAttemptCommand`).
  - `ScoreTracker.Application.Queries.*` — `IQuery<T>` records (`GetChartsQuery`, `GetTierListQuery`, …; `IQuery<T> : IRequest<T>` is the SharedKernel marker distinguishing reads from commands — see CLAUDE.md *Message taxonomy*).
  - `ScoreTracker.Application.Handlers.*` — `IRequestHandler<,>` and `IConsumer<>` implementations. Two shapes:
    - Single-purpose handlers: `CreateUserHandler`, `GetChartHandler`, …
    - "Saga" classes: a single class implementing one `IConsumer<TEvent>` plus one or more `IRequestHandler` for related queries (e.g. [`BountySaga`](ScoreTracker/ScoreTracker.Application/Handlers/BountySaga.cs), `TierListSaga`, `MatchSaga`). These are not MassTransit `MassTransitStateMachine` sagas — they are plain handler classes grouped by feature.
  - `ScoreTracker.Application.Events.MatchUpdatedEvent` — example of a `MediatR.INotification` (in-process pub/sub); contrast with Domain events that travel over MassTransit.
  - `ScoreTracker.PersonalProgress.PlayerRatingSaga` — vertical-slice experiment containing one consumer + a few handlers for player-rating math.
- **Dependencies**
  - `Domain` (entities, ports).
  - `PersonalProgress` (Application also references this sibling project).
  - `MediatR`, `MassTransit.Abstractions`, `Microsoft.Extensions.Caching.Memory`. **Does not** reference EF Core, ASP.NET, or `Microsoft.Extensions.DependencyInjection` — handlers are pure C# classes constructed via DI.
- **Conventions**
  - Commands and queries are `sealed record`s.
  - Handlers are `sealed class`es with constructor-injected ports.
  - "Saga" suffix denotes a feature-grouped handler class (consumer + related requests sharing dependencies). It does not imply state-machine semantics.
  - Side-effect dispatch from a handler is via injected `IBus.Publish` (MassTransit) for cross-feature events; in-process notifications use `IMediator.Publish` with `INotification`.

### Infrastructure — `ScoreTracker.Data`

- **Responsibility** — Concrete implementations of Domain ports: EF Core repositories, HTTP API clients, blob storage, email, and the Discord bot.
- **Key types / namespaces**
  - `ScoreTracker.Data.Persistence.ChartAttemptDbContext` — single `DbContext` with ~60 `DbSet`s. Connection string from `SqlConfiguration`, SQL Server provider.
  - `ScoreTracker.Data.Persistence.ChartDbContextFactory` — `IDbContextFactory<ChartAttemptDbContext>` so repositories can create scoped contexts.
  - `ScoreTracker.Data.Persistence.Entities.*` — EF entity types (separate from Domain models).
  - `ScoreTracker.Data.Repositories.EF*Repository` — implementations of `ScoreTracker.Domain.SecondaryPorts.I*Repository` (e.g. [`EFUserRepository`](ScoreTracker/ScoreTracker.Data/Repositories/EFUserRepository.cs), `EFChartRepository`).
  - `ScoreTracker.Data.Migrations.*` — 165 EF migrations from 2022-04 onward, plus `ChartAttemptDbContextModelSnapshot`.
  - `ScoreTracker.Data.Apis.PiuGameApi` (+ `Apis/Contracts/IPiuGameApi`, `Apis/Dtos/*`) — HTTP client for the official PIU site, registered as a typed `HttpClient`.
  - `ScoreTracker.Data.Clients.*` — `AzureBlobFileUploadClient`, `DiscordBotClient`, `OfficialSiteClient`, `PiuTrackerClient`, `SendGridAdminNotificationClient`.
  - `ScoreTracker.Data.Configuration.*` — POCOs bound from configuration (`SqlConfiguration`, `AzureBlobConfiguration`, `DiscordConfiguration`, `SendGridConfiguration`).
- **Dependencies** — EF Core SqlServer, Azure.Storage.Blobs, Discord.Net, HtmlAgilityPack, SendGrid, MediatR. Project references: `Domain`, **`Application` (divergence)**.
- **Conventions**
  - Repository class name is `EF<Port>` and implements one Domain port interface.
  - Repositories take `IDbContextFactory<ChartAttemptDbContext>` and create their own context per repository instance.
  - Some repositories also take `IMemoryCache` for read-side caching.
  - All migrations live in `ScoreTracker.Data/Migrations/` and are applied manually (no startup `Migrate()` call observed).

### Presentation — `ScoreTracker.Web`

- **Responsibility** — Blazor Server UI, MVC API controllers, authentication, hosted background services, and process bootstrap.
- **Key types / namespaces**
  - [`Program.cs`](ScoreTracker/ScoreTracker/Program.cs) — single composition root for the process. Configures Razor Pages + Server-Side Blazor, MassTransit (in-memory + delayed scheduler), Hangfire (SQL Server storage + dashboard + recurring-job registrations), MediatR (multi-assembly scan), authentication (Discord/Google/Facebook OAuth + cookie + custom `ApiToken` scheme), MudBlazor, Application Insights, Swagger, localization, and calls `AddInfrastructure(...)` from `CompositionRoot`.
  - `ScoreTracker.Web.Pages.*` — Blazor pages, organized by feature folders (`Admin/`, `Communities/`, `Competition/`, `Experiments/`, `OfficialLeaderboards/`, `Progress/`, `TierLists/`, `Tools/`). Pages dispatch via injected `IMediator`.
  - `ScoreTracker.Web.Components.*` — Blazor components reused across pages.
  - `ScoreTracker.Web.Controllers.Api.*` — MVC controllers under `api/*`. Controllers dispatch via `IMediator` and use `[ApiToken]` for auth.
  - [`ScoreTracker.Web.HostedServices.RecurringJobRunner`](ScoreTracker/ScoreTracker/HostedServices/RecurringJobRunner.cs) — thin `IBus`-only runner whose methods are the entry points Hangfire serializes. One method per recurring job, each a one-line `_bus.Publish(new XEvent())`. Hangfire registrations live in `Program.cs` via `RecurringJob.AddOrUpdate<RecurringJobRunner>(id, r => r.Method(), cron)`. Cron expressions are UTC.
  - `ScoreTracker.Web.HostedServices.BotHostedService` — Discord bot lifecycle.
  - `ScoreTracker.Web.Accessors.*` — `HttpContextUserAccessor : ICurrentUserAccessor`, `DateTimeOffsetAccessor : IDateTimeOffsetAccessor`. Concrete implementations of Domain ports that depend on ASP.NET and so live here.
  - `ScoreTracker.Web.Security.*` — `ApiTokenAttribute`, `ApiTokenAuthenticationScheme`, `ScoreTrackerClaimTypes`, [`HangfireDashboardAuthorization`](ScoreTracker/ScoreTracker/Security/HangfireDashboardAuthorization.cs) (`IDashboardAuthorizationFilter` gating `/hangfire` on `User.IsAdmin`).
  - `ScoreTracker.Web.Services.*` — `PhoenixScoreFileExtractor` (Tesseract OCR), `UiSettingsAccessor`, `ChartVideoDisplayer`.
- **Dependencies** — MudBlazor, Tesseract, MassTransit DI, Hangfire (AspNetCore + SqlServer), OAuth providers, Swashbuckle. Project references: `CompositionRoot`, `PersonalProgress`.
- **Conventions**
  - Razor pages dispatch via `IMediator` injected with `[Inject]`. They do not call repositories or `DbContext` directly.
  - `Controllers/Api/` for HTTP API; route prefix `api/<feature>`; `[ApiToken]` + `[EnableCors("API")]`.
  - Configuration POCOs are bound in `Program.cs` via `Configuration.GetSection(...).Get<T>()` and `Services.Configure<T>(...)`.

## Cross-cutting concerns

- **Authentication** — Cookie-based `DefaultAuthentication` (30-day sliding) plus three OAuth providers (Discord, Google, Facebook) and a custom `ApiToken` scheme for API requests. Configured in `Program.cs`.
- **Authorization** — Single policy named after `ApiTokenAttribute` (`ApiTokenAttribute.AuthPolicy`).
- **Logging** — `Microsoft.Extensions.Logging` injected as `ILogger<T>`. No custom logging infrastructure.
- **Telemetry** — Blazor Application Insights via `AddBlazorApplicationInsights()`.
- **Localization** — `AddLocalization` with `Resources/` path; supported cultures `en-US, pt-BR, ko-KR, en-ZW, es-MX, fr-FR, ja-JP, it-IT`; default `en-US`. A scoped `IStringLocalizer<App>` is injected globally as `L` via [_Imports.razor](ScoreTracker/ScoreTracker/_Imports.razor); strings are resolved with `L["..."]` in Razor markup or `L.GetString("...")` from code. Resource keys use **English UI text as the key verbatim** (e.g. `L["Add to Favorites"]`, `L["Recorded Date"]`). Format strings keep their literal English in the key and use positional placeholders in the value (e.g. key `"You are X place!"` → value `"You are {0} Place!"`, called as `L["You are X place!", placeText]`). Title case in the English source is preserved in the key. Missing keys in non-en resx files fall back to the key string itself, so en-US.resx is the complete superset. **When adding new keys, populate every locale resx file in the same pass** — `en-US`, `en-ZW`, `es-MX`, `fr-FR`, `it-IT`, `ja-JP`, `ko-KR`, `pt-BR`. Each non-English locale has a glossary at the repo root (`LOCALIZATION-<locale>.md`, e.g. [LOCALIZATION-it-IT.md](LOCALIZATION-it-IT.md)) that captures style conventions and established term mappings; follow that file's conventions and reuse its term mappings rather than inventing new ones. For `en-ZW` (Murloc-speak), there's no glossary — match the creative pattern of existing values in `App.en-ZW.resx`. **Skip prose paragraphs that contain inline markup** (e.g. a `<MudText>` body with embedded `<MudLink>` elements interrupting the sentence). Splitting such prose into fragment keys produces poor translations across languages with different word order, and embedding HTML in resx values is not a pattern this codebase has adopted. Leave these hardcoded English; the maintainer handles them manually.
- **Caching** — `IMemoryCache` injected directly into select repositories (e.g. `EFUserRepository`). No global cache pipeline.
- **Ambient services** — `ICurrentUserAccessor` (Domain port → `HttpContextUserAccessor` in Web), `IDateTimeOffsetAccessor` (Domain port → `DateTimeOffsetAccessor` in Web).
- **MediatR pipeline** — None. No `IPipelineBehavior` implementations exist.
- **Validation** — None as a pipeline. Value types validate at construction (e.g. `Name.From`); commands/queries do not run through a validator.

## Eventing

- **Library** — MassTransit `8.5.7` (`MassTransit.Abstractions` in Application, full `MassTransit` in `PersonalProgress`). Web uses `MassTransit.Extensions.DependencyInjection 7.3.1` — a version skew, see tech debt.
- **Transport** — In-memory (`UsingInMemory`). Configured in [Program.cs:56-69](ScoreTracker/ScoreTracker/Program.cs:56). Delayed message scheduler is enabled via `AddDelayedMessageScheduler` + `UseDelayedMessageScheduler`.
- **Consumer registration** — `o.AddConsumers(typeof(PlayerRatingSaga).Assembly, typeof(TierListSaga).Assembly, typeof(RecurringJobRunner).Assembly)` scans `PersonalProgress`, `Application`, and `Web` for `IConsumer<>` implementations.
- **Events** (past-tense facts; records in `ScoreTracker.Domain.Events`)
  - **Fat contract events** (rearch P3; envelope `EventId`/`OccurredAt`/`SchemaVersion`, primitives-only payloads that double as partner webhook bodies): `PlayerScoresUpdatedEvent`, `ScoreImportCompletedEvent`. Dual-published alongside their thin predecessors (`PlayerScoreUpdatedEvent`, `RecentScoreImportedEvent`) until the last consumer migrates; the Ledger's read contract is `IScoreReader` (consumers must not use `IPhoenixRecordRepository`).
  - Score/import flow: `PlayerScoreUpdatedEvent`, `RecentScoreImportedEvent`, `PlayerRatingsImprovedEvent`, `PlayerStatsUpdatedEvent`, `ChartDifficultyUpdatedEvent`, `ImportStatusUpdatedEvent`, `ImportStatusErrorEvent`, `NewTitlesAcquiredEvent`, `TitlesDetectedEvent`, `UcsLeaderboardPlacedEvent`, `UserCreatedEvent`, `UserUpdatedEvent`, `UserWeeklyChartsProgressedEvent`.
  - Application-internal MediatR notification: `ScoreTracker.Application.Events.MatchUpdatedEvent` (`INotification`).
- **Trigger messages** (imperative bus commands; records in `ScoreTracker.Application.Messages`) — published by `RecurringJobRunner` / admin pages to kick off work: `RotateWeeklyChartsCommand`, `RecalculateScoringDifficultyCommand`, `RecalculateChartLetterDifficultiesCommand`, `StartLeaderboardImportCommand`, `FlushOverdueScoreBatchesCommand`, `ProcessScoresTiersListCommand`, `ProcessPassTierListCommand`. They are requests, not facts — the consumer owns the decision (e.g. `RotateWeeklyChartsCommand` exits early when the current week hasn't expired).
- **Publishers** — Razor pages (e.g. `UploadPhoenixScores.razor`, `Admin.razor`), Hangfire recurring jobs via `RecurringJobRunner`, and many handlers (`CreateUserHandler`, `UpdatePhoenixRecordHandler`, `UpdateUserHandler`, etc.) inject `IBus` and call `Publish`.
- **Consumers** — The "Saga" classes in `Application/Handlers` and `PersonalProgress/PlayerRatingSaga`.
- **Operational implication** — Because the transport is in-memory, **in-flight bus messages do not survive a process restart**. Recurring schedules *do* survive restarts because they live in the Hangfire SQL Server store, not the bus — Hangfire re-fires according to its `MisfireHandlingMode` (default: schedule the next occurrence). Any ad-hoc `IBus.Publish` work that was mid-flight at restart is still lost.

## Data access

- **Provider** — EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer 10.0.2`). SQL Server.
- **Context** — Single [`ChartAttemptDbContext`](ScoreTracker/ScoreTracker.Data/Persistence/ChartAttemptDbContext.cs) in `ScoreTracker.Data.Persistence`, with ~60 `DbSet`s.
- **Factory** — [`ChartDbContextFactory`](ScoreTracker/ScoreTracker.Data/Persistence/DbContextFactory.cs) implements `IDbContextFactory<ChartAttemptDbContext>` and is registered transient in `CompositionRoot`. The DbContext itself is registered with `AddDbContext`.
- **Abstractions** — Repositories (`I*Repository`) are defined in `ScoreTracker.Domain.SecondaryPorts` and implemented as `EF*Repository` in `ScoreTracker.Data.Repositories`. Application code never touches `DbContext` directly (verified — no EF references in `ScoreTracker.Application`).
- **Registration** — [`RegistrationExtensions.AddInfrastructure`](ScoreTracker/ScoreTracker.CompositionRoot/RegistrationExtensions.cs) reflects over `ScoreTracker.Data` types and binds every `Domain.SecondaryPorts.*` interface they implement as transient. `IBotClient` is registered as a singleton instead. `IPiuGameApi` is registered as a typed `HttpClient`.
- **Migrations** — `ScoreTracker.Data/Migrations/` (oldest 2022-04). Applied manually (no `db.Database.Migrate()` at startup). Scaffold from `ScoreTracker.Data` with **`dotnet ef migrations add <Name> --startup-project ../ScoreTracker.CompositionRoot`** — the design-time factory lives in CompositionRoot because the model must include every vertical's contribution (see next bullet); scaffolding from Data alone would silently generate `DropTable`s for vertical-owned entities.
- **Vertical entity ownership** — verticals own their EF entities (internal classes in `<Vertical>/Infrastructure/Entities/`) and register them with the single context via `IDbModelContribution` ([IDbModelContribution.cs](ScoreTracker/ScoreTracker.Data/Persistence/IDbModelContribution.cs)): the vertical's `Wiring/` contribution pins its table names (previously derived from the deleted `DbSet` property names) and its `AddXxx()` extension registers it in DI; the context applies all contributions at the end of `OnModelCreating`. Vertical repositories use `Set<TEntity>()` instead of `DbSet` properties. [`VerticalModelContributions.All()`](ScoreTracker/ScoreTracker.CompositionRoot/VerticalModelContributions.cs) is the canonical list — the design-time factory and the integration-test fixture both consume it. The context factory is registered with `AddDbContextFactory` (not pooled — pooling requires an options-only constructor and the context takes the contribution set).
- **Score event journal** — `scores.ScoreEventJournal` (rearch P4, ADR-001 Q8) is an append-only log of score submissions *as received* — raw submitted values, including ones that don't beat the stored best. Written by `UpdatePhoenixRecordHandler` through the Ledger-internal `IScoreJournalRepository.Append`; read by consumers through `IScoreReader.GetScoreHistory(userId, chartId)`. Seeded 2026-06 from the Phoenix best-attempt table (`scores.PhoenixRecord`; `Source = 'backfill'`). Rows are never updated or deleted; this is the foundation of score-progression history and the candidate source of truth if the Ledger is event-sourced later.
- **Hangfire schema** — Hangfire owns its own `HangFire` schema in the same SQL Server database. Tables are auto-created on first boot (`PrepareSchemaIfNecessary = true`); no EF migration manages them. Do not add EF entities for Hangfire's tables.

## Testing strategy

- **Framework** — xUnit `2.9.3` with `Moq 4.20.72` for test doubles.
- **Layout** — `ScoreTracker.Tests` mirrors `src` by namespace. Today: `DomainTests/` for value-type tests (`NameTests`, `DifficultyLevelTests`), `ApplicationTests/` for handler tests (`CreateUserHandlerTests`).
- **Coverage focus** — Domain value types and a seed handler test demonstrating the Moq mocking pattern (mock the Domain ports, instantiate the handler, `Verify` the side effects). Wider Application/Infrastructure coverage is still to come.
- **Project reference** — `Tests` references `Application`, which transitively pulls `Domain` and `PersonalProgress`. It does not reference `Data` or `Web`.
- **Mocking convention** — Mock the Domain port interfaces (`IUserRepository`, `IBus`, etc.), construct the real handler with `mock.Object` dependencies, and `Verify` calls with `It.Is<T>(...)` predicates. Do not introduce alternative double libraries (`FakeItEasy`, `NSubstitute`, `AutoFixture`) without explicit approval.
- **Test helpers** — Reusable scaffolding lives in `ScoreTracker.Tests/TestHelpers/` (`FakeDateTime.At(...)` returns a configured `Mock<IDateTimeOffsetAccessor>`) and `ScoreTracker.Tests/TestData/` (`UserBuilder`, `ChartBuilder` — fluent builders with sensible defaults). Prefer these over hand-rolled doubles in new tests.
- **Coverage exclusions** — Pure data shapes (commands, queries, events, records, view projections), exception classes, and enum-helper static classes are marked with `[ExcludeFromCodeCoverage]` so coverage % reflects logic, not DTO surface area. Each project has a `GlobalUsings.cs` that exposes `System.Diagnostics.CodeAnalysis` so the attribute requires no per-file `using`. **When adding a new command/query/event/record/exception, mark it `[ExcludeFromCodeCoverage]`.** Excluded folders: `Application/Commands`, `Application/Queries`, `Application/Events`, `Application/Messages`, `Domain/Records`, `Domain/Events`, `Domain/Views`, `Domain/Exceptions`, `Domain/Enums` (helper classes only — enums themselves can't take the attribute), `PersonalProgress/Queries`. Real logic in `Domain/Models`, `Domain/Services`, `Application/Handlers`, and `Domain/ValueTypes` is *not* excluded — that's where coverage is meaningful.

## Conventions and rules

- **Domain has no outward dependencies.** No project references; only MediatR + Logging.Abstractions NuGets.
- **All persistence and integration goes through ports** defined in `ScoreTracker.Domain.SecondaryPorts`. Application code never references `DbContext`, `HttpClient`, blob storage, etc. directly.
- **Application handlers do not reference EF Core or ASP.NET.** Confirmed by grep.
- **Razor pages and API controllers dispatch via `IMediator`.** No direct repository access from Presentation.
- **One DbContext** (`ChartAttemptDbContext`). Add `DbSet`s here; do not introduce a second context without discussion.
- **Repository naming**: implementation is `EF<Port>` (e.g. `EFUserRepository` implements `IUserRepository`). One implementation per port.
- **Value types validate at construction** via static `From(...)` factories that throw a domain exception. Do not bypass them.
- **Configuration POCOs** live in `ScoreTracker.Data.Configuration` (or `ScoreTracker.Web.Configuration` for Web-only options) and are bound in `Program.cs`.
- **Recurring background work** is scheduled via Hangfire (`RecurringJob.AddOrUpdate<RecurringJobRunner>(...)` in `Program.cs`) which fans out to MassTransit by publishing the matching `Application/Messages/` trigger record. Cron expressions are UTC. Do not introduce a second scheduler library.
- **Async work that should not block the originating request** is published over `IBus` — past-tense facts from `Domain/Events/`, imperative triggers from `Application/Messages/`.

## Known divergences and tech debt

- **`ScoreTracker.Data` references `ScoreTracker.Application`.** `ScoreTracker/ScoreTracker.Data/ScoreTracker.Data.csproj` line 24. This points outward through the onion. **To be removed** — Infrastructure should depend only on Domain.
- **`ScoreTracker.PersonalProgress` is a parallel Application-layer assembly.** Acceptable as an experimental vertical-slice split, but **do not introduce additional vertical-slice projects without explicit approval**. `Application` referencing `PersonalProgress` makes them effectively co-equal.
- **MassTransit version skew.** `Web` uses `MassTransit.Extensions.DependencyInjection 7.3.1`; the rest uses `MassTransit 8.5.7`. Consolidate on the v8 DI extensions.
- ~~**Domain `Events/` folder mixes events and command-shaped messages.**~~ **Resolved 2026-06-12** (rearch C6+C7): trigger messages moved to `Application/Messages/` with honest imperative names; `Domain/Events/` holds only past-tense facts.
- **No MediatR pipeline behaviors and no validation pipeline.** Validation lives only in value-type constructors. Adding a behavior pipeline (logging, validation) is a future option, not a current rule.
- **No automatic migration on startup.** `Database.Migrate()` is not called; migrations must be applied out-of-band.

## Glossary

For PIU and project domain terms (Mix, Song, Chart, Phoenix score, Pumbility, Tier list, Weekly Charts, Community Leaderboards, UCS, Saga, etc.), see [DOMAIN.md](DOMAIN.md).

Architecture-internal terms used in this document:

- **Secondary port** — Outbound interface defined in `ScoreTracker.Domain.SecondaryPorts` and implemented in `ScoreTracker.Data` (or, for ASP.NET-bound concerns, in `ScoreTracker.Web`).
- **CompositionRoot** — `ScoreTracker.CompositionRoot.RegistrationExtensions.AddInfrastructure`. The DI extension that wires Infrastructure implementations to Domain ports.

## Open questions

- Should `IDateTimeOffsetAccessor` and `ICurrentUserAccessor` implementations move out of `ScoreTracker.Web` once a non-Blazor host appears, or are they Web-bound by intent?
- `EFUserRepository` constructs both a long-lived `ChartAttemptDbContext` (via `factory.CreateDbContext()`) and stores the factory itself for ad-hoc creation. Is the long-lived context intentional given the repository is registered transient, or a vestige to clean up?
- The reflective registration in `AddInfrastructure` binds *every* `Domain.SecondaryPorts` interface a `Data` type implements as transient. Are any ports expected to be scoped/singleton (besides `IBotClient`, which is hard-coded singleton)?
