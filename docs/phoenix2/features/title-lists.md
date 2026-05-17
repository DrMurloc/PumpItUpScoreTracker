# Feature: Title lists

> Status: **design locked** · Last updated: 2026-05-16

Phoenix 2 gets its own title list, parallel to `PhoenixTitleList`. Hardcoded static class, same pattern as Phoenix 1.

## Load these first (agent context)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`mix-model.md`](mix-model.md)
- [`events.md`](events.md)

## Scope

- New `Phoenix2TitleList` static class, structured like `PhoenixTitleList`
- `TitleSaga` branching by mix
- `GetTitleProgressQuery` already takes `MixEnum` — verify Phoenix 2 routing
- TODO comment noting future DB-backed migration

## Out of scope

- Migrating titles to a DB-backed table — explicitly deferred. Worth it eventually, but not in Phoenix 2 release scope.
- XX titles — already handled separately if present

## Locked decisions

- **E2** — Phoenix 2 title list is a **hardcoded `Phoenix2TitleList` static class**, parallel to the existing `PhoenixTitleList`. DB-backed titles would be cleaner but are out of scope for this rollout.
- A `// TODO: DB-backed titles when scope allows` comment sits next to both static classes so the deferred decision is visible in code.

## Why not DB-backed now

Worth restating the trade-off:

- **DB-backed pros**: edit titles without deploy; one source of truth; easier when PIU surprises us with a title rebalance mid-release (which they do).
- **DB-backed cons**: schema work, migration, admin UI to manage the data, seed data for both Phoenix 1 and Phoenix 2.

The release scope says "do the minimum that ships Phoenix 2 support correctly." Title migration adds work without unblocking anything else. Phase 4 (slow burn) is a reasonable home for it.

## Phoenix2TitleList shape

Mirrors [`PhoenixTitleList.cs`](../../../ScoreTracker/ScoreTracker.Domain/Models/Titles/Phoenix/PhoenixTitleList.cs):

```csharp
// ScoreTracker.Domain/Models/Titles/Phoenix2/Phoenix2TitleList.cs
// TODO: Migrate titles to DB-backed storage when scope allows.
// Mirror is intentional during Phoenix 2 rollout — see docs/phoenix2/features/title-lists.md
public static class Phoenix2TitleList
{
    public static readonly IReadOnlyList<Title> All = new[]
    {
        // populated as Phoenix 2 titles become known
    };
}
```

Initial content can be empty or near-empty at code commit — populated as titles are documented post-Phoenix 2 launch. The list **must** exist as a stub before Phoenix 2's `[LiveMix]` flip, even if empty, so `TitleSaga` doesn't NullReference.

## TitleSaga branching

```csharp
// ScoreTracker.Application/Handlers/TitleSaga.cs
public async Task Consume(ConsumeContext<TitlesDetectedEvent> ctx)
{
    var titleList = ctx.Message.Mix switch
    {
        MixEnum.Phoenix => PhoenixTitleList.All,
        MixEnum.Phoenix2 => Phoenix2TitleList.All,
        _ => throw new NotSupportedException($"No title list for mix {ctx.Message.Mix}")
    };
    // ...rest of processing uses titleList...
}
```

`TitlesDetectedEvent` already carries `Mix` per [events.md](events.md), so no re-querying is needed.

## Files this touches

- New: `ScoreTracker.Domain/Models/Titles/Phoenix2/Phoenix2TitleList.cs` (and any related per-tier classes if `PhoenixTitleList` is split)
- Modified: `ScoreTracker.Application/Handlers/TitleSaga.cs` (mix-aware branching)
- Comment: add `// TODO: DB-backed titles when scope allows` to `PhoenixTitleList.cs` and `Phoenix2TitleList.cs`
- Verify: [`GetTitleProgressQuery`](../../../ScoreTracker/ScoreTracker.Application/Queries/GetTitleProgressQuery.cs) — already mix-aware per the explore pass, just confirm Phoenix 2 routes correctly

## Risks

- **Title list incompleteness at launch.** Phoenix 2 titles aren't fully documented until PIU ships and the community catalogs them. Ship a stub list and update it post-launch. Communicate this in Discord.
- **Title detection works even when the list is empty** — title-detected events fire, but matching against an empty list means no `NewTitlesAcquiredEvent`. Acceptable; users see updates as the list fills in.
- **Code duplication.** `Phoenix2TitleList` shares structure with `PhoenixTitleList`. That's the cost of deferring DB-backed titles. Live with it; the deferred work cleans it up.

## Open questions

- Are there sub-files per title tier (`Bronze`, `Silver`, `Gold`, `Platinum`) under `Phoenix/`, or is it one flat list? Verify during implementation and mirror the structure exactly under `Phoenix2/`.

## Changelog

- 2026-05-16: Doc created from workshop.
