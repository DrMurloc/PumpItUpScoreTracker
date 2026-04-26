# PumpItUpScoreTracker

Blazor Server + MVC API for tracking Pump It Up scores, leaderboards, tournaments, and player progression. Onion architecture: pure `Domain`, MediatR-based `Application`, EF Core `Infrastructure` (`Data`), Blazor/MVC `Presentation` (`Web`), wired through `CompositionRoot`. Async work runs on MassTransit with an in-memory transport.

## Imports

- `@ENTERPRISE.md` — **simulated org managed policy.** Stands in for the OS-level managed-policy CLAUDE.md that would auto-load in a real enterprise deployment. Owned by the (notional) org architecture team; treat it as read-only from this repo. Per-project carve-outs go in the *Carve-outs* section below, never in that file.
- `@ARCHITECTURE.md` — structural source of truth: solution layout, dependency graph, eventing, data access, glossary, known divergences, open questions.

@ENTERPRISE.md
@ARCHITECTURE.md

## Commands

Run from the repo root (the solution lives at `ScoreTracker/ScoreTracker.sln`).

- **Build**: `dotnet build ScoreTracker/ScoreTracker.sln -c Release`
- **Typecheck**: covered by `dotnet build` (no separate step).
- **Lint/static analysis**: none locally configured. DeepSource runs in CI (`.deepsource.toml`).
- **Unit tests**: `dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj` — covers `DomainTests/` (unit) and `ApplicationTests/` (component).
- **Component / module tests**: same command, same project.
- **Contract tests**: not present.
- **Real-dependency integration tests**: not present. SQL Server via Testcontainers is the intended target if/when added.
- **E2E / Playwright tests**: not present.
- **Mutation tests**: not present.

CI: Azure Pipelines (`azure-pipelines.yml`) does NuGet restore → `VSBuild` → `VSTest` on `windows-latest`.

## Architecture conventions

These are the project-local realizations of the rules in `ENTERPRISE.md`. When ENTERPRISE.md says "do X," this section says "and here X looks like *this*."

### Layer graph and per-layer package allowlist

```
Domain  ◄── Application ◄── Data
  ▲           ▲              ▲
  │           │              │
  └── PersonalProgress ◄─────┘
              ▲
              │
              └── Web ◄── CompositionRoot
```

| Project | Allowed external packages | Forbidden |
|---|---|---|
| `ScoreTracker.Domain` | `MediatR`, `Microsoft.Extensions.Logging.Abstractions` | Anything else. No EF, no `HttpClient`, no MassTransit, no ASP.NET, no Azure/Discord/SendGrid. |
| `ScoreTracker.Application` | + `MassTransit.Abstractions`, `Microsoft.Extensions.Caching.Memory` | EF Core, ASP.NET, `HttpClient`, vendor SDKs. Application must never know it's behind a web server. |
| `ScoreTracker.Data` | + `Microsoft.EntityFrameworkCore.SqlServer`, `Azure.Storage.Blobs`, `Discord.Net`, `HtmlAgilityPack`, `SendGrid` | ASP.NET. |
| `ScoreTracker.Web` | + `MudBlazor`, OAuth providers, `Swashbuckle`, `Tesseract`, MassTransit DI, `Hangfire.AspNetCore`, `Hangfire.SqlServer` | EF Core directly (must go through ports). |
| `ScoreTracker.CompositionRoot` | DI extensions only | Business logic. |
| `ScoreTracker.Tests` | + `xunit`, `Moq` | Other doubling libraries (see Test conventions). |

Adding a package outside its allowed layer is a violation. Adding a project reference that isn't in the graph above is a violation.

### Use case dispatch

