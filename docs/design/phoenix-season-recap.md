# Phoenix Season Recap

**Status:** implemented — C1–C9 all landed on `claude/phoenix-season-recap-a0db4f` (2026-07-10). PR #128 has since merged to main and this branch merged main back in, so it PRs cleanly against main.

A Raider.IO-style end-of-season recap: one animated, screenshottable page that walks a player through their Phoenix era. Every threshold below was validated against production data on 2026-07-09; measured player counts are noted so future tuning has a baseline.

---

## Page & access

- Route **`/Player/{userId}/PhoenixRecap`** — public shareable URL, gated exactly like `/Player/{id}/Sessions` (public profiles only), plus a "My Recap" entry for the signed-in user.
- Full-screen **slide deck** (scroll-snap sections), CSS keyframe animations only — Blazor Server friendly, no JS interop. Each slide is self-contained and screenshottable.
- Phoenix accent `#1D9BCC` (`MixEnum.GetAccentColor()`) throughout; the finale slide flips to Phoenix 2 green `#6CA832`.
- **One-time popup** on login pointing at the page: `MudDialog` in `MainLayout` following the B1G ONE 2024 pattern (`UserSettings` flag, key `MainLayout__PhoenixRecapPopupShown`). Only shown when the viewer's recap row exists. No expiry cutoff initially (set one when the P2 launch date is known).

## Compute model

**Owner decision: not a recurring job.** Two buttons on `/Admin`:

1. **Compute my recap** — publishes the trigger for the admin's own user (fast feedback loop).
2. **Compute all recaps** — one-shot job over every eligible user.

- **Plus per-player freshness** (owner call after round one — compute proved fast): RecapSaga also consumes `ScoreHighlightsCapturedEvent`, so a settled Phoenix score batch recomputes that player's recap on the same signal that fires the Discord card. Cross-player inputs ride a 30-minute shared cache (candidate top-50 sets 6 h; the subject's own set always recomputes), so a busy import evening stays cheap. Failures are swallowed and logged — recaps never disturb the import pipeline.
- Trigger is an imperative bus command in PlayerProgress `Contracts/` (e.g. `CalculateSeasonRecapsCommand(Guid? UserId)`), consumed by a new **RecapSaga** in PlayerProgress. Idempotent upsert — safe to re-fire after a process restart (in-memory transport caveat).
- Results persist to a new table **`scores.PlayerSeasonRecap`** (PK `UserId + MixId`; `Payload` JSON, `SchemaVersion`, `ComputedAt`). The page reads the persisted row only — no live computation.
- Recap is keyed by mix (`MixEnum.Phoenix` now) so a future Phoenix 2 recap reuses the whole pipeline.
- **Eligibility:** ≥10 non-broken Phoenix scores (~1,371 users in prod: 1,289 with ≥50, 82 with 10–49). Sections degrade gracefully when their data is thin.
- Future option (post-launch, based on observed job cost): a self-serve recalculate button for users.

