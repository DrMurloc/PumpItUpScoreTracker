# ADR-001: Subdomain verticals with contract-only boundaries

> Status: **accepted** · 2026-06-12 · Owner-approved in the 2026-06 rearchitecture design sessions.
> This is the Step 1.7 deliverable from [BACKLOG.md §rearch](../../BACKLOG.md#onion--ddd--hexagonal-rearchitecture). It finalizes the 2026-05-17 workshop decisions as revised 2026-06-11, and resolves the open questions from [CONTEXTS.md](../../CONTEXTS.md). Commit-level roadmap lives in [PR #104](https://github.com/DrMurloc/PumpItUpScoreTracker/pull/104).

## Context

The codebase is a single-process modular monolith organized as horizontal onion layers (`Domain` / `Application` / `Data` / `Web`). The domain analysis in CONTEXTS.md identified nine conceptual bounded contexts cutting across those layers, with eight concrete boundary violations (F1–F8) rooted in shared tables and thin events. The target is clean boundaries: **each context can change everything behind its contract without any other context noticing.**

## Decisions

### D1. Vertical taxonomy

One assembly per subdomain vertical, each with internal `Domain/`, `Application/`, `Infrastructure/` layers and a public `Contracts/` namespace:

`ScoreLedger` · `PlayerProgress` · `ChartIntelligence` · `Catalog` · `WeeklyChallenge` · `EventCompetition` (slim: M.o.M. + Qualifiers) · `Community` · `OfficialMirror` · `UCS` · `Identity`

Plus `ScoreTracker.SharedKernel` (not a vertical — the PIU Game Model). `ScoreTracker.Domain` and `ScoreTracker.Data` dissolve into the verticals and are deleted at the end.

### D2. Contract surface mechanics

- Within a vertical assembly, **only the `Contracts/` namespace is `public`**; everything else is `internal` (`InternalsVisibleTo` for the vertical's tests).
- Contracts contain: event records, reader interfaces, MediatR command/query records, and DTOs. Kernel types appear freely in contracts; **EF entities and internal domain models never cross**.
- Dependency rule: a vertical references `SharedKernel` + other verticals (seeing only their Contracts). `Web` references verticals (Contracts-only visibility) and dispatches via `IMediator`. `CompositionRoot` references everything and wires DI via per-vertical `AddXxx()` extensions.
- Enforcement: the compiler (via `internal`) + one architecture test per rule, added as a ratchet when each rule becomes true. Ratchets are never removed.

### D3. Communication: push facts, pull questions

> **Amended 2026-06-12 (owner directive):** the three message kinds are distinguished by folder, name, and interface — queries are `*Query` records implementing `IQuery<T>` (SharedKernel.Messaging) and never travel the bus; commands are `*Command` records (MediatR `IRequest` in `Commands/`, plain bus triggers in `Messages/`); events are past-tense `*Event` records, never `IRequest`. Enforced by `MessageTaxonomyTests` ratchets.

- **Push** — MassTransit events: past-tense facts, **fat** (carrying the producer's facts so consumers don't reach back), with an envelope (`eventId`, `occurredAt`, `source`, `schemaVersion`). Recurring-job trigger messages also live in the owning vertical's Contracts.
- **Pull** — reader interfaces (`IScoreReader`, `IPlayerStatsReader`, `ICatalogReader`, …): synchronous questions through the owner's front door, returning contract DTOs/kernel types.
- **Nothing else.** No shared tables, no cross-vertical joins, no foreign repository imports, no cross-vertical EF navigations. Consumer-local projections fed by events are the sanctioned escape hatch where a reader proves too chatty (reference example: M.o.M. snapshotting scoring levels at cycle time).
- **All contract events must round-trip JSON cleanly** (value-type converters, no object-reference semantics). Required for partner webhooks today; makes a future durable-transport swap a config change.

### D4. Data

- **One `ChartAttemptDbContext`, one database, one migration history** (A3 stands). Entities move into their owning vertical; the context applies `IEntityTypeConfiguration`s per vertical; the public `DbSet` properties are deleted (repositories use `Set<TOwnEntity>()`).
- **One writer per table.** Cross-vertical FKs are plain `Guid` columns without navigations. Arch test: no vertical references another's entity namespace.
- Per-vertical SQL schemas (`scores.*`, `intel.*`, …) are approved but optional (P7 tail).

### D5. Resolutions of CONTEXTS.md open questions

| Q | Decision |
|---|---|
| Q1 | Recommendations live **inside PlayerProgress**. Extraction of PlayerProgress waits for the in-flight WSIP feature to land. |
| Q2 | **Catalog is a small vertical** owning content writes (songs, charts, skills, videos, name mappings); the kernel keeps only types and IDs. Amends A2's kernel-and-shared-ports realization. |
| Q3 | The `ScoringConfiguration` **engine joins the SharedKernel**; presets live with their owners (M.o.M.'s in EventCompetition, Pumbility's in PlayerProgress). Amends A2's tight-kernel scope. |
| Q4 | Randomizer = `IRandomChartSelector` in **Catalog**. |
| Q5 | Chart skill tagging in **Catalog** (curation). Amends A1's PlayerProgress placement. |
| Q6 | `IPiuGameApi` is **internal to OfficialMirror**; off the shared-port list. Other verticals receive official-site facts via Mirror events/readers. Credentials and HTML never leave the Mirror. |
| Q7 | Webhooks = per-vertical event contracts + one **generic outbound delivery service** (subscriptions, signing, retries). P7. |
| Q8 | **Journal now, event sourcing later (maybe).** `ScoreEventJournal` (append-only, mix-aware, enveloped) is written alongside the best-attempt upsert starting in the Phoenix 2 schema window — it is the foundation of the commonly-requested score-history feature and accumulates data that cannot be captured retroactively. Promotion of the journal to source of truth is a future, Ledger-internal decision; if ever taken, ES stays inside ScoreLedger — PlayerProgress and ChartIntelligence remain projections. |

### D6. A2 amendment: shared ports become read-only

`IUserRepository` / `IChartRepository` / `ISongRepository` leave the shared-port list. Shared read access is `IUserReader` (Identity) and `ICatalogReader` (Catalog); **writes are owned by their vertical**. `ICurrentUserAccessor`, `IDateTimeOffsetAccessor`, `IRandomNumberGenerator`, and `IBus` remain shared ports. The MediatR-in-Domain carve-out stays.

### D7. Infrastructure stance (evaluated 2026-06-12)

- **Transport:** stays MassTransit in-memory. Publish-side durability via the MassTransit EF transactional outbox (P7). If consume-side durability is ever demanded, the path is **Azure Service Bus** (managed, MT-native), not self-hosted RabbitMQ — and D3's serialization rule makes that swap config-level.
- **Datastore:** stays SQL Server. Cosmos DB rejected: the model is relational, Hangfire needs SQL, A3's one-DB story is an asset, and the journal-on-SQL serves the event-stream need at this scale.
- **Aspire:** approved as a *separate* local-dev/onboarding item (BACKLOG "friction-free local-dev mode"); not part of the rearch sequence.

## Consequences

- Boundary violations become compile errors or red arch tests instead of review comments.
- Contract events double as partner webhook payloads; their envelope and JSON-cleanliness are public-contract obligations from the moment they exist.
- The Phoenix 2 mix transition and the rearch interact in exactly two places: the journal rides the Phoenix 2 schema window (C31), and per-mix derived tables move with their owning vertical.
- `ARCHITECTURE.md` and `CLAUDE.md` layer/package tables are rewritten incrementally — each phase's closing commit updates them (same-PR rule).
- Rollback story: every commit builds green and is individually revertible; ratchet tests pin completed phases.