- Every business operation is an `IRequest<T>` (or `IRequest`) record in `Application/Commands/` or `Application/Queries/`, paired with an `IRequestHandler<,>` in `Application/Handlers/`. Async work uses `IConsumer<TEvent>` (MassTransit) in the same `Application/Handlers/` folder.
- **Razor pages, Blazor components, and MVC controllers dispatch exclusively via `IMediator`.** No `DbContext`, repository, or `HttpClient` is injected into Web code. (Exception: `Accessors/` types in Web that *implement* Domain ports.)
- Background work is published over `IBus.Publish(...)` with a record from `Domain/Events/`. In-process notifications use `IMediator.Publish(INotification)`.
- **Recurring scheduled work uses Hangfire** with SQL Server storage (auto-created `HangFire` schema, durable across restarts). Each recurring job is a one-line `IBus.Publish(...)` on `RecurringJobRunner` ([RecurringJobRunner.cs](ScoreTracker/ScoreTracker/HostedServices/RecurringJobRunner.cs)); registrations live in [Program.cs](ScoreTracker/ScoreTracker/Program.cs) via `RecurringJob.AddOrUpdate<RecurringJobRunner>(...)`. Cron expressions are UTC. Do not introduce a second scheduler library; do not reintroduce hosted-timer / self-rescheduling-bus patterns. The Hangfire dashboard is mounted at `/hangfire` and gated on `User.IsAdmin` via [HangfireDashboardAuthorization.cs](ScoreTracker/ScoreTracker/Security/HangfireDashboardAuthorization.cs). Adding a new recurring job = add a method to `RecurringJobRunner` + one `RecurringJob.AddOrUpdate` line. The `PreventRecurringJobs` config flag, when `true`, removes the registrations instead of scheduling them.

### Ports and abstractions

- All external boundaries (persistence, HTTP, blob, email, Discord) cross via Domain ports. Naming: `I*Repository`, `I*Client`, `I*Accessor`.
- Ports live in `ScoreTracker.Domain.SecondaryPorts/`.
- EF-backed implementations: `EF<Port>` in `ScoreTracker.Data.Repositories/` (e.g. `EFUserRepository : IUserRepository`). **One implementation per port.**
- Non-EF integrations: concrete types in `ScoreTracker.Data.Clients/` and `ScoreTracker.Data.Apis/`.
- DI wiring: `CompositionRoot.RegistrationExtensions.AddInfrastructure` reflects over `ScoreTracker.Data` types and binds every `Domain.SecondaryPorts.*` interface they implement as **transient by default**. `IBotClient` is the only singleton. Adding a port = no DI wiring needed if the implementation lives in `Data` and follows naming. Other lifetimes require an explicit registration.
- Cross-cutting Web-bound ports (`HttpContextUserAccessor : ICurrentUserAccessor`, `DateTimeOffsetAccessor : IDateTimeOffsetAccessor`) live in `ScoreTracker.Web.Accessors/` because they depend on ASP.NET.

### Domain models

- `sealed record` is the default for entities and value-shaped types.
- Most models are intentionally lean (e.g. `User`, `Song`, `Chart` are property bags). Rich behavior is acceptable when invariants demand it — `TournamentSession` is the project's reference example, with methods like `Approve`, `AddPhoto`, `RemovePhoto`, `SetVerificationType` enforcing approval flow.
- **Value-type construction**: immutable structs/records with `static <Type> From(<input>)` factories that throw a domain exception on invalid input. Implicit conversions from primitives may delegate to `From`. Reference: [Name.cs](ScoreTracker/ScoreTracker.Domain/ValueTypes/Name.cs). Bypassing `From` is a violation.
- Domain exceptions live in `Domain/Exceptions/` and inherit a domain base (e.g. `InvalidNameException`).

### One DbContext

- `ChartAttemptDbContext` is the only `DbContext`. Add `DbSet`s here. Adding a second context requires explicit discussion.
- Repositories take `IDbContextFactory<ChartAttemptDbContext>` and create scoped contexts.
- Migrations live in `ScoreTracker.Data/Migrations/` and are applied **manually** — no `Database.Migrate()` at startup.

## Test conventions

### Frameworks and doubles

