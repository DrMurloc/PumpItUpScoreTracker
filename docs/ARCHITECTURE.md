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
2. **Published ports** — read interfaces (e.g. `IScoreReader`, `IPlayerStatsReader`, `IPlayerVisibilityReader`) for high-traffic reads. This is also the escape hatch when a consumer would otherwise close a reference cycle: the Discord broadcast feeds fan out to channel subscriptions that Communities owns, but OfficialMirror can't reference Communities (`OfficialMirror → ScoreLedger → Communities`), so it reads the subscriptions through the published `IDiscordFeedReader` port instead. The same shape carries a live piugame session out of an import: the sid exists only inside OfficialMirror and only during a scrape, the entitlement to receive it lives in CommunityTools, so it crosses through the Domain port `ISessionDeliveryClient`. A **shared domain service** solves the same problem when the thing in the middle is logic rather than a boundary: the personalized Score tier list and the PUMBILITY page both need one answer to "what would this player score here", and ChartIntelligence cannot reference PlayerProgress (`PlayerProgress → ChartIntelligence` already exists), so `IScoreProjector` lives in `Domain/Services/` where both can see it — the same reason `TierListProcessor` does.

**Never SQL joins onto another vertical's tables.** A vertical's tables are private storage, not an integration surface. This is what keeps a vertical extractable: its data model can change shape without a ripple, because nothing else touches it below the contract line.

