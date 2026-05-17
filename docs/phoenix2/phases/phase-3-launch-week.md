# Phase 3: Launch week

> Status: **[ ] Not started** · Last updated: 2026-05-16

The week of Phoenix 2's actual release. This phase is reactive — most tasks depend on what PIU's Phoenix 2 site actually looks like once it's live.

## Load these first (required)

Refuse to proceed if any of these are missing:

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`docs/phoenix2/features/import-flow.md`](../features/import-flow.md)
- [`docs/phoenix2/features/known-fragile.md`](../features/known-fragile.md)
- [`docs/phoenix2/features/mix-model.md`](../features/mix-model.md)
- [`docs/phoenix2/features/notifications-gating.md`](../features/notifications-gating.md)
- [`docs/phoenix2/phases/phase-2-pre-launch.md`](phase-2-pre-launch.md) — verify it's done

## Prerequisites

- Phase 2 complete: schema, events, gating, accessors all in place. Phoenix 2 exists in the enum without `[LiveMix]`.
- PIU has shipped Phoenix 2 (or is shipping imminently) — we can see the live site.

## In scope

- Walk the [`known-fragile.md`](../features/known-fragile.md) checklist against PIU's actual Phoenix 2 site
- Build `UploadPhoenix2Scores.razor` — shape determined by PIU's site
- Rework the scraper (`PiuGameApi`, `OfficialSiteClient`) if PIU's URLs, HTML, or auth changed
- Tune the defensive corruption guard threshold against real Phoenix 2 traffic
- Move `[LiveMix]` from `Phoenix` to `Phoenix2` (one-line code change, deploy)
- Verify notification gating actually quieted Phoenix 1 → Discord
- Communicate to Discord users when Phoenix 2 mode is available

## Out of scope (defer to Phase 4)

- Derived-data tables that were marked "fully rebuilds, defer"
- DB-backed titles
- Older-mix support (pre-XX)
- Scraper architectural rework

## Locked decisions affecting this phase

- **G1** — Import is explicit on the command (`UploadPhoenix2Scores.razor` passes `Mix = MixEnum.Phoenix2`).
- **G2** — Defensive guard runs against scraped responses. Threshold tuned during this phase.
- **C2/C4/H1** — Moving `[LiveMix]` from `Phoenix` to `Phoenix2` is the one-line toggle. Same change makes Phoenix 2 selectable in the UI and becomes the default for new users.

## Tasks

### Reconnaissance (do first)

1. [ ] **Walk the fragile-spots checklist.** See [known-fragile.md "What to check on Phoenix 2 launch day"](../features/known-fragile.md).
   - Does `login_check.php` still authenticate?
   - Do the 11+ hardcoded URLs still resolve?
   - Has the HTML for `my_best_score.php` changed?
   - Have image CDN paths (`stepball/full/`, avatars, plates) changed?
   - Are there new song names that need mapping entries?
   - Document findings in this file's Changelog.

2. [ ] **Manual import dry-run against Phoenix 2 data, in a dev env with `MixId = Phoenix2`.** Don't ship yet — observe what the scraper actually parses.

### Scraper rework (only what reconnaissance flagged)

3. [ ] **Update hardcoded URLs** in [`PiuGameApi.cs`](../../../ScoreTracker/ScoreTracker.Data/Apis/PiuGameApi.cs) if any changed.

4. [ ] **Update HTML parsers** if class names or structure changed.

5. [ ] **Update image-path regexes** (difficulty parsing, avatars, plates) if CDN paths changed.

6. [ ] **Add new song-name mappings** to `OfficialSiteClient`'s Korean→English dict if needed.

7. [ ] **Handle simultaneous Phoenix 1 + Phoenix 2 support**, if PIU ships both at once:
   - Decision needed: does the scraper accept a `MixEnum` and route to different URLs per mix, or does the scraper auto-detect mix from response shape? The roadmap defaults to **explicit mix on command** (G1) — the scraper accepts mix as a parameter and uses different URLs as needed.
   - If PIU shut Phoenix 1 down at Phoenix 2 launch: ignore this task; only Phoenix 2 URLs matter.

### UI

8. [ ] **Build `UploadPhoenix2Scores.razor`.** Sibling of `UploadPhoenixScores.razor`. Same flow; passes `Mix = MixEnum.Phoenix2` to the command. Gated on Phoenix 2 being selectable (which happens after step 11 below).

### Guard tuning

9. [ ] **Tune the corruption guard threshold** against observed Phoenix 2 traffic. Run several test imports; measure how often the heuristic fires false-positive. Adjust thresholds in [`OfficialLeaderboardSaga`](../../../ScoreTracker/ScoreTracker.Application/Handlers/OfficialLeaderboardSaga.cs).

### The flip

10. [ ] **Final verification before flip:**
    - All scraper tests green
    - Test users can import to Phoenix 2 in dev with no corruption
    - Notification gating verified (Phoenix 1 import in dev does not fire Discord notifications when LiveMix moves)
    - Backup taken of prod DB

11. [ ] **Move `[LiveMix]` from `Phoenix` to `Phoenix2` in `MixEnum.cs`.** Deploy. This:
    - Makes Phoenix 2 the default for new users (C1 — create handler writes LiveMix)
    - Makes Phoenix 2 selectable in the UI (C4 selectability rule)
    - Quiets Phoenix 1 → Discord notifications (notification gating)
    - Keeps Phoenix 1 selectable for existing users (per C3, they stay opted in)

12. [ ] **Post-deploy verification:**
    - Existing users still see Phoenix 1 mode by default (their `CurrentMix` is unchanged).
    - New users land on Phoenix 2.
    - The Phoenix 2 import page is reachable.
    - Importing a Phoenix 2 score writes a row with `MixId = Phoenix2`.
    - Phoenix 1 import flow still works; the guard doesn't false-positive on legit Phoenix 1 data.

### Communication

13. [ ] **Discord announcement.** Cover:
    - Phoenix 2 mode is live; how to switch.
    - Tier Lists and Weekly Charts will be sparse early; that's expected.
    - Phoenix 1 scores remain accessible by toggling mix.
    - Known issues / what's not yet supported (Title list is sparse; etc.)

## Success criteria

- [ ] Phoenix 2 mode is selectable and functional in prod.
- [ ] At least one real Phoenix 2 import has succeeded end-to-end with correct `MixId` writes.
- [ ] Phoenix 1 scores remain untouched and queryable.
- [ ] No silent corruption: every Phoenix 1 import in prod still parses as Phoenix 1; every Phoenix 2 import as Phoenix 2.
- [ ] Discord notification gating verified live.
- [ ] Smoke assertion (from Phase 1) is reporting non-zero counts again post-Phoenix-2 scraper rework.

## Open questions (resolved during this phase)

- Whether Phoenix 1 site stays up alongside Phoenix 2 (informs scraper URL routing)
- Whether auth changed
- Whether HTML structure changed
- Whether new song-name mappings are needed
- Final corruption-guard threshold

## Changelog

- 2026-05-16: Phase doc created from workshop.
