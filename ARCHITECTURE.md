# Architecture

> Last verified against commit `9aa78a8` on 2026-04-25. If you change structural patterns, update this file in the same PR.

## Overview

Pump It Up Score Tracker is a Blazor Server web app (with MVC API controllers) for tracking Pump It Up scores, leaderboards, tournaments, and player progression. It follows an onion architecture: a pure `Domain` core, a `MediatR`-based `Application` layer, an EF Core `Infrastructure` layer, and a Blazor/MVC `Presentation` layer wired through a `CompositionRoot`. Asynchronous work is dispatched over MassTransit on an in-memory transport.

## Solution layout

```
ScoreTracker.sln
├── Core (solution folder)
│   ├── ScoreTracker.Domain          — entities, value types, ports, domain services
│   ├── ScoreTracker.Application     — MediatR handlers, MassTransit consumers
│   └── ScoreTracker.PersonalProgress— vertical-slice experiment (player rating logic)
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
Domain    ◄──── Application ◄──── Data ────┐
   ▲              ▲                ▲       │
   │              │                │       │
   └── PersonalProgress ◄──────────┘       │
                  ▲                        │
                  │                        ▼
                  └────────── Web ◄── CompositionRoot
```

- `Domain` references no other project.
- `Application` references `Domain` and `PersonalProgress`.
- `PersonalProgress` references `Domain` only.
- `Data` references `Application` and `Domain`. **The Application reference is a known divergence — see [Known divergences](#known-divergences-and-tech-debt).**
- `CompositionRoot` references `Application` and `Data`.
- `Web` references `CompositionRoot` and `PersonalProgress`.
- `Tests` references `Application`.

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
  - `ScoreTracker.Domain.Events.*` — message records published over MassTransit and/or scheduled by `RecurringJobHostedService`. Note: the folder also contains command-shaped messages such as `ProcessPassTierListCommand`.
  - `ScoreTracker.Domain.Exceptions.*` — domain exceptions (`InvalidNameException`, `ChartNotFoundException`, …).
  - `ScoreTracker.Domain.Views.*` — projection types used by the Match feature.
- **Dependencies** — `MediatR` and `Microsoft.Extensions.Logging.Abstractions` only. No project references. No EF, no MassTransit (the abstractions live one layer up), no ASP.NET.
- **Conventions**
  - Value types are immutable structs/records with static `From(...)` factories that throw a domain exception on invalid input.
  - Ports use the `I*Repository`, `I*Client`, `I*Accessor` naming.
  - Records under `Records/` are flat, read-only DTO-style shapes used for query results (not entities).

### Application — `ScoreTracker.Application` (+ `ScoreTracker.PersonalProgress`)

- **Responsibility** — Orchestrate domain operations in response to MediatR requests and MassTransit messages. Holds command/query types and the handlers that fulfil them.
- **Key types / namespaces**
  - `ScoreTracker.Application.Commands.*` — `IRequest`/`IRequest<T>` records (e.g. [`CreateUserCommand`](ScoreTracker/ScoreTracker.Application/Commands/CreateUserCommand.cs), `UpdatePhoenixBestAttemptCommand`).
  - `ScoreTracker.Application.Queries.*` — `IRequest<T>` records (`GetChartsQuery`, `GetTierListQuery`, …).
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
  - [`Program.cs`](ScoreTracker/ScoreTracker/Program.cs) — single composition root for the process. Configures Razor Pages + Server-Side Blazor, MassTransit (in-memory + delayed scheduler), MediatR (multi-assembly scan), authentication (Discord/Google/Facebook OAuth + cookie + custom `ApiToken` scheme), MudBlazor, Application Insights, Swagger, localization, and calls `AddInfrastructure(...)` from `CompositionRoot`.
  - `ScoreTracker.Web.Pages.*` — Blazor pages, organized by feature folders (`Admin/`, `Communities/`, `Competition/`, `Experiments/`, `OfficialLeaderboards/`, `Progress/`, `TierLists/`, `Tools/`). Pages dispatch via injected `IMediator`.
  - `ScoreTracker.Web.Components.*` — Blazor components reused across pages.
  - `ScoreTracker.Web.Controllers.Api.*` — MVC controllers under `api/*`. Controllers dispatch via `IMediator` and use `[ApiToken]` for auth.
  - `ScoreTracker.Web.HostedServices.RecurringJobHostedService` — `IHostedService` + `IConsumer<RescheduleMessages>` that uses `IMessageScheduler.SchedulePublish` to plant the next day's recurring messages (`ProcessScoresTiersListCommand`, `UpdateBountiesEvent`, `CalculateScoringDifficultyEvent`, `UpdateWeeklyChartsEvent`, `ProcessPassTierListCommand`, `CalculateChartLetterDifficultiesEvent`) and re-schedules itself at 03:30.
  - `ScoreTracker.Web.HostedServices.BotHostedService` — Discord bot lifecycle.
  - `ScoreTracker.Web.Accessors.*` — `HttpContextUserAccessor : ICurrentUserAccessor`, `DateTimeOffsetAccessor : IDateTimeOffsetAccessor`. Concrete implementations of Domain ports that depend on ASP.NET and so live here.
  - `ScoreTracker.Web.Security.*` — `ApiTokenAttribute`, `ApiTokenAuthenticationScheme`, `ScoreTrackerClaimTypes`.
  - `ScoreTracker.Web.Services.*` — `PhoenixScoreFileExtractor` (Tesseract OCR), `UiSettingsAccessor`, `ChartVideoDisplayer`.
- **Dependencies** — MudBlazor, Tesseract, MassTransit DI, OAuth providers, Swashbuckle. Project references: `CompositionRoot`, `PersonalProgress`.
- **Conventions**
  - Razor pages dispatch via `IMediator` injected with `[Inject]`. They do not call repositories or `DbContext` directly.
  - `Controllers/Api/` for HTTP API; route prefix `api/<feature>`; `[ApiToken]` + `[EnableCors("API")]`.
  - Configuration POCOs are bound in `Program.cs` via `Configuration.GetSection(...).Get<T>()` and `Services.Configure<T>(...)`.

## Cross-cutting concerns

- **Authentication** — Cookie-based `DefaultAuthentication` (30-day sliding) plus three OAuth providers (Discord, Google, Facebook) and a custom `ApiToken` scheme for API requests. Configured in `Program.cs`.
- **Authorization** — Single policy named after `ApiTokenAttribute` (`ApiTokenAttribute.AuthPolicy`).
- **Logging** — `Microsoft.Extensions.Logging` injected as `ILogger<T>`. No custom logging infrastructure.
- **Telemetry** — Blazor Application Insights via `AddBlazorApplicationInsights()`.
- **Localization** — `AddLocalization` with `Resources/` path; supported cultures `en-US, pt-BR, ko-KR, en-ZW, es-MX, fr-FR`; default `en-US`. A scoped `IStringLocalizer<App>` is injected globally as `L` via [_Imports.razor](ScoreTracker/ScoreTracker/_Imports.razor); strings are resolved with `L["..."]` in Razor markup or `L.GetString("...")` from code. Resource keys use **English UI text as the key verbatim** (e.g. `L["Add to Favorites"]`, `L["Recorded Date"]`). Format strings keep their literal English in the key and use positional placeholders in the value (e.g. key `"You are X place!"` → value `"You are {0} Place!"`, called as `L["You are X place!", placeText]`). Title case in the English source is preserved in the key. Missing keys in non-en resx files fall back to the key string itself, so en-US.resx must remain the complete superset; other locales fill in over time. **Add new keys to `App.en-US.resx` only** when extracting strings; per-locale translations follow as a separate pass.
- **Caching** — `IMemoryCache` injected directly into select repositories (e.g. `EFUserRepository`). No global cache pipeline.
- **Ambient services** — `ICurrentUserAccessor` (Domain port → `HttpContextUserAccessor` in Web), `IDateTimeOffsetAccessor` (Domain port → `DateTimeOffsetAccessor` in Web).
- **MediatR pipeline** — None. No `IPipelineBehavior` implementations exist.
- **Validation** — None as a pipeline. Value types validate at construction (e.g. `Name.From`); commands/queries do not run through a validator.

## Eventing

- **Library** — MassTransit `8.5.7` (`MassTransit.Abstractions` in Application, full `MassTransit` in `PersonalProgress`). Web uses `MassTransit.Extensions.DependencyInjection 7.3.1` — a version skew, see tech debt.
- **Transport** — In-memory (`UsingInMemory`). Configured in [Program.cs:56-69](ScoreTracker/ScoreTracker/Program.cs:56). Delayed message scheduler is enabled via `AddDelayedMessageScheduler` + `UseDelayedMessageScheduler`.
- **Consumer registration** — `o.AddConsumers(typeof(PlayerRatingSaga).Assembly, typeof(TierListSaga).Assembly, typeof(RecurringJobHostedService).Assembly)` scans `PersonalProgress`, `Application`, and `Web` for `IConsumer<>` implementations.
- **Events** (records in `ScoreTracker.Domain.Events`)
  - Recurring/scheduled: `UpdateBountiesEvent`, `CalculateScoringDifficultyEvent`, `UpdateWeeklyChartsEvent`, `CalculateChartLetterDifficultiesEvent`, `StartLeaderboardImportEvent`.
  - Score/import flow: `PlayerScoreUpdatedEvent`, `RecentScoreImportedEvent`, `PlayerRatingsImprovedEvent`, `PlayerStatsUpdatedEvent`, `ChartDifficultyUpdatedEvent`, `ImportStatusUpdated`, `ImportStatusError`, `NewTitlesAcquiredEvent`, `TitlesDetectedEvent`, `UcsLeaderboardEntryPlacedEvent`, `UserCreatedEvent`, `UserUpdatedEvent`, `UserWeeklyChartsProgressedEvent`.
  - Command-shaped (despite living in `Events/`): `ProcessPassTierListCommand`, `ProcessScoresTiersListCommand`.
  - Application-internal MediatR notification: `ScoreTracker.Application.Events.MatchUpdatedEvent` (`INotification`).
- **Publishers** — Razor pages (e.g. `UploadPhoenixScores.razor`, `Admin.razor`), `RecurringJobHostedService`, and many handlers (`CreateUserHandler`, `UpdatePhoenixRecordHandler`, `UpdateUserHandler`, etc.) inject `IBus` and call `Publish`.
- **Consumers** — The "Saga" classes in `Application/Handlers` and `PersonalProgress/PlayerRatingSaga`.
- **Operational implication** — Because the transport is in-memory, **scheduled and in-flight messages do not survive a process restart**. `RecurringJobHostedService.StartAsync` re-publishes `RescheduleMessages` on boot to re-plant the daily schedule, but any one-off in-flight work at restart time is lost.

## Data access

- **Provider** — EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer 10.0.2`). SQL Server.
- **Context** — Single [`ChartAttemptDbContext`](ScoreTracker/ScoreTracker.Data/Persistence/ChartAttemptDbContext.cs) in `ScoreTracker.Data.Persistence`, with ~60 `DbSet`s.
- **Factory** — [`ChartDbContextFactory`](ScoreTracker/ScoreTracker.Data/Persistence/DbContextFactory.cs) implements `IDbContextFactory<ChartAttemptDbContext>` and is registered transient in `CompositionRoot`. The DbContext itself is registered with `AddDbContext`.
- **Abstractions** — Repositories (`I*Repository`) are defined in `ScoreTracker.Domain.SecondaryPorts` and implemented as `EF*Repository` in `ScoreTracker.Data.Repositories`. Application code never touches `DbContext` directly (verified — no EF references in `ScoreTracker.Application`).
- **Registration** — [`RegistrationExtensions.AddInfrastructure`](ScoreTracker/ScoreTracker.CompositionRoot/RegistrationExtensions.cs) reflects over `ScoreTracker.Data` types and binds every `Domain.SecondaryPorts.*` interface they implement as transient. `IBotClient` is registered as a singleton instead. `IPiuGameApi` is registered as a typed `HttpClient`.
- **Migrations** — `ScoreTracker.Data/Migrations/` (165 files, oldest 2022-04, newest 2025-06). Applied manually (no `db.Database.Migrate()` at startup). Standard EF `dotnet ef migrations add ...` flow.

## Testing strategy

- **Framework** — xUnit `2.9.3` with `Moq 4.20.72` for test doubles.
- **Layout** — `ScoreTracker.Tests` mirrors `src` by namespace. Today: `DomainTests/` for value-type tests (`NameTests`, `DifficultyLevelTests`), `ApplicationTests/` for handler tests (`CreateUserHandlerTests`).
- **Coverage focus** — Domain value types and a seed handler test demonstrating the Moq mocking pattern (mock the Domain ports, instantiate the handler, `Verify` the side effects). Wider Application/Infrastructure coverage is still to come.
- **Project reference** — `Tests` references `Application`, which transitively pulls `Domain` and `PersonalProgress`. It does not reference `Data` or `Web`.
- **Mocking convention** — Mock the Domain port interfaces (`IUserRepository`, `IBus`, etc.), construct the real handler with `mock.Object` dependencies, and `Verify` calls with `It.Is<T>(...)` predicates. Do not introduce alternative double libraries (`FakeItEasy`, `NSubstitute`, `AutoFixture`) without explicit approval.
- **Test helpers** — Reusable scaffolding lives in `ScoreTracker.Tests/TestHelpers/` (`FakeDateTime.At(...)` returns a configured `Mock<IDateTimeOffsetAccessor>`) and `ScoreTracker.Tests/TestData/` (`UserBuilder`, `ChartBuilder` — fluent builders with sensible defaults). Prefer these over hand-rolled doubles in new tests.
- **Coverage exclusions** — Pure data shapes (commands, queries, events, records, view projections), exception classes, and enum-helper static classes are marked with `[ExcludeFromCodeCoverage]` so coverage % reflects logic, not DTO surface area. Each project has a `GlobalUsings.cs` that exposes `System.Diagnostics.CodeAnalysis` so the attribute requires no per-file `using`. **When adding a new command/query/event/record/exception, mark it `[ExcludeFromCodeCoverage]`.** Excluded folders: `Application/Commands`, `Application/Queries`, `Application/Events`, `Domain/Records`, `Domain/Events`, `Domain/Views`, `Domain/Exceptions`, `Domain/Enums` (helper classes only — enums themselves can't take the attribute), `PersonalProgress/Queries`. Real logic in `Domain/Models`, `Domain/Services`, `Application/Handlers`, and `Domain/ValueTypes` is *not* excluded — that's where coverage is meaningful.

## Conventions and rules

- **Domain has no outward dependencies.** No project references; only MediatR + Logging.Abstractions NuGets.
- **All persistence and integration goes through ports** defined in `ScoreTracker.Domain.SecondaryPorts`. Application code never references `DbContext`, `HttpClient`, blob storage, etc. directly.
- **Application handlers do not reference EF Core or ASP.NET.** Confirmed by grep.
- **Razor pages and API controllers dispatch via `IMediator`.** No direct repository access from Presentation.
- **One DbContext** (`ChartAttemptDbContext`). Add `DbSet`s here; do not introduce a second context without discussion.
- **Repository naming**: implementation is `EF<Port>` (e.g. `EFUserRepository` implements `IUserRepository`). One implementation per port.
- **Value types validate at construction** via static `From(...)` factories that throw a domain exception. Do not bypass them.
- **Configuration POCOs** live in `ScoreTracker.Data.Configuration` (or `ScoreTracker.Web.Configuration` for Web-only options) and are bound in `Program.cs`.
- **Recurring background work** is scheduled via MassTransit's delayed scheduler from `RecurringJobHostedService`, not via a separate scheduler library.
- **Async work that should not block the originating request** is published over `IBus` with a record from `ScoreTracker.Domain.Events`.

## Known divergences and tech debt

- **`ScoreTracker.Data` references `ScoreTracker.Application`.** `ScoreTracker/ScoreTracker.Data/ScoreTracker.Data.csproj` line 24. This points outward through the onion. **To be removed** — Infrastructure should depend only on Domain.
- **`ScoreTracker.PersonalProgress` is a parallel Application-layer assembly.** Acceptable as an experimental vertical-slice split, but **do not introduce additional vertical-slice projects without explicit approval**. `Application` referencing `PersonalProgress` makes them effectively co-equal.
- **MassTransit version skew.** `Web` uses `MassTransit.Extensions.DependencyInjection 7.3.1`; the rest uses `MassTransit 8.5.7`. Consolidate on the v8 DI extensions.
- **Domain `Events/` folder mixes events and command-shaped messages** (`ProcessPassTierListCommand`, `ProcessScoresTiersListCommand`). Naming/folder placement could be tightened.
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
