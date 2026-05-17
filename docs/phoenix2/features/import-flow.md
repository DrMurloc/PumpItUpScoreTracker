# Feature: Import flow

> Status: **design partial — shape depends on PIU's Phoenix 2 site** · Last updated: 2026-05-16

The most-unknown feature stack. We can land the *command* shape and the *guards* before Phoenix 2; the actual *scraper* work waits until we can see PIU's Phoenix 2 site.

## Load these first (agent context)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`mix-model.md`](mix-model.md)
- [`events.md`](events.md)
- [`known-fragile.md`](known-fragile.md)
- [`ARCHITECTURE.md`](../../../ARCHITECTURE.md) — section "Layers / Infrastructure", clients

## Scope

- The import command shape: explicit `Mix` parameter
- Separate UI pages per mix (`UploadPhoenixScores.razor`, future `UploadPhoenix2Scores.razor`)
- The defensive corruption guard
- The unknowns and how we'll handle them when we know more

## Out of scope

- Implementing the Phoenix 2 scraper — deferred until PIU's site is observable (see [phase-3-launch-week.md](../phases/phase-3-launch-week.md))
- Scraper internals — see [known-fragile.md](known-fragile.md) for the silent-failure inventory

## Locked decisions

- **G1** — `Mix` is **explicit on the import command**, not inferred from the scraper response. The import UI page knows which mix it's for (Phoenix 1 page → Phoenix; Phoenix 2 page → Phoenix2); the command carries that explicitly.
- **G2** — A **defensive corruption guard** runs against scraped responses to verify the data shape matches the declared mix. Even a heuristic check is worth shipping: the failure mode (Phoenix 1 page silently returning Phoenix 2 data) is invisible otherwise.

## Open (depends on what PIU ships)

- Whether Phoenix 1 and Phoenix 2 will be supported simultaneously on PIU's site, or Phoenix 1 will be turned off at launch
- Whether the scraping URLs/HTML structure change
- Whether username/password import survives at all (the existential question — if PIU pulls that, manual entry continues but auto-import dies)

## Import command shape

```csharp
// ScoreTracker.Application/Commands/ImportPhoenixScoresCommand.cs
public sealed record ImportPhoenixScoresCommand(
    Guid UserId,
    string Username,
    string Password,
    MixEnum Mix,                 // NEW — explicit, no default
    bool SyncPiuTracker = false) : IRequest;
```

`Mix` is required. The UI page passes it in based on which import page the user clicked.

## UI surface

- Existing: [`Pages/UploadPhoenixScores.razor`](../../../ScoreTracker/ScoreTracker/Pages/UploadPhoenixScores.razor) — keep, modify to pass `Mix = MixEnum.Phoenix` explicitly.
- Existing: [`Pages/UploadXXScores.razor`](../../../ScoreTracker/ScoreTracker/Pages/UploadXXScores.razor) — already mix-specific (XX), no change to its mix selection; XX has its own command path.
- New (Phase 3): `Pages/UploadPhoenix2Scores.razor` — sibling page, passes `Mix = MixEnum.Phoenix2`. Shape depends on what PIU's Phoenix 2 site looks like.

Each page is gated on the mix being selectable per the [mix-model selectability rule](mix-model.md) — Phoenix 2's page is reachable only after `[LiveMix]` moves to `Phoenix2`.

## Defensive corruption guard

The risk: PIU keeps serving Phoenix 1's URLs but those URLs start returning Phoenix 2 data (chart IDs that don't exist in Phoenix 1, scores against new charts, etc.). The Phoenix 1 import command writes those to Phoenix 1's rows → silent corruption of the most-important data we have.

Guard placement: in `OfficialLeaderboardSaga.Consume(ImportPhoenixScoresEvent)` (or equivalent), after the scraper returns its parsed list and before writes occur. Verify:

1. Every scraped chart ID exists in `ChartMix` for the declared mix. If a substantial fraction don't, fail loud — refuse to write, log an alert.
2. (Heuristic, optional) Look for at least one chart known to exist in the declared mix but not in the other. Presence = response is the right shape.

This isn't a complete defense — a perfect overlap between Phoenix 1 and Phoenix 2 chart pools would fool it — but the failure mode it catches is high-likelihood and otherwise invisible.

```csharp
private void GuardOrThrow(IReadOnlyCollection<ScrapedScore> scores, MixEnum declaredMix)
{
    var validChartIds = _chartRepo.GetChartIdsInMix(declaredMix);  // cache this
    var unknownIds = scores.Select(s => s.ChartId).Except(validChartIds).ToList();
    if (unknownIds.Count > scores.Count * 0.10)  // >10% unknown is suspicious
        throw new ImportMixMismatchException(declaredMix, unknownIds.Take(5).ToList());
}
```

Tune the threshold during implementation.

## Files this touches

- Modified: `ScoreTracker.Application/Commands/ImportPhoenixScoresCommand.cs` (add `MixEnum Mix`)
- Modified: `ScoreTracker.Application/Handlers/OfficialLeaderboardSaga.cs` (use the new property; add guard)
- Modified: `ScoreTracker/Pages/UploadPhoenixScores.razor` (pass mix explicitly)
- Modified: `ScoreTracker/Controllers/Api/PhoenixScoresController.cs` (if any API surface exposes import, accept mix)
- Phase 3: New `ScoreTracker/Pages/UploadPhoenix2Scores.razor`
- Phase 3: Possibly `ScoreTracker.Data/Apis/PiuGameApi.cs` and `ScoreTracker.Data/Clients/OfficialSiteClient.cs` rework — shape unknown until PIU ships

## Risks

- **Scraper assumes Phoenix 1 forever.** Hardcoded URLs in [`PiuGameApi.cs`](../../../ScoreTracker/ScoreTracker.Data/Apis/PiuGameApi.cs) point at `piugame.com/my_page/...` and `piugame.com/leaderboard/...`. If those URLs change, the scraper fails — possibly silently (returns empty). The smoke assertion ([known-fragile.md](known-fragile.md), I2) catches "returned empty."
- **The guard is heuristic.** It can be defeated by a fully-overlapping chart pool. The honest framing: it defends against the obvious failure mode, not every possible mix-confusion bug.
- **PiuTracker shares auth.** [`PiuTrackerClient`](../../../ScoreTracker/ScoreTracker.Data/Clients/PiuTrackerClient.cs) reuses the PIU session token. If PIU auth changes, this breaks too, but silently (it's behind an optional flag).

## What we'll know in Phase 3

- Whether the existing scraper URLs work for Phoenix 2 unchanged
- Whether PIU's HTML structure for the "my best score" pages changed
- Whether the avatar URL CDN paths changed
- Whether login still works
- Whether there's a separate Phoenix 1 vs Phoenix 2 site, or a single site with mix-aware data

Plan for each scenario in Phase 3, not now.

## Open questions

- (Resolved in Phase 3 when PIU's site is observable.)

## Changelog

- 2026-05-16: Doc created from workshop.