The verticals: **ScoreLedger** (the system of record for scores), **PlayerProgress** (ratings, titles, history), **ChartIntelligence** (tier lists, difficulty analytics), **Catalog** (game content reads, videos, chart identity — the piucenter badge vocabulary, folder baselines and the chips built on them, plus the official avatar catalog behind manual avatar selection), **Randomizer** (chart draw generation, randomizer settings, tournament draws), **OfficialMirror** (the anti-corruption layer against the official PiuGame site), **WeeklyChallenge**, **EventCompetition** (tournaments), **Communities**, **CommunityTools** (registered partner tools, player sharing, API keys, webhook delivery),
**ChartComments** (comments and personal notes on a chart, their votes, and the plain-text parser
that autolinks a URL and decides whether its host is trusted — and, since the step-chart comments slice, an optional
second of the chart a comment points at, which the strip draws as a mark and reads in a sticky panel; see
[docs/design/step-chart-comments/README.md](design/step-chart-comments/README.md)), **Rivals** (the rival graph — a directed edge onto a site player or a board tag, plus the blocks and invite codes that gate it; see [rivals.md](design/rivals.md) — and, until a Peers vertical exists, the home of the two peer-shaped Domain ports: `IPlayerVisibilityReader` (who may look at whom) and `IPeerStandingReader` (where your score stands among the peers you chose, see [peers-abstraction.md](design/peers-abstraction.md))), **Identity** (accounts, logins, tokens), and **HomePage** (dashboard layout persistence — pages and widget instances; the widget *render components* live in Web's registry, see [docs/design/HomePageWidgets/README.md](design/HomePageWidgets/README.md)).

**Translations** is a real vertical since the chart-comments feature's translation slice: it owns the batch pipeline that renders community text across locales — prompts and glossary, the queue and spend-ledger tables, the nightly submit / hourly collect jobs, and the rolling dollar ceiling. Its contract is deliberately generic: `QueueTextForTranslationCommand(sourceKey, text)` in, `TextTranslatedEvent` / `TextTranslationFailedEvent` out, with the source key opaque — ChartComments is the first consumer, and community descriptions or tool blurbs can ride the same pipeline later. The old "nothing that ships can spend a metered token" property survives as the shipped *posture* rather than a structural fact: the batch adapter (`ILanguageModelBatchClient` → `AnthropicBatchClient` in `Data`) reports itself unconfigured and the pipeline parks unless a `ClaudeApi:ApiKey` is deliberately supplied, and the submit step checks a rolling 30-day ceiling before any batch goes out. The synchronous `ILanguageModelClient` port still has no shipping implementation — its one adapter lives in `ScoreTracker.ExplorationTests`, whose sweep exercises the same prompts via `InternalsVisibleTo`; see [docs/design/comment-translation.md](design/comment-translation.md).

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
- **Rich models where invariants demand it** (`TournamentSession` enforces its approval workflow; `Community` enforces its role/permission rules — promotion delegation, single-seat creator transfer, ban retention — so community authorization lives in the aggregate, not in handlers); **lean property-bag records** where they don't (`User`, `Song`, `Chart`). Rigor is spent where rules are dense.
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
│   ├── ScoreTracker.Catalog           chart/song reads, videos, chart identity,
│   │                                  the official avatar catalog
│   ├── ScoreTracker.Randomizer        chart draw generation + randomizer settings
│   ├── ScoreTracker.OfficialMirror    PiuGame ACL: scraping, leaderboard mirror,
│   │                                  supplemented reading, score import saga
│   ├── ScoreTracker.WeeklyChallenge   weekly board rotation, entries, placements
│   ├── ScoreTracker.EventCompetition  tournaments, sessions, qualifiers, co-op teams
│   ├── ScoreTracker.Communities       communities, memberships, Discord channel feeds
│   ├── ScoreTracker.CommunityTools    registered tools, player sharing, API keys,
│   │                                  webhook delivery
│   ├── ScoreTracker.ChartComments     comments and personal notes on a chart, votes,
│   │                                  the plain-text parser and its link-trust gate
│   ├── ScoreTracker.Rivals            the rival graph: directed edges onto site players and
│   │                                  board tags, blocks, invite codes
│   ├── ScoreTracker.HomePage          dashboard layout persistence: pages + widget
│   │                                  instances (widget UI lives in Web's registry)
│   ├── ScoreTracker.Translations      community-text translation: prompts + glossary,
│   │                                  the batch pipeline, queue + spend-ledger tables,
│   │                                  the rolling dollar ceiling (parked without a key)
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
    ├── ScoreTracker.Tests.Components  Blazor rendering/behavior tests (bUnit)
    ├── ScoreTracker.Tests.Integration real-DB tests (Testcontainers + Respawn)
    ├── ScoreTracker.Tests.E2E         Playwright critical-workflow tests (Kestrel-hosted
    │                                  app + WireMock PIU stub + Testcontainers SQL)
    └── ScoreTracker.ExplorationTests  manual-only workbench: live PIU crawl + Discord canary
                                       (never CI; read-only unless the owner asks to mutate)
```

### Inside a vertical

`ScoreTracker.WeeklyChallenge` is the template. Every vertical follows the same internal shape:

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
| *(root)* | `/` (the **front door** for logged-out visitors, the **widget dashboard** for signed-in — one route, split server-side by the `FrontDoor` dispatcher; `/Home` is an alias; see [front-door.md](design/front-door.md) + [HomePageWidgets/README.md](design/HomePageWidgets/README.md)), `/Charts` (the **chart SRP** — faceted search over the selected mix, with Community-Vote-vs-score-derived tier facets, densities, quick record, and the `/Charts/Export.csv` UI-support endpoint; a cross-mix view is deferred to its own design pass, [charts-srp.md](design/charts-srp.md) §7), `/Charts/{mix}/{song}/{difficulty}` (the **canonical chart page** — static SSR, real crawlable HTML; the `/Chart/{guid}` permalink and `/Record` 301 to it via `ChartPermalinkController`, and historical/stale slugs 301 to canonical from the page itself), `/Login` (the front door; a signed-in visitor is bounced home), `/Welcome`, `/Setup` (the **new-account step** — username, language, country, visibility and mix, between the front door and the home dashboard; every field saves on change and the mix re-themes the page live, so it carries no MudBlazor components of its own, see [new-user-setup.md](design/new-user-setup.md)), `/Account` (profile, API tokens), `/Account/Data/Undo` + `/Account/Data/Delete` (the **Your Data** pair — undo one import, withdraw the broken personal bests an earlier import recorded, delete a chosen scope, or delete the account; see [delete-my-data.md](design/delete-my-data.md)), `/UploadPhoenixScores` (bulk CSV import), `/UploadXXScores` |
| `TierLists/` | `/TierLists` (+ `/TierLists/{type}/{level}`, the consolidated tier-list page — the site's most-used feature) and `/TierLists/{type}/{level}/Breakdown` (the Personalized Breakdown: what goes into your personalized blend and which charts it moves — see [docs/design/personalized-breakdown.md](design/personalized-breakdown.md)) |
| `Progress/` | the **PUMBILITY section** — one frame carrying your number, the pool selector and the bar, over three routed pages: `/Pumbility` (**Play** — **your peers**, on both mixes: the players the projection draws from — within ±1 competitive level of you on Phoenix 1, the players whose pool of the chart type sits within 500 below and 250 above yours, each holding a full pool of it, on Phoenix 2 — and every chart they hold in their top 50, grouped by prevalence (a slot-weighted hold count) or by projected gain, each card carrying the grade you are projected to land at your Energy, your own standing, and the peers' roster beneath; the gain badge is the target list's own projection — on Phoenix 1 a weighted quantile of the band discounted by how much each peer has outgrown the score they lent, on Phoenix 2 the median of the peers, shown only where five or more have passed the chart), `/Pumbility/Breakdown` (**PUMBILITY Breakdown** — what the number is worth in titles, what it is made of — with your fifty set against your peers' average by chart type and by level inside that band — the curve, and **your top 50** as a tier list of what you hold, its rows carrying no peers' data and no projections, those being Play's; `/Pumbility/Pool` still resolves) and `/Pumbility/Phoenix1` (**Phoenix 1** — your Phoenix 1 record repriced under Phoenix 2's rules; Phoenix 2 only, so the section is two pages on Phoenix 1), see [pumbility-overhaul.md](design/pumbility-overhaul.md). `/Titles` (the **ladder rails** — one rail per title ladder rather than one row per title, see [titles-overhaul.md](design/titles-overhaul.md)), `/Player/{id}/Sessions` (the **session breakdown** — the most recent session rendered full-page, older ones a table with a View button; the Discord score card's link target, see [session-breakdown.md](design/session-breakdown.md)), `/Player/{id}/PhoenixRecap` (the season-recap slide deck, admin-computed — see [docs/design/phoenix-season-recap.md](design/phoenix-season-recap.md)), and their root **`/Player/{id}`** (the **player page** — gem + PUMBILITY + identity, the rating tiles, the Official Boards card when the account is linked, folder completion, and the head-to-head against you; who may look is `IPlayerVisibilityReader`'s answer — self, public, a shared user-created community, a rival edge — read inside the profile handler; the community leaderboard row and the rivals roster both land here, and the app-bar search's Players section does too, see [player-page-and-site-search.md](design/player-page-and-site-search.md)). The old Player Stats pages (`/Progress`, `/Phoenix/Progress`) are **retired** — their graphs are home-page widgets now ([HomePageWidgets/by-level-breakdown.md](design/HomePageWidgets/by-level-breakdown.md)) |
| `Compete/` | `/Rivals` (roster · rivals-of-you · feed, plus the head-to-head) and `/Rivals/Invite/{code}` — [rivals.md](design/rivals.md) |
| `Competition/` | the **March of Murlocs section** (`MarchOfMurlocs/` — the quarterly stamina ladder on its own `MoM*` tables, one board per mix × chart type, [march-of-murlocs.md](design/march-of-murlocs.md)): `/MarchOfMurlocs` (**Season** — static SSR: your standing, the Doubles / Singles boards ranking *sessions*, previous / next season links; `/Tournaments/MarchOfMurlocs` and `/Tournament/Stamina/{id}` 301 here), `/MarchOfMurlocs/Session/{id}` (**Session Breakdown** — the four numbers with board marks, the charts in three densities, the pace chart, Compare against this board or your own past seasons with the re-pricing split, the Download image dialog), and the Past-seasons dialog island the section frame carries on every MoM page (no route — each season's own page is the crawlable artifact); the old record and planner pages (`/Tournament/Stamina/{id}/Record`, `/Tournament/Stamina/{id}/Builder`) stay reachable behind the section's links until Slice 4b replaces them; `/WeeklyCharts`, and the qualifiers pair: `/Tournament/{id}/Qualifiers` (the **one player page** — status, your standing, the board, the pool, scoring; submitting is a dialog, and `/Qualifiers/Submit` 301s here) plus `/Tournament/{id}/Qualifiers/Admin` (the organizer screen, the only place photos are visible; `/Tournament/{id}/Admin` 301s here) — [qualifiers-overhaul.md](design/qualifiers-overhaul.md). The bracket subsystem and the `/Tournaments` directory were deleted ([deletions-wave-1](design/deletions-wave-1.md)) |
| `CommunityTools/` | `/CommunityTools` (My Tools, then Connected Tools, then the directory to browse), `/CommunityTools/Invite/{code}` (a private tool's landing page), `/Developers` (a maker with no tools gets the setup wizard — four named crumbs, no step count; afterwards the console: settings, then keys, invite links and webhooks as their own sections, then Help, then the delete), `/Developers/{id}/Console` (activity log) and `/Developers/{id}/Debug` (test delivery, replay) — [api-v2-community-tools.md](design/api-v2-community-tools.md) |
| `Communities/` | `/Communities` (directory: World card + Regions rail + player-community field), `/Community/Leaderboard` (Rankings · By Chart · Members tabs), `/Community/Members` (roles/permissions management), `/Communities/Invite/{code}` (landing + private-profile consent) — [communities-overhaul.md](design/communities-overhaul.md) |
| `OfficialLeaderboards/` | the Official Leaderboards section — five routed pages sharing the `OfficialSectionFrame` chrome: `/OfficialLeaderboards` (This Week highlights), `/OfficialLeaderboards/Rankings` (PUMBILITY/computed boards incl. the CO-OP estimate; `/PlayerRankings` aliases here), `/Players` (`?player=` deep links), `/Popularity`, `/WhatItTakes` — [official-leaderboards-overhaul.md](design/official-leaderboards-overhaul.md) |
| `Tools/` | calculators (`/LifeCalculator`, the XX conversion), `/PhoenixCalculator/{mix}` (the **Phoenix score page** — the formula, judgement costs, both mixes' letter cutoffs, and the measured population/note-count/hold-tick sections; static SSR with one vanilla-JS module for the live calculator, the plays dialog and the toggles, one self-canonical URL per mix, the bare route serving the viewer's mix — [phoenix-score-calculator.md](design/phoenix-score-calculator.md)), `/PumbilityCalculator/{mix}` (the **PUMBILITY formula page** — static SSR with one vanilla-JS module for the type toggle, the table's contour click and the quick calculator, one self-canonical URL per mix; `/RatingCalculator` 301s here — [pumbility-calculator.md](design/pumbility-calculator.md)), `/ChartRandomizer`, `/MixChanges` (+ `/MixChanges/{from}/{to}` — the mix diff; `/ChartCompare` 301s here) |
| `Admin/` | admin dashboard, chart maintenance, bulk voting, `/Admin/CommunityTools` (the tool review queue) |
| `Dev/` | `/Dev/Populate` — the local-database setup harness (dev-only, see [HOW-TO-RUN.md](HOW-TO-RUN.md)) |

**Components** (`Components/`): the reusable vocabulary of the UI — `ChartSelector` (autocomplete), `DifficultyBubble`, `SongImage`, `LetterGradeIcon`, `ScoreBreakdown`, `UserLabel`, `TierListSection`, `ChartVideoDisplay`, etc.

**The shell** (`Pages/Shared/_SiteLayout.cshtml` + `Components/Shell/`) is **server-rendered HTML on every page**, not a Blazor component ([static-shell.md](design/static-shell.md)): the top nav and its menus, the mobile bottom nav and More sheet, the mix pill, and the theme tokens all render before any circuit exists, so a crawler sees the nav and the page paints without waiting on a websocket. `ShellModelFactory` builds its model from the request — the one place the anonymous mix cookie is readable, since a circuit cannot see the request — and hands the mix to the app as a root parameter. Menus are vanilla (`wwwroot/js/nav.js`); native `<details>` carries the mix picker's disclosure. Two responsive rules live entirely in CSS so a fold or rotation needs no circuit: Tier Lists folds into Play below 1280px, and the More sheet renders as an icon grid on squarish or wide viewports and a drill-down on narrow tall ones ([static-shell.md §11](design/static-shell.md)). Three things stay interactive as shell islands: the app-bar chart search, the import pulse dot, and the render-nothing mix seed (`ShellMixSeed`) that carries the request-resolved mix into each page's circuit.

Three rules follow, and they bind anything the SSR migration touches next:
- **A static region is `--mix-*`-only.** Every `--mud-*` custom property is emitted by `MudThemeProvider` *inside* the circuit, and MudBlazor's own `body` rule reads them — so a `var(--mud-…)` in the shell paints unthemed until the circuit arrives.
- **MudBlazor's providers mount as the first root** (`Components/MudProviders.razor`), not in `MainLayout`. There is one popover provider per circuit and roots initialise in document order, so a provider in the layout — the last root — is behind every island that might ask it for a popover.
- **The router is static and every page declares its own circuit** (`@rendermode RenderModes.Interactive`, ratcheted by `RenderModeDeclarationTests`; a page converts to static SSR by deleting that line). `MainLayout` renders statically around every page: it keeps the legacy-mix gate — a real HTTP redirect now — plus two islands, `PageDockHost` (renders the page-supplied dock fragment from inside the circuit) and `RecapPointer`. `MudLayout` moved out with the drawers: each `Temporary` drawer wraps itself in its own zero-footprint container. In-app navigation is a full document load; enhanced navigation is the deliberately-parked SPA-feel opt-in (render-modes.md §7.1).

**Controllers** (`Controllers/`): the [API surface](API.md) — thin MediatR dispatchers under `api/*` (v1, frozen) and `api/v2/*`, and UI-support endpoints (`login`, `logout`, `culture`, sitemap). There is no separate dev-harness surface — `/Dev/Populate` reads `api/v2/*` with a personal token like any integrator.

**Login flow**: `/Login/{Provider}` issues the OAuth challenge → `/Login/{Provider}/Callback` maps the external identity to a user (`GetUserByExternalLoginQuery`, creating via `CreateUserCommand` + `CreateExternalLoginCommand` on first sign-in) → claims principal built with custom claims (`ScoreTrackerClaimTypes`: game tag, country, profile image, `ClaimsIssuedAt` for cache-invalidated sign-out) → 30-day sliding cookie (`DefaultAuthentication`). The OAuth handshake itself lands in a short-lived `ExternalAuthentication` cookie (never the session cookie), which is what lets a signed-in user link additional providers from `/Account` via `/Login/{Provider}/Link` — sign-in methods are many-to-one with accounts (see [docs/design/login-overhaul.md](design/login-overhaul.md)). PIUGAME is a credential-based provider, not OAuth: `/PiuGameLogin` posts the credentials to `/Login/PiuGame`, which authenticates against piugame itself (OfficialMirror's `GetPiuGameAccountIdentityQuery` — passwords ride `RedactedString` and are never stored or logged) and resolves the account by any stored alias (`Identity.ResolveExternalUserCommand`). API callers use the separate `ApiToken` Basic-auth scheme. A **brand-new account lands on `/Setup`** rather than the dashboard — every provider path does, carrying `?from=` so the username field can say which sign-in filled it in ([new-user-setup.md](design/new-user-setup.md)); a `Universal__SetupCompleted` UiSetting written on Continue means it only ever fires once, and returnUrl is deliberately dropped for new accounts. Locally, a `DevAuth`-gated backdoor (`/Login/Dev`, `/Login/Dev/Bootstrap`) skips OAuth entirely and lands on `/Dev/Populate` when the database is empty. The pages behind `/` and `/Login` are the **front door** — a real (non-Blazor) Razor Page that renders full HTML with no SignalR circuit so crawlers and link unfurlers see real content ([front-door.md](design/front-door.md)); a signed-in visitor to either route is dispatched to the home dashboard instead.

**Localization**: `IStringLocalizer<App>` injected globally as `L`; keys are English UI text verbatim; nine locales, each one other than `en-US` carrying a translation glossary alongside this doc (`LOCALIZATION-<locale>.md`) — including the `en-ZW` Murloc joke locale, whose glossary is a specification rather than a native speaker's record. New keys get populated in every locale in the same pass. New locales bootstrap through the volunteer intake form: [LOCALIZATION-INTAKE-TEMPLATE.md](LOCALIZATION-INTAKE-TEMPLATE.md). Which locale a given request renders in — query string, then the signed-in player's saved setting, then the cookie, then the browser — is [culture-resolution.md](design/culture-resolution.md).

**Accessors** (`Accessors/`): Web-bound implementations of domain ports that need ASP.NET (`HttpContextUserAccessor : ICurrentUserAccessor`, `DateTimeOffsetAccessor : IDateTimeOffsetAccessor`).

### Data access

One SQL Server database, one `DbContext`, table-by-table breakdown in [DATABASE-SCHEMA.md](DATABASE-SCHEMA.md). Repositories take `IDbContextFactory<ChartAttemptDbContext>` and create scoped contexts. Migrations: scaffold from `ScoreTracker.Data` with `--startup-project ../ScoreTracker.CompositionRoot` (the design-time factory includes every vertical's contribution); production applies them via the deploy-pipeline EF bundle, local dev auto-migrates through the AppHost.

### Composition

[`Program.cs`](../ScoreTracker/ScoreTracker/Program.cs) is the single bootstrap: authentication, MediatR scans, MassTransit + every vertical's `AddXxxConsumers` hook, Hangfire + recurring-job registrations, localization, Swagger, and the CompositionRoot's `AddInfrastructure(...)` (reflection-binds every `Domain.SecondaryPorts` interface to its `Data` implementation, transient by default; `IBotClient` is the lone singleton).

Static files are served by `MapStaticAssets()` off the build-time manifest, not `UseStaticFiles` — markup reads `@Assets["css/site.css"]` and ships a content-hashed, year-immutable URL, so a release invalidates its own CSS and JS ([TECHNOLOGIES.md](TECHNOLOGIES.md)).
