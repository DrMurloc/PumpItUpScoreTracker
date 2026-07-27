# Domain glossary

[Pump It Up](https://en.wikipedia.org/wiki/Pump_It_Up_\(video_game_series\)) (PIU) is a five-panel dance arcade game by [Andamiro](https://www.piugame.com/). Players step on arrows in time with music. This document defines the PIU and project terms the code uses, in a form approachable to people who haven't played the game.

For solution layout and patterns, see [ARCHITECTURE.md](ARCHITECTURE.md). For project conventions, see [CLAUDE.md](../CLAUDE.md).

## Game basics

- **Pump It Up** — the arcade series. Current release: **Pump It Up 2024 Phoenix** (May 2024). **Phoenix 2** is announced — its website went live 2026-07-01 at piugame.com (Phoenix's site moved to phoenix.piugame.com), but the game itself has not released yet.
- **Mix** — a version of PIU. Three mixes matter here: **XX** (legacy, pre-Phoenix, its own tables/pages), and **Phoenix** / **Phoenix 2**, which run **in parallel** — both use Phoenix scoring, share `Chart` rows via per-mix `ChartMix` levels, and every score-derived table is keyed by mix. Modeled by `MixEnum`; users switch via the current-mix selector, and several Phoenix 2 surfaces show "Coming soon" until post-launch verification (titles, recommendations, import, official leaderboards).
- **Song** — a piece of music: title, artist, duration, BPM. One song typically has multiple charts. See [`Song`](../ScoreTracker/ScoreTracker.SharedKernel/Models/Song.cs).
- **Chart** — a specific playable arrangement of a song: a difficulty level + chart type (Single, Double, Co-op) + step pattern. A given song often has many charts (e.g. Single S15, Single S20, Double D18, Co-op-2). See [`Chart`](../ScoreTracker/ScoreTracker.SharedKernel/Models/Chart.cs).
- **Difficulty level** — numeric chart rating (e.g. 15, 20, 26). Higher = harder. See [`DifficultyLevel`](../ScoreTracker/ScoreTracker.SharedKernel/ValueTypes/DifficultyLevel.cs).

## Scoring

- **Phoenix score** — the current scoring scheme. 0–1,000,000 points, accuracy-based rather than combo-based. See [`PhoenixScore`](../ScoreTracker/ScoreTracker.SharedKernel/ValueTypes/PhoenixScore.cs).
- **XX score** — the pre-Phoenix scoring scheme (combo and letter based). **Legacy** — retained for historical data; new feature work targets Phoenix only.
- **Letter grade** — end-of-chart performance rating. Phoenix and XX have separate letter-grade systems (`PhoenixLetterGrade`, `XXLetterGrade`).
- **Plate** — Phoenix-only secondary rating, based on per-step accuracy distribution. Modeled by `PhoenixPlate`.
- **Judgment** — the per-step verdict: Perfect, Great, Good, Bad, Miss. Drives both score and the lifebar. Kept in English in every locale. Modeled by `Judgment`.
- **Lifebar** — the health gauge. The **visible bar** is 0–1000 life (full = the rainbow bar); every song starts you at 500. Above 1000 sits the **overflow**, `3 × level²` of life the cabinet never shows you — the only thing a higher level changes. Phoenix 2 runs an electric effect along the bar when the overflow is completely full. A hidden **gain multiplier** (0.1 at song start, capped at 0.8, near-zeroed by a miss and halved by a bad) scales what clean notes pay, which is why misses cost far more than the life they take. Modeled by [`LifebarSimulator`](../ScoreTracker/ScoreTracker.SharedKernel/Models/LifebarSimulator.cs); derived answers live in `LifebarAnalysis` and surface on `/LifeCalculator` ([design](design/life-calculator-redesign.md)). Formulas are an NX2/Prime data-mine, unverified against Phoenix.

## Player progression

- **Pumbility** — a composite player rating computed from a player's top scores; the closest single number to "how good is this player overall." Calculated by `PlayerRatingSaga` / `WorldRankingService`, with a **different formula per mix** (`ScoringConfiguration.PumbilityScoring(mix, …)`):
  - **Phoenix**: one mixed top-50 pool; per chart `BaseRating(level) × gradeModifier`, plate-blind.
  - **Phoenix 2** (reverse-engineered from the live site; per-chart values verified to the cent against the official breakdown page `my_page/pumbility.php`, 2026-07-19): **one merged top-50 pool across Singles+Doubles** for the overall value (plus separate per-type top-50 pools behind the Singles/Doubles boards); per chart `Base(pricedLevel) × (gradeMultiplier + plateBonus)` (additive), where `Base = 130 + 5·L + 5·max(0, L−24)` and **singles price one level up the curve** (an S17 is worth `Base(18)`; doubles price at their printed level). **Charts below level 10 price at zero.** CO-OP, UCS, half-double and broken plays never contribute. The site exposes all three values (Total = merged top-50, Singles/Doubles = per-type pools); the title ladder's [S]/[D] tiers gate on the per-type pools and the Total tier on the merged pool. Overall PUMBILITY is NOT Singles + Doubles (corrected 2026-07-13).
- **Competitive Level progress** — a tier/level system tracking a player's competitive standing, driven primarily by Weekly Charts performance. UI at `Pages/Progress/CompetitiveLevel.razor`.
- **Peers** — the settled UI term for *players near your competitive level*: the cohort the bucket-cached machinery (`CohortScoreProvider`, half-level buckets ±0.5) ranks a player's scores against. "You beat 80% of Peers" means your best on that chart beats 80% of that cohort's bests. First shipped in the Hot Streak widget goal; older copy ("competitive matches" on Account Stats) predates the term.

## Community-tracked systems

The features the community uses most:

- **Tier list** — charts categorized into difficulty/skill buckets. Generated by `TierListSaga` from aggregate score data. UI under `Pages/TierLists/`.
- **Badge** — what a chart actually demands, as piucenter measures it: 33 granular tags (`staggered_bracket`, `twist_over90`, `anchor_run`, …) banked per chart with a segment-coverage fraction, plus a top-three dominance pick. This is the site's skill vocabulary. Badges group into five **badge families** — Brackets, Twists, Stamina/Runs, Tech, Doubles Tech — which carry the identity colours (`BadgeCategory`, `PiuCenterBadges`). ⚠ The older `Skill` enum is a lossy 33-into-11 rollup of these, still wired into the tier-list Skill lens and Pumbility and slated for deletion — see [design/nuke-old-skill-categories.md](design/nuke-old-skill-categories.md).
- **Weekly Charts** — a weekly-rotating set of charts the community competes on, scraped from the official PIU site. Refreshed by the recurring `RotateWeeklyChartsCommand`. Drives Competitive Level progress.
- **Community Leaderboards** — per-community (user-formed group: team, friends, region) leaderboards. See [`ICommunityRepository`](../ScoreTracker/ScoreTracker.Communities/Domain/ICommunityRepository.cs).
- **UCS** — User-Created Step: a community-authored chart for a song, ranked separately from official charts. Still a real part of the game; **this site no longer tracks it.** The UCS vertical and its leaderboard page were retired (tables archived, see [DATABASE-SCHEMA.md](DATABASE-SCHEMA.md)). The term survives here because it still appears in scoring rules — UCS plays never count toward PUMBILITY.

## Code-only

- **Saga (in this codebase)** — a class grouping one MassTransit `IConsumer<>` with related MediatR handlers for a single feature (e.g. `TierListSaga`, `WeeklyTournamentSaga`). **Not** a `MassTransit.MassTransitStateMachine` — those are formal state machines; these are feature-grouped handler classes.