**Perf note:** rival matching is the expensive step. The all-users job memoizes each user's top-50 competitive (fung) chart-id set once per run, so matching is set intersection — O(users × scores) overall for ~1,450 users. The impressive-scores step must reuse `ScoreQualitySaga`'s cached cohort machinery rather than issuing per-user raw cohort-ranking queries — cohort ranking against prod SQL is exactly what caused the 2026-07-10 CPU incident (fixed by PR #129's bucket caching + covering index).

## Slides

In deck order. Data sources are published contracts/ports only (see Architecture).

### 1. Your Arc (opener)
First vs latest `PlayerHistory` snapshot: competitive level then → now, pass-count growth, animated climb.

### 2. Play rollup
- **Play days**: distinct calendar days in `ScoreEventJournal` (prod: avg 23.8, max 538). *Import counts were dropped — session capture only started 2026-07 (avg 2.4 tracked sessions), too thin to brag about.*
- **Charts passed** + rank + percentile across all PIUScores Phoenix players.
- **Singles & doubles competitive rank** + percentile.
- **Total chart time & note count** (best scores only): `Song.Duration` is .NET ticks (validated: min 38 s, max 6:18, zero missing); `ChartMix.NoteCount` missing on only 9 charts. Rendered with fun conversions ("2.5 days of nonstop Pump").
- **Top 5 step artists** — with the mislabel disclaimer (~22% of charts have no step artist).

### 3. Player type
Average score of the top-50 Pumbility scores (minimum 10 scores; computed over what exists below 50). Five types (owner-named; prod distribution noted):

| Type | Avg-score band | Prod players |
|---|---|---|
| Pass Pusher | ≤ AA+ (< 950,000) | 321 |
| Pass Refiner | AAA–AAA+ (950,000–969,999) | 653 |
| Balanced Player | S–S+ (970,000–979,999) | 205 |
| Competitive | SS–SSS (980,000–994,999) | 140 |
| Perfectionist | SSS+ (≥ 995,000) | 43 |

### 4. Badges
Show all earned; lead with the biggest.

- **Title collection** — measured against the **full Phoenix title list including site-detected titles** (213 in code today; use `PhoenixTitleList.BuildList().Count()` at runtime, never hardcode). Owner decision: site-detected titles count — people fly to other countries to earn them. Thresholds are personal-progress percentages, deliberately not population-tuned: **Title Hunter ≥50%, Title Collector ≥75%, Title Master ≥90%, "Seriously, Leave Some Titles for the Rest of Us" ≥95%**. (Prod today: 37 / 5 / 0 / 0 players — the top two are aspirational and that's intended.)
- **Special Snowflake** — holds ≥1 title held by <1% of titled users (`GetTitleAggregations` / `CountTitledUsers`; ~≤13 of 1,314 holders). The only comparative title badge.
- **Completionist ladder** — count of `(Type, Level)` folders (S and D counted separately, no level floor) with ≥90% of charts passed: **5 / 10 / 20 / 30 / 40** → Completionist, Plus, Supreme, Ultra, **"You Know Pump It Up Doesn't Do Lamps, Right?"** (Prod: 106 / 45 / 21 / 10 / **1** players — validated as specced.)
- **CoOp ladder** — completion of CoOp ×2 charts (`ChartType.CoOp`, `Level == 2`; player count *is* the Level field): **>50% Socialite, >75% Clearly Has Friends, >90% Friendship is Magic, 100% "I Hope You Held Hands on Canon D"**. (Prod: 123 / 80 / 50 / 12.)
- **BanYa Lover** — >50% of charts passed on songs where `Artist LIKE '%banya%' OR Artist LIKE '%yahpp%'` (covers `BanYa`, `Banya Production`, `YAHPP`, and collabs; **msgoon excluded** — no BanYa Production membership). ~162 songs in prod; 55 songs have NULL artist and silently don't count.
- **"Big Feet or Injured Back?"** — SSS+ (≥995,000, not broken) on **Uh-Heung S22**. Chart resolved at compute time by song name + Single + level 22 (never a hardcoded chart id). (Prod: 18 holders.)
- **"Grand Mashter"** *(not a typo)* — more than **75%** of S24+ singles passed **at AA+ or lower** (≤949,999). Only mash-grade passes count toward the 75%; stray AAAs don't disqualify. (Prod: 16 holders. Calibrated 2026-07-09 — the original 95%-passed/hard-AAA+-cap version had no plausible holders; 36 S24+ charts exist today, so the bar is currently ≥28 mash-grade passes.)
- **"Now You Can Play the Game"** — passed any Double level ≥28 (≥ so a D29-only pass still counts). (Prod: 5 holders.)
- **"Dove 🕊️"** — easter egg: exact `GameTag` match `DULKI #2827`. Rendered with the emoji, not the word.

### 5. Rivals
3 singles + 3 doubles rivals. Candidates are within **±0.25** of your singles/doubles competitive level, **public users only**, ranked by overlap of top-50 competitive (fung) chart-id sets. Tiers are **strict priority, not thresholds** (owner call after round one — a ≥3-candidates-or-fall-through rule surfaced strangers over community mates): ANY in-range member of your user-created communities outranks everyone outside them; your country community and then the global pool only top up leftover slots. (Country communities are auto-joined system communities like "World", so "non-World" alone would make USA ≈ World.)

### 6. Most impressive PGs
Records with `Plate = PerfectGame`, ordered folder descending then PG difficulty descending; only charts whose **"PG" tier list** category is `Hard`, `VeryHard`, or `Underrated`.

### 7. Most impressive passes
3 singles + 3 doubles, folder descending; only charts whose **"Difficulty" (pass) tier list** category is `Hard`, `VeryHard`, or `Underrated`.

### 8. Most impressive scores
3 singles + 3 doubles from competitive-contributing scores, descending, taking scores with **>90% tie-inclusive percentile** vs the ±0.5 competitive cohort (reuse `ScoreQualitySaga` mechanics / `ScoreRankings.TieInclusivePercentile`). PGs excluded (they live on slide 6).

### 9. Weekly Charts
From `UserWeeklyPlacing` (rows carry `Place`, `Score`, `Plate`, `WasWithinRange`, `ObtainedDate`):

- **Longest streak** (headline): consecutive **rotations** entered — order the distinct global placement weeks by `ObtainedDate`, longest unbroken run the user appears in. Rotation-indexed, not calendar-indexed, so a skipped rotation breaks nobody's streak unfairly. (Prod: top streak is 78 consecutive rotations; the top 20 range 8–78 — strong headline material.)
- Weeks entered (total), wins + podium count — **computed within range**: on charts where the player was within range, re-rank among within-range entrants only (from the placing rows' `Score`/`WasWithinRange`, not the stored overall `Place`), so a level-19 player isn't "beaten" by level-25 tourists.
- **Best result**: best within-range rank (tie-break: higher chart level, then more recent), shown with the chart and week.
- **Giant Slayer**: the player outscored an entrant ≥1.0 competitive level above them (each row snapshots the entrant's `CompetitiveLevel`; margin tunable). Tightened after round one (raw per-week moments read ~68 for the owner): **both runs must be unbroken**, and **each giant counts once** however many weeks you beat them — the count is distinct players slain, and the top 3 callouts keep each giant's most dramatic fall (biggest gap, then score margin).

Exact highlight composition is the one soft area of this spec — owner is open to iteration here.

### 10. Trophy shelf
Left panel: **highest difficulty title** (+ how many players reached it, from the rarity aggregations), your 3 rarest titles (with % of titled users), and the singles-vs-doubles split. Middle: plate cabinet (count per plate). Right: **grade distribution** with plus-grades folded in (SS+ counts as SS — ten buckets max, bar-filled rows). Title names render in brackets (`[EXPERT Lv. 1]`), matching the game. *Longest-standing best was cut after round one (read as confusing); community placements stay future polish.*

### 11. Phoenix 2 finale
P1 scores projected onto Phoenix 2: carried-over charts (4,367 of 4,571 — 95.5%) rescored with P2 `ChartMix` levels through the mix-keyed P2 formula (PR #128), **two-pool Singles/Doubles Pumbility totals + two projected titles** — the highest `Phoenix2PumbilityTitle` from the Singles pool ladder and the highest from the Doubles pool ladder (`PumbilityPool.Singles`/`Doubles`; the hidden Total tiers are not projected). Green accent; "see you in Phoenix 2". Note the existing `PumbilityProjectionSaga` is P1→peer-expectation, *not* this — the P1→P2 rescoring is new code.

**Dropped ideas:** White Whale (owner), import counts (data too thin), nightly Hangfire job (owner wants admin-triggered).

## Localization

New keys populated in all 8 locales in the same pass, per convention. Badge names: translate where a faithful localized version exists (use the per-locale glossaries); where the pun doesn't carry ("You Know Pump It Up Doesn't Do Lamps, Right?", "I Hope You Held Hands on Canon D"), best-effort localized humor or keep English per glossary judgment — owner delegated this call.

## Architecture placement

- **RecapSaga + calculators live in PlayerProgress** — precedent: `HighlightCaptureSaga` already does cross-vertical enrichment through published contracts.
- Cross-vertical reads via published contracts/ports only: `GetPhoenixRecordsQuery` + journal queries (ScoreLedger), `GetChartsQuery` (Catalog), `GetTierListQuery` (ChartIntelligence), `GetMyCommunitiesQuery`/`GetCommunityMembersQuery` (Communities), `GetUserWeeklyPlacementsQuery` (WeeklyChallenge), `IPlayerStatsReader`/`ITitleRepository` (Domain ports). Never SQL joins onto other verticals' tables.
- Pure calculators (player type, badges, rival matcher, P2 projection assembly) are Domain services with unit tests; the saga orchestrates and persists.
- `PlayerSeasonRecapEntity` is internal to PlayerProgress, registered via its `IDbModelContribution`; new row in [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md).

## Commit plan

- **C1** — this design doc.
- **C2** — Domain: recap payload records + pure calculators (player type, title %, completionist, co-op, BanYa, snowflake, plate cabinet) + `DomainTests`.
- **C3** — Contracts + persistence: trigger command, `GetPlayerRecapQuery`, `PlayerSeasonRecapEntity` + model contribution + migration + repository; DATABASE-SCHEMA.md row.
- **C4** — RecapSaga compute pipeline (rollup, badges, impressive picks) + `ApplicationTests` with mocked ports.
- **C5** — Rival matcher + saga wiring + tests.
- **C6** — P2 projection step + tests (consistent with `Phoenix2PumbilityScoringTests` golden rows).
- **C7** — Admin: the two compute buttons on `Admin.razor` publishing the triggers.
- **C8** — Web: the `/Player/{id}/PhoenixRecap` slide deck + localization keys (8 locales).
- **C9** — Popup + "My Recap" nav entry + polish.

## Open items

- Popup expiry cutoff (set when the P2 launch date is known).
- Self-serve recalculate button — decide post-launch from all-users job timings.
- Giant Slayer level margin (≥1.0 assumed) — calibrate.
- Weekly slide highlight composition — iterate once rendered.
