# Architecture

Two sections: the **philosophy** (why the code is shaped this way) and the **code map** (where things are). Domain terms (Mix, Chart, Phoenix score, Pumbility, UCS, …) are defined in [DOMAIN.md](DOMAIN.md).

---

## 1. Architecture Philosophy

### Verticals split by bounded context

The system is decomposed into **vertical slices, one per bounded context** of the Pump It Up scoring domain. Each vertical is its own assembly owning its full stack — domain logic, application handlers, EF entities and repositories — with a deliberately small public surface:

- **`Contracts/`** — the only types other assemblies may consume: commands, queries, DTO records, and the events the vertical publishes.
- **`Wiring/`** — the DI hook (`AddXxx()`), the bus-consumer hook (`AddXxxConsumers(...)`), and the vertical's database-model contribution.
- **Everything else is `internal`**, compiler-enforced. EF entities and internal domain types never cross the boundary.

Cross-vertical communication happens two ways, and only two ways:

1. **Contracts** — one vertical sends another's published commands/queries via MediatR, or consumes its published events off the bus.
2. **Published ports** — read interfaces (e.g. `IScoreReader`, `IPlayerStatsReader`) for high-traffic reads.

**Never SQL joins onto another vertical's tables.** A vertical's tables are private storage, not an integration surface. This is what keeps a vertical extractable: its data model can change shape without a ripple, because nothing else touches it below the contract line.

