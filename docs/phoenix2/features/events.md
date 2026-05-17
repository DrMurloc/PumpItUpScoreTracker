# Feature: Event schema changes

> Status: **design locked** · Last updated: 2026-05-16

The score-flow events grow a `MixEnum Mix` property so downstream consumers never have to ask "which mix?"

## Load these first (agent context)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`mix-model.md`](mix-model.md)
- [`phoenix-records-schema.md`](phoenix-records-schema.md)
- [`ARCHITECTURE.md`](../../../ARCHITECTURE.md) — section "Eventing"

## Scope

- Adding `MixEnum Mix` to score-flow events
- Locking down the semantics of that property
- Updating publishers to populate it correctly
- Updating consumers to use it instead of querying user selection

## Out of scope

- Adding mix to events that aren't score-shaped (no mix concept attached)
- Notification gating logic — see [notifications-gating.md](notifications-gating.md)

## Locked decisions

- **D1** — Events that gain a `MixEnum Mix` property:
  - `PlayerScoreUpdatedEvent`
  - `RecentScoreImportedEvent`
  - `PlayerRatingsImprovedEvent`
  - `PlayerStatsUpdatedEvent`
  - `ChartDifficultyUpdatedEvent`
  - `NewTitlesAcquiredEvent`
  - `TitlesDetectedEvent`
  - `UserWeeklyChartsProgressedEvent`
  
  Audit `ScoreTracker.Domain/Events/` during implementation for any others that are score-derived. Cheap to add now, expensive to retrofit.
  
- **D2** — `Mix` on an event = **the mix the score/record belongs to**, not the user's currently-selected mix at the moment of the event. Once a score exists, it has a mix; user selection is a UI affordance, not a property of the score. This makes events replay-safe and lets historical Phoenix 1 imports work cleanly after LiveMix flips to Phoenix 2.

## Why this matters

Today, sagas that need a mix have two unappealing options:

1. **Re-query the user's currently selected mix.** Concurrency hazard — user could have changed selection between event publish and consume.
2. **Default to Phoenix.** Works today because everything is Phoenix; silently corrupts the moment Phoenix 2 has data.

With `Mix` on the event, neither is needed. The event carries the mix it pertains to, and consumers act on that.

## Semantics checklist

When publishing one of these events, the publisher must know which mix the change pertains to:

- **`PlayerScoreUpdatedEvent`** — the mix the new score belongs to. For imports, this comes from the import command (see [import-flow.md](import-flow.md)). For manual entry, from the UI page (e.g. `UploadXXScores.razor` → XX; future per-mix manual entry pages → that mix).
- **`RecentScoreImportedEvent`** — the mix of the import session (carried on the import command).
- **`PlayerRatingsImprovedEvent`** — the mix the rating recalc was for (Pumbility/PUMBILITY+ is per-mix).
- **`PlayerStatsUpdatedEvent`** — the mix of the stats row that just changed.
- **`ChartDifficultyUpdatedEvent`** — the mix the difficulty adjustment applies to (seasonal adjustments only apply to the current/live mix per the roadmap; older mixes use official difficulty).
- **`NewTitlesAcquiredEvent`**, **`TitlesDetectedEvent`** — the mix the titles were observed in (Phoenix 2 has its own title list — see [title-lists.md](title-lists.md)).
- **`UserWeeklyChartsProgressedEvent`** — the mix whose Weekly Charts rotation this progress was against.

## Comment to attach to each event

```csharp
/// <summary>
/// The mix this event pertains to — i.e. the mix the score/record/title belongs to,
/// NOT the user's currently-selected mix at publish time. This makes the event
/// replay-safe and lets consumers act without re-querying user state.
/// </summary>
public required MixEnum Mix { get; init; }
```

Locking the semantics in a per-event comment removes the chance someone misreads `Mix` as "user's selection" later.

## Files this touches

- Every event record in `ScoreTracker.Domain/Events/` listed above
- Every publisher: search for `_bus.Publish(new <Event>` and `IBus.Publish<<Event>>` across the solution
- Every consumer (`IConsumer<<Event>>` implementations in `ScoreTracker.Application/Handlers/`) — most will use the new property instead of re-querying user selection

## Risks

- **Forgotten publish site.** A `_bus.Publish(new PlayerScoreUpdatedEvent { ... })` site that doesn't populate `Mix` becomes a compile error if `Mix` is `required`. Use `required` on the property to make the compiler enforce.
- **Consumer drift.** A consumer that still re-queries user selection instead of using the event's `Mix` defeats the purpose. Add a code-review checklist item; consider lightweight architectural tests asserting consumers of these events don't inject `ICurrentUserAccessor` (heuristic, not perfect).

## Open questions

None known.

## Changelog

- 2026-05-16: Doc created from workshop.