- **xUnit `2.9.3` + Moq `4.20.72`.** Do not introduce alternative double libraries (`FakeItEasy`, `NSubstitute`, `AutoFixture`) without explicit approval.
- All tests live in `ScoreTracker.Tests/`, mirroring source by namespace.
- **Double vocabulary follows ENTERPRISE-TESTING.md** (Dummy / Stub / Fake / Spy / Mock / Simulator) even though Moq is the only library. A `Mock<T>` set up only with `ReturnsAsync` and never `Verify`'d is a *stub*; a `Mock<T>` whose `Verify` calls are the assertion is a *mock*; `FakeDateTime.At(...)` returns a stub even though it's named "fake." Talk about the role, not the type.

### Test categorization

This repo uses **folder-as-tag**: the path encodes ENTERPRISE `Layer` / `Size` / `DependencyMode` for everything in it. Per-test `[Trait]` attributes are not used today and are not required when adding a test to an existing folder. They become the right tool only if a single folder ever mixes dependency modes (e.g., when ephemeral-DB tests land — see [BACKLOG.md](BACKLOG.md)).

| Folder | `Layer` | `Size` | `DependencyMode` |
|---|---|---|---|
| `ScoreTracker.Tests/DomainTests/` | `Unit` | `Small` | `None` (rare `TestDouble` for clock/RNG seams) |
| `ScoreTracker.Tests/ApplicationTests/` | `Component` | `Small` | `TestDouble` |

PR gate: `dotnet build` + `dotnet test` runs both folders. There is no fast/slow split today.

### Unit tests — `DomainTests/`

- **Subjects**: value types, domain services, pure functions, simulators, policies, edge cases.
- **Naming**: `<TypeName>Tests.cs`, one class per subject. Test method names describe behavior (`ApproveClearsNeedsApprovalAndSnapshotsVerificationType`), not implementation (`CallsRepository`). Natural-language form is preferred over a strict `Given_When_Then` template.
- **Form**: xUnit `[Fact]` / `[Theory]`. Use real domain objects; mocking is the exception, not the default.
- **No external dependencies** — no Moq usage if avoidable, no DbContext, no HTTP, no time/RNG calls (use the seams below).
- Reference: [NameTests.cs](ScoreTracker/ScoreTracker.Tests/DomainTests/NameTests.cs).

### Component tests — `ApplicationTests/`

- **Subjects**: MediatR handlers and "Sagas" (feature-grouped consumer + handler classes).
- **Naming**: `<HandlerOrSaga>Tests.cs`. Test method names describe behavior, not method names.
- **Pattern** (canonical, follow this verbatim):
  1. Construct a `Mock<TPort>` for each Domain port the handler depends on.
  2. Construct the real handler with `mock.Object` dependencies.
  3. Call `handler.Handle(new TCommand(...), CancellationToken.None)`.
  4. `Assert` on the returned value where applicable.
  5. `Verify` side-effect calls with `It.Is<T>(predicate)` and `Times.Once` (or appropriate).
- **Bus assertions**: mock `MassTransit.IBus` and `Verify` `Publish` calls. The publish *is* the observable behavior, so `Verify`-only assertions are appropriate here per ENTERPRISE-TESTING.md.
- Reference: [CreateUserHandlerTests.cs](ScoreTracker/ScoreTracker.Tests/ApplicationTests/CreateUserHandlerTests.cs).

### Test data

- **Builders** live in `ScoreTracker.Tests/TestData/` as `internal sealed class <Type>Builder` with `WithX(...)` methods returning `this`, a terminal `Build()`, and (optionally) an implicit conversion operator to the built type. Sensible defaults for every field. Reference: [UserBuilder.cs](ScoreTracker/ScoreTracker.Tests/TestData/UserBuilder.cs). Add a builder when you find yourself constructing the same type by hand in two tests.
- **Clock seam**: `IDateTimeOffsetAccessor` (Domain port). In tests, use `FakeDateTime.At(<DateTimeOffset>)` from `TestHelpers/` — returns a configured `Mock<IDateTimeOffsetAccessor>`. **Never call `DateTime.Now`, `DateTimeOffset.Now`, or `*.UtcNow` from Application code or tests.**
- **Randomness seam**: `IRandomNumberGenerator` (Domain port). Inject and mock. Do not call `Random.Shared` or `new Random()` from Application code.
- **IDs**: there is no formal ID seam yet. Application handlers currently allow `Guid.NewGuid()` only at construction (e.g. inside a Domain factory like `User(...)`). Direct `Guid.NewGuid()` calls in handlers are forbidden by convention; if you need new IDs in a handler, introduce an `IGuidGenerator`-style port first.