The verticals: **ScoreLedger** (the system of record for scores), **PlayerProgress** (ratings, titles, history), **ChartIntelligence** (tier lists, difficulty analytics), **Catalog** (game content reads, videos, skills), **Randomizer** (chart draw generation, randomizer settings, tournament draws), **OfficialMirror** (the anti-corruption layer against the official PiuGame site), **WeeklyChallenge**, **EventCompetition** (tournaments), **Communities**, **Ucs** (user-created steps), **Identity** (accounts, logins, tokens), and **HomePage** (dashboard layout persistence — pages and widget instances; the widget *render components* live in Web's registry, see [docs/design/HomePageWidgets/README.md](design/HomePageWidgets/README.md)).

### Onion (dependency direction)

Within and across layers, dependencies point **inward, toward the domain**:

```
SharedKernel ◄── Domain ◄── Application ◄── Data ◄── verticals ◄── Web ◄── CompositionRoot
```

- **SharedKernel** holds the PIU game model — value types (`PhoenixScore`, `DifficultyLevel`, …), enums, `Chart`/`Song`, the scoring engine. It references nothing.
- **Domain** holds entities, domain services, and the *ports* (interfaces) everything outside must implement. No EF, no HTTP, no vendor SDKs.
- **Application** orchestrates use cases via MediatR handlers. It knows the domain and the ports — never that it's behind a web server or in front of SQL Server.
- **Infrastructure** (`Data`, and each vertical's internal `Infrastructure/`) implements the ports: EF repositories, HTTP clients, blob/email/Discord adapters.
- **Presentation** (`Web`) renders UI and translates HTTP to MediatR dispatches. It contains no business logic and touches no repository directly.
- **CompositionRoot** wires it all together — the only place that knows every concrete type.

Business rules live in the center and have no idea what database, web framework, or vendor happens to surround them today.

### Hexagonal (ports & adapters)

Every external boundary is crossed through a **port defined in the domain** (`I*Repository`, `I*Client`, `I*Accessor` in `Domain.SecondaryPorts`) and implemented by an **adapter in infrastructure** (`EFUserRepository`, `AzureBlobFileUploadClient`, `DiscordBotClient`, …). One implementation per port; DI binds them by reflection in the CompositionRoot. Swapping SQL Server, blob storage, or the email provider is an adapter change, not a domain change. The same seam is what makes handlers testable — component tests mock ports, nothing else.

### DDD, pragmatically

- **Value types validate at construction**: immutable structs/records with `static From(...)` factories that throw domain exceptions on invalid input. There is no such thing as an invalid `PhoenixScore` in flight.
- **Rich models where invariants demand it** (e.g. `TournamentSession` enforces its approval workflow); **lean property-bag records** where they don't (`User`, `Song`, `Chart`). Rigor is spent where rules are dense.
- **Message taxonomy is explicit**: *queries* (`IQuery<T>`, read-only, never on the bus), *commands* (imperative requests — MediatR for in-process, plain records on the bus for triggers), *events* (past-tense facts on the bus). Folder + name + interface tell you which is which, and architecture tests enforce it.
- **A "Saga" here is a feature-grouped handler class** (one bus consumer plus related request handlers sharing dependencies) — not a state machine.

### Dispatch, eventing, and scheduling

- **Synchronous use cases**: UI/API → `IMediator` → handler.
- **Asynchronous side effects**: handlers publish to MassTransit (`IBus`); consumers in the owning vertical react. The transport is in-memory — fast, but mid-flight messages die with the process, so consumers are idempotent and anything that must re-fire is scheduled. When a downstream consumer needs another consumer's output, ordering comes from pipeline shape, not from racing: the score-batch pipeline has ONE progression-side consumer (`HighlightCaptureSaga`, the session-snapshot orchestrator), which computes highlight flags and folder lamps, runs the rating and title steps in-process via MediatR — each failure-isolated — and only then publishes the enriched `ScoreHighlightsCapturedEvent` that the one Discord session-snapshot card renders from. Cross-vertical enrichment goes through published contracts (the card reads weekly-board placements via `GetUserWeeklyPlacementsQuery`), never by pulling another vertical's internals into the chain.
- **Recurring work**: Hangfire (SQL-persisted, restart-safe) fires one-line publishers; the real work happens in bus consumers. See [SCHEDULED-JOBS.md](SCHEDULED-JOBS.md).

### Enforcement over convention

The rules above are **ratcheted by architecture tests** (`ScoreTracker.Tests/ArchitectureTests/`): layer dependency rules, vertical public-surface checks, MediatR/MassTransit discovery tripwires, message-taxonomy scans. Rules are added, never removed. If you break the philosophy, the build tells you before a reviewer does. The machine-readable conventions (per-layer package allowlists, test patterns) live in [CLAUDE.md](../CLAUDE.md).

---

## 2. Code Map

### Solution layout

```
ScoreTracker.sln
├── Core
│   ├── ScoreTracker.SharedKernel      PIU game model: value types, enums, Chart/Song,
│   │                                  scoring engine, IQuery marker
│   ├── ScoreTracker.Domain            entities, secondary ports, domain services, events
│   ├── ScoreTracker.Application       MediatR handlers + bus trigger messages (shrinking:
│   │                                  most logic now lives in verticals)
│   ├── ScoreTracker.ScoreLedger       score system of record: Phoenix/XX best attempts,
│   │                                  append-only ScoreEventJournal, IScoreReader
│   ├── ScoreTracker.PlayerProgress    ratings, titles, player history, recommendations
│   ├── ScoreTracker.ChartIntelligence tier lists, scoring/letter difficulties, votes
│   ├── ScoreTracker.Catalog           chart/song reads, videos, skills
│   ├── ScoreTracker.Randomizer        chart draw generation + randomizer settings
│   ├── ScoreTracker.OfficialMirror    PiuGame ACL: scraping, leaderboard mirror,
│   │                                  world rankings, score import saga
│   ├── ScoreTracker.WeeklyChallenge   weekly board rotation, entries, placements
│   ├── ScoreTracker.EventCompetition  tournaments, sessions, qualifiers, co-op teams
│   ├── ScoreTracker.Communities       communities, memberships, Discord channel feeds
│   ├── ScoreTracker.Ucs               user-created steps + leaderboards
│   ├── ScoreTracker.HomePage          dashboard layout persistence: pages + widget
│   │                                  instances (widget UI lives in Web's registry)
│   └── ScoreTracker.Identity          accounts, external logins, api tokens, settings
├── Infrastructure
│   └── ScoreTracker.Data              shared DbContext, unextracted repositories,
│                                      external clients (blob, Discord, SendGrid, PiuGame)
└── Presentation
    ├── ScoreTracker (Web)             Blazor Server UI + MVC API controllers
    ├── ScoreTracker.CompositionRoot   DI wiring, vertical model contributions,
    │                                  design-time EF factory, migration startup
    ├── ScoreTracker.AppHost           Aspire local-dev orchestration
    ├── ScoreTracker.ServiceDefaults   OTel/resilience defaults
    ├── ScoreTracker.Tests             unit + component + architecture tests
    ├── ScoreTracker.Tests.Api         API wire-shape approval tests
    ├── ScoreTracker.Tests.Integration real-DB tests (Testcontainers + Respawn)
    └── ScoreTracker.Tests.E2E         Playwright critical-workflow tests (Kestrel-hosted
                                       app + WireMock PIU stub + Testcontainers SQL)
```

### Inside a vertical

`ScoreTracker.Ucs` is the template. Every vertical follows the same internal shape:

```
ScoreTracker.<Vertical>/
├── Contracts/          public: Commands/, Queries/, Events/, DTO records
├── Wiring/             public: AddXxx() DI extension, AddXxxConsumers() bus hook,
│                       IDbModelContribution (pins its tables on the shared context)
├── Domain/             internal: models, vertical-local rules
├── Application/        internal: handlers + sagas (bus consumers)
└── Infrastructure/     internal: EF entities + repositories (use Set<TEntity>())
```

Every vertical's model contribution must be listed in [`VerticalModelContributions.All()`](../ScoreTracker/ScoreTracker.CompositionRoot/VerticalModelContributions.cs) — the design-time factory and the integration-test fixture both consume it; omitting one silently drops that vertical's tables from scaffolded migrations.

### The web app (`ScoreTracker/ScoreTracker/`)

**Pages** (`Pages/`, grouped by feature — all dispatch via `IMediator`, never repositories):

| Folder | What's there |
|---|---|
| *(root)* | `/` (the **front door** for logged-out visitors, the **widget dashboard** for signed-in — one route, split server-side by the `FrontDoor` dispatcher; `/Home` is an alias; see [front-door.md](design/front-door.md) + [HomePageWidgets/README.md](design/HomePageWidgets/README.md)), `/Charts` (the core browser), `/Chart/{id}` (record + detail), `/Login` (the front door; a signed-in visitor is bounced home), `/Welcome`, `/Account` (profile, API tokens), `/UploadPhoenixScores` (bulk CSV import), `/UploadXXScores` |
| `TierLists/` | `/TierLists` (+ `/TierLists/{type}/{level}`, the consolidated tier-list page — the site's most-used feature) and `/TierLists/{type}/{level}/Breakdown` (the Personalized Breakdown: what goes into your personalized blend and which charts it moves — see [docs/design/personalized-breakdown.md](design/personalized-breakdown.md)) |
| `Progress/` | `/Progress`, `/Phoenix/Progress`, `/Pumbility`, `/Titles`, `/CompetitiveLevel`, `/Player/{id}/Sessions` (public session roundups + score journal — the Discord score card's link target), `/Player/{id}/PhoenixRecap` (the season-recap slide deck, admin-computed — see [docs/design/phoenix-season-recap.md](design/phoenix-season-recap.md)) |
| `Competition/` | `/Tournaments`, stamina + match tournament flows, qualifiers submission, `/WeeklyCharts`, `/UcsLeaderboards`, `/ScoreRankings`, `/Completion` |
| `Communities/` | `/Communities`, invite links, community leaderboards |
| `OfficialLeaderboards/` | mirrored official leaderboards, player compare, `/PlayerRankings` |
| `Tools/` | calculators (`/LifeCalculator`, `/PhoenixCalculator`, rating/conversion), `/ChartRandomizer`, `/ChartCompare`, `/StepArtists` |
| `Experiments/` | stats playground: `/GameStats`, score distributions, letter-difficulty data |
| `Admin/` | admin dashboard, chart maintenance, bulk voting |
| `Dev/` | `/Dev/Populate` — the local-database setup harness (dev-only, see [HOW-TO-RUN.md](HOW-TO-RUN.md)) |

**Components** (`Components/`): the reusable vocabulary of the UI — `ChartSelector` (autocomplete), `DifficultyBubble`, `SongImage`, `LetterGradeIcon`, `ScoreBreakdown`, `UserLabel`, `TierListSection`, `ChartVideoDisplay`, etc.

**The shell** (`Pages/Shared/_SiteLayout.cshtml` + `Components/Shell/`) is **server-rendered HTML on every page**, not a Blazor component ([static-shell.md](design/static-shell.md)): the top nav and its mega-menus, the mobile bottom nav and More sheet, the mix pill, and the theme tokens all render before any circuit exists, so a crawler sees the nav and the page paints without waiting on a websocket. `ShellModelFactory` builds its model from the request — the one place the anonymous mix cookie is readable, since a circuit cannot see the request — and hands the mix to the app as a root parameter. Menus are vanilla (`wwwroot/js/nav.js`); native `<details>` carries the mix picker's disclosure. Two things stay interactive as islands: the app-bar chart search and the import pulse dot.

Three rules follow, and they bind anything the SSR migration touches next:
- **A static region is `--mix-*`-only.** Every `--mud-*` custom property is emitted by `MudThemeProvider` *inside* the circuit, and MudBlazor's own `body` rule reads them — so a `var(--mud-…)` in the shell paints unthemed until the circuit arrives.
- **MudBlazor's providers mount as the first root** (`Components/MudProviders.razor`), not in `MainLayout`. There is one popover provider per circuit and roots initialise in document order, so a provider in the layout — the last root — is behind every island that might ask it for a popover.
- **`MainLayout` keeps only what an app root owns**: the legacy-mix gate, the page dock, the recap pointer, and `MudLayout` — which is MudBlazor's *drawer host*, not chrome, and stays until the last `MudDrawer` goes.

**Controllers** (`Controllers/`): the [API surface](API.md) — thin MediatR dispatchers under `api/*`, the dev-harness exports under `dev/export/*`, and UI-support endpoints (`login`, `logout`, `culture`, sitemap).

**Login flow**: `/Login/{Provider}` issues the OAuth challenge → `/Login/{Provider}/Callback` maps the external identity to a user (`GetUserByExternalLoginQuery`, creating via `CreateUserCommand` + `CreateExternalLoginCommand` on first sign-in) → claims principal built with custom claims (`ScoreTrackerClaimTypes`: game tag, country, profile image, `ClaimsIssuedAt` for cache-invalidated sign-out) → 30-day sliding cookie (`DefaultAuthentication`). The OAuth handshake itself lands in a short-lived `ExternalAuthentication` cookie (never the session cookie), which is what lets a signed-in user link additional providers from `/Account` via `/Login/{Provider}/Link` — sign-in methods are many-to-one with accounts (see [docs/design/login-overhaul.md](design/login-overhaul.md)). PIUGAME is a credential-based provider, not OAuth: `/PiuGameLogin` posts the credentials to `/Login/PiuGame`, which authenticates against piugame itself (OfficialMirror's `GetPiuGameAccountIdentityQuery` — passwords ride `RedactedString` and are never stored or logged) and resolves the account by any stored alias (`Identity.ResolveExternalUserCommand`). API callers use the separate `ApiToken` Basic-auth scheme. Locally, a `DevAuth`-gated backdoor (`/Login/Dev`, `/Login/Dev/Bootstrap`) skips OAuth entirely and lands on `/Dev/Populate` when the database is empty. The pages behind `/` and `/Login` are the **front door** — a real (non-Blazor) Razor Page that renders full HTML with no SignalR circuit so crawlers and link unfurlers see real content ([front-door.md](design/front-door.md)); a signed-in visitor to either route is dispatched to the home dashboard instead.

**Localization**: `IStringLocalizer<App>` injected globally as `L`; keys are English UI text verbatim; nine locales, each non-English one with a translation glossary alongside this doc (`LOCALIZATION-<locale>.md`). New keys get populated in every locale in the same pass. New locales bootstrap through the volunteer intake form: [LOCALIZATION-INTAKE-TEMPLATE.md](LOCALIZATION-INTAKE-TEMPLATE.md).

**Accessors** (`Accessors/`): Web-bound implementations of domain ports that need ASP.NET (`HttpContextUserAccessor : ICurrentUserAccessor`, `DateTimeOffsetAccessor : IDateTimeOffsetAccessor`).

### Data access

One SQL Server database, one `DbContext`, table-by-table breakdown in [DATABASE-SCHEMA.md](DATABASE-SCHEMA.md). Repositories take `IDbContextFactory<ChartAttemptDbContext>` and create scoped contexts. Migrations: scaffold from `ScoreTracker.Data` with `--startup-project ../ScoreTracker.CompositionRoot` (the design-time factory includes every vertical's contribution); production applies them via the deploy-pipeline EF bundle, local dev auto-migrates through the AppHost.

### Composition

[`Program.cs`](../ScoreTracker/ScoreTracker/Program.cs) is the single bootstrap: authentication, MediatR scans, MassTransit + every vertical's `AddXxxConsumers` hook, Hangfire + recurring-job registrations, localization, Swagger, and the CompositionRoot's `AddInfrastructure(...)` (reflection-binds every `Domain.SecondaryPorts` interface to its `Data` implementation, transient by default; `IBotClient` is the lone singleton).
