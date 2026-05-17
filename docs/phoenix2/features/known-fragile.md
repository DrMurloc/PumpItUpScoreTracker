# Feature: Known-fragile scraper spots

> Status: **inventory** · Last updated: 2026-05-16

The places in the import/scraping stack that will silently break or mis-behave if PIU's site changes shape. Not a remediation plan — that lives in [import-flow.md](import-flow.md) and Phase 3. This doc is the *inventory* so we don't forget what to check when Phoenix 2 ships.

## Load these first (agent context)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`import-flow.md`](import-flow.md)
- [`ARCHITECTURE.md`](../../../ARCHITECTURE.md) — section "Data access", section "Eventing"

## Scope

- Hardcoded URL constants in scraping code
- Regex patterns parsing HTML structure
- Hardcoded mapping dictionaries that paper over PIU's inconsistencies
- Silent-failure modes in the recurring import job
- The single-smoke-assertion that catches the broadest category of breakage

## Out of scope

- Fixing the scraper for Phoenix 2 — that work waits until PIU's site is observable, see [phase-3-launch-week.md](../phases/phase-3-launch-week.md)
- Refactoring the scraper architecture — out of scope for the Phoenix 2 release

## Locked decisions

- **I1** — Document the silent failure modes here. Action (rework, alerting, restructure) deferred until we see what changes. Awareness now, action later.
- **I2** — Add a smoke assertion to the weekly leaderboard import Hangfire job: "imported >0 scores at known levels." Mix-agnostic, cheap, catches the broadest scraper-broke-and-returned-empty failure.

## Inventory

### Hardcoded URLs in [`PiuGameApi.cs`](../../../ScoreTracker/ScoreTracker.Data/Apis/PiuGameApi.cs)

Verified during the explore pass:

| URL | Purpose | Risk |
|---|---|---|
| `https://piugame.com/leaderboard/over_ranking.php` | Top-level "20 above" leaderboard | 404 on path change → no leaderboard data |
| `https://piugame.com/leaderboard/over_ranking_view.php` | Per-song leaderboard | 404 on path change → no per-song data |
| `https://piugame.com/leaderboard/rating_ranking.php` | Rating leaderboards | 404 on path change |
| `https://piugame.com/ajax/top_steps.php` (POST) | Chart popularity | Schema change → silent bad parse |
| `https://piugame.com/my_page/recently_played.php` | User recent scores | 404 → no recent-import flow |
| `https://piugame.com/my_page/my_best_score.php` | User best scores (paginated) | 404 → **import dies** |
| `https://piugame.com/my_page/title.php` | User profile + titles | 404 → no title detection, no avatar |
| `https://piugame.com/bbs/login_check.php` (POST) | Authentication | Change → **auth dies entirely** |
| `https://piugame.com/my_page/game_id_information.php` | Game card switching | 404 → no card switching |
| `https://ucs.piugame.com/bbs/board.php` | UCS chart metadata | 404 → no UCS detail |
| `https://piutracker.app:3002/api/sync/` ([`PiuTrackerClient.cs`](../../../ScoreTracker/ScoreTracker.Data/Clients/PiuTrackerClient.cs)) | Third-party sync | Auth-dependent on the same session token |

### Regex patterns and CDN-path parsing

- **Difficulty parsing via image URLs**: regex matches `stepball/full/<level>.png` style paths to infer chart level. If PIU restructures their image CDN, every imported chart silently parses as the default level. ([`PiuGameApi.cs`](../../../ScoreTracker/ScoreTracker.Data/Apis/PiuGameApi.cs) lines ~18–28)
- **Avatar URL regex**: matches `piugame.com/data/avatar_img/` patterns to extract avatar IDs and store them in Azure Blob. Path change → avatars silently break.
- **Plate parsing via image filename**: `PhoenixPlateHelperMethods.ParseShorthand()` works off plate image filenames. Filename change → plate parsing fails.

### Hardcoded mappings in [`OfficialSiteClient.cs`](../../../ScoreTracker/ScoreTracker.Data/Clients/OfficialSiteClient.cs)

- **Korean → English song name dictionary** (around lines 334–344). Papers over inconsistent naming on PIU's side. If PIU normalizes their naming, the dictionary becomes dead code or causes mis-mappings.

### Silent failure modes

- **Hangfire weekly leaderboard import job** ([`Program.cs`](../../../ScoreTracker/ScoreTracker/Program.cs) `start-leaderboard-import`, Sundays 05:30 ET, cron `30 10 * * 0`): no alerting on failure. Can fail every Sunday for a month and nothing notifies. Smoke assertion (I2) is the mitigation.
- **No retry/alerting on `OfficialLeaderboardSaga`**: if the saga throws, the message is in-memory-bus-only and not redelivered after a restart.
- **`PiuTrackerClient` failures**: behind a `SyncPiuTracker` flag on the import command. Failures here are deliberately optional and currently swallowed.

## The smoke assertion (I2)

In the Hangfire-driven recurring leaderboard import:

```csharp
// HostedServices/RecurringJobRunner.cs (illustrative)
public async Task RunWeeklyLeaderboardImport()
{
    var result = await _bus.Request<StartLeaderboardImportEvent, LeaderboardImportCompleted>(/*...*/);
    
    // Smoke assertion: at least N scores at canonical levels imported
    if (result.ImportedScoreCount < 100 || !result.LevelsObserved.Any(l => l >= 20))
    {
        _logger.LogError("Leaderboard import returned suspicious data: {Count} scores, levels {Levels}",
            result.ImportedScoreCount, string.Join(",", result.LevelsObserved));
        // Optionally: publish AlertEvent → Discord webhook
    }
}
```

Threshold values tuned during Phase 1. Goal: catch "scraper returned empty" and "scraper returned data with no high-level charts" — both indicate a parser break.

## What to check on Phoenix 2 launch day

Phase 3's first task: walk this list against PIU's actual Phoenix 2 site.

1. **Does login still work?** (`login_check.php`)
2. **Do the URL paths still exist?** (404 hunt across the URL table above)
3. **Has the HTML structure changed?** (compare a recent vs. live `my_best_score.php` page; if class names changed, parsing breaks)
4. **Did the image CDN paths change?** (`stepball/full/`, avatars, plates)
5. **Are there new song names that need mapping entries?**
6. **Did the rating/leaderboard query format change?**

## Files this touches

This doc is reference-only. Action items live in the relevant phase docs:

- Smoke assertion: [phase-1-safety-nets.md](../phases/phase-1-safety-nets.md)
- Scraper rework: [phase-3-launch-week.md](../phases/phase-3-launch-week.md)
- Long-term restructure (out of current scope): [phase-4-slow-burn.md](../phases/phase-4-slow-burn.md)

## Open questions

- (Resolved as PIU's Phoenix 2 site becomes observable.)

## Changelog

- 2026-05-16: Doc created from workshop.