### Coverage exclusions

- Mark new commands, queries, events, records, exceptions, view projections, and enum-helper static classes with `[ExcludeFromCodeCoverage]`. `GlobalUsings.cs` already exposes `System.Diagnostics.CodeAnalysis`.
- Excluded folders: `Application/Commands`, `Application/Queries`, `Application/Events`, `Domain/Records`, `Domain/Events`, `Domain/Views`, `Domain/Exceptions`, `Domain/Enums` (helpers only), `PersonalProgress/Queries`.
- **Never** exclude code in `Domain/Models`, `Domain/Services`, `Domain/ValueTypes`, or `Application/Handlers` — that's where coverage matters.

### Dependency realism in this project

- **MassTransit in-memory transport is the production transport.** Tests that go through `IBus`/`IConsumer<>` are exercising the real transport, not a fake. Classify any future MassTransit-flow test as `Layer=Component` or `Layer=Slice` (with `DependencyMode=TestDouble`) based on what *else* is mocked. Do not call it `Integration` just because it touches the bus.
- **SQL Server is never replaced** with EF in-memory or SQLite. ENTERPRISE-TESTING.md explicitly classifies in-memory providers as `DependencyMode=InMemory`, not `EphemeralInfra`, and they cannot be called real-DB integration tests. Today, tests either mock the repository port (`DependencyMode=TestDouble`) or wait for real-dependency integration tests (`Layer=Integration, DependencyMode=EphemeralInfra`, planned in [BACKLOG.md](BACKLOG.md)).

### Local external dependencies

SQL Server is required to *run* the app but is not exercised by the current test suite. There is no `docker-compose` or scripted local bring-up; configure a connection string via user-secrets or `appsettings.Development.json`.

## Carve-outs from ENTERPRISE.md

Deliberate, documented divergences. Read these before flagging a violation.

- **MediatR is permitted in `ScoreTracker.Domain`.** ENTERPRISE.md forbids framework types in Domain; this codebase exempts `MediatR` and `Microsoft.Extensions.Logging.Abstractions`. Any other outside dependency in Domain is a violation.
- **"Saga" is a feature-grouped class, not a state machine.** A `*Saga` (e.g. `BountySaga`, `TierListSaga`, `MatchSaga`, `PlayerRatingSaga`) contains one MassTransit `IConsumer<>` plus related MediatR `IRequestHandler<>` for that feature. It is **not** a `MassTransit.MassTransitStateMachine`.
- **`Domain/Events/` mixes events and command-shaped messages** (`ProcessPassTierListCommand`, `ProcessScoresTiersListCommand`). Known divergence from ENTERPRISE's domain-events-vs-integration-events rule. Don't relocate without a planned cleanup.
- **`ScoreTracker.Data` references `ScoreTracker.Application`.** Onion-direction divergence. Slated for removal — do not lean on it for new code.
- **No domain-vs-integration-event distinction.** Single bounded context with an in-memory transport makes the distinction moot today. Revisit if a second bounded context appears.

## Architecture authority

`ARCHITECTURE.md` is the source of truth for solution layout, full dependency graph, eventing detail, data-access detail, glossary (Mix, Chart, Phoenix score, Pumbility, Tier list, Bounty, UCS, Saga), known divergences, and open questions. Update it in the same PR that changes a structural pattern.

## Backlog

[BACKLOG.md](BACKLOG.md) tracks the gaps between this codebase and `ENTERPRISE.md` that require real refactor work — real-dependency database tests, external-adapter coverage, dependency-realism labels, the `Data → Application` cleanup, etc. Items in `BACKLOG.md` are not current rules; consult it when picking up follow-on work, not when judging existing code.
