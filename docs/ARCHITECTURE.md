# Architecture

Why the code is shaped this way. Where things are is [CODE-MAP.md](CODE-MAP.md). Domain terms (Mix, Chart, Phoenix score, Pumbility, UCS, …) are defined in [DOMAIN.md](DOMAIN.md).

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

The verticals: **ScoreLedger** (the system of record for scores), **PlayerProgress** (ratings, titles, history), **ChartIntelligence** (tier lists, difficulty analytics, the weekly Hardmode census, the daily PUMBILITY presence census behind the chart page's title-by-title graph — see [chart-presence-graph.md](design/chart-presence-graph.md)), **Catalog** (game content reads, videos, chart identity — the piucenter badge vocabulary, folder baselines and the chips built on them, the channel a song sits in per mix, plus the official avatar catalog behind manual avatar selection), **Randomizer** (chart draw generation, randomizer settings, tournament draws), **OfficialMirror** (the anti-corruption layer against the official PiuGame site), **WeeklyChallenge**, **EventCompetition** (tournaments), **Communities** (also the title-to-role table for a community's Discord: one designated server, a mapping per Phoenix 2 title, and the reconcile that keeps a member's roles equal to what they have earned — see [discord-role-management.md](design/discord-role-management.md)), **CommunityTools** (registered partner tools, player sharing, API keys, webhook delivery),
**ChartComments** (comments and personal notes on a chart, their votes, and the plain-text parser
that autolinks a URL and decides whether its host is trusted — and, since the step-chart comments slice, an optional
second of the chart a comment points at, which the strip draws as a mark and reads in a sticky panel; see
[docs/design/step-chart-comments/README.md](design/step-chart-comments/README.md)), **Rivals** (the rival graph — a directed edge onto a site player or a board tag, plus the blocks and invite codes that gate it; see [rivals.md](design/rivals.md) — and, until a Peers vertical exists, the home of the two peer-shaped Domain ports: `IPlayerVisibilityReader` (who may look at whom) and `IPeerStandingReader` (where your score stands among the peers you chose, see [peers-abstraction.md](design/peers-abstraction.md))), **Seasons** (the quarterly calendar: the season row, the daily roll that opens a quarter and seals an ended one, the backfill, and the season pages to come. It owns the calendar and nothing else — the season rows themselves stay with the verticals whose tables carry them under a `SeasonId`, and `ChartSeason` is Catalog's. It references no vertical back: "which season holds this play, and is it still open" crosses outward through the Domain port `ISeasonReader` — see [seasons.md](design/seasons.md)), **Identity** (accounts, logins, tokens), and **HomePage** (dashboard layout persistence — pages and widget instances; the widget *render components* live in Web's registry, see [docs/design/HomePageWidgets/README.md](design/HomePageWidgets/README.md)).

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
- **Asynchronous side effects**: handlers publish to MassTransit (`IBus`); consumers in the owning vertical react. The transport is in-memory — fast, but mid-flight messages die with the process, so consumers are idempotent and anything that must re-fire is scheduled. When a downstream consumer needs another consumer's output, ordering comes from pipeline shape, not from racing: the score pipeline has ONE progression-side consumer (`HighlightCaptureSaga`, the session-snapshot orchestrator), which computes highlight flags and folder lamps, runs the rating and title steps in-process via MediatR — each failure-isolated — and only then publishes the enriched `ScoreHighlightsCapturedEvent` that the one Discord session-snapshot card renders from. Cross-vertical enrichment goes through published contracts (the card reads weekly-board placements via `GetUserWeeklyPlacementsQuery`), never by pulling another vertical's internals into the chain. Plays posted through `POST api/v2/players/me/plays` skip the two-minute batch: they gather into *sittings* (a gap in play time longer than the mix's window starts a new one: 15 minutes, 4 hours on RISE), and each sitting is announced once, by replaying it from the journal when the tool closes it (`POST api/v2/players/me/sittings/close`) or once its window passes with nothing arriving ([rise.md §12](design/rise.md)). An official import — plain, check or deep scan — skips the batch too: it announces once, the moment its last score is saved, carrying the titles piugame reported. Only typed entries, the v1 record API and CSV uploads ride the batch, and an upload drains it as soon as it ends ([import-restart-recovery.md §0](design/import-restart-recovery.md)).
- **Recurring work**: Hangfire (SQL-persisted, restart-safe) fires one-line publishers; the real work happens in bus consumers. See [SCHEDULED-JOBS.md](SCHEDULED-JOBS.md).

### Enforcement over convention

The rules above are **ratcheted by architecture tests** (`ScoreTracker.Tests/ArchitectureTests/`): layer dependency rules, vertical public-surface checks, MediatR/MassTransit discovery tripwires, message-taxonomy scans. Rules are added, never removed. If you break the philosophy, the build tells you before a reviewer does. The machine-readable conventions (per-layer package allowlists, test patterns) live in [CLAUDE.md](../CLAUDE.md).
