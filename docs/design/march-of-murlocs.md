# March of Murlocs Overhaul

Status: **Slice 0 landed; Slices 1–5 planned.** Exploration session 2026-08-09.

March of Murlocs is the site's quarterly stamina ladder: 1 hour 45 minutes to bank as many
points as you can, scored with PUMBILITY+. It has been running on autopilot since
[`4474ce15`](#) and the autopilot has been broken since 2026-07-01.

This pass does four things: stop a live runaway that has created 71 garbage tournaments,
**decouple MoM from the `scores.Tournament` table** (which is about to lose qualifiers too and
then die), give **each mix its own leaderboard**, and rebuild a UI that has never had a design
pass.

Today's pages: [`MarchOfMurlocs.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/MarchOfMurlocs.razor)
(season list), [`StaminaTournament.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/StaminaTournament.razor)
(board), [`RecordTournamentSession.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/RecordTournamentSession.razor)
(submission), [`SessionBuilder.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/SessionBuilder.razor)
(the "Test Scores" planner). Logic lives in
[`MarchOfMurlocsHandler.cs`](../../ScoreTracker/ScoreTracker.EventCompetition/Application/MarchOfMurlocsHandler.cs).

Mock: **not yet built** — that is Slice 1, and it deliberately precedes the schema work.

---

## 1. The rules

Confirmed by the owner 2026-08-09 against the
[rules doc](https://docs.google.com/document/d/1Nwr-PDy6lgkTSt4dKu1-0fdeDXdgLWvl7j5yiuIcRCw/edit).
**The rules are not changing in this pass.** Verification is the one exception, and it is a
deletion rather than a rule change (§3).

- **Quarterly seasons.** Jan–Mar = *Winter*, Apr–Jun = *Spring*, Jul–Sep = *Summer*,
  Oct–Dec = *Fall*. A season ends at 23:59:59 on the last day of its final month, UTC−5.
- **1 hour 45 minutes** of play per session.
- **Singles and Doubles are separate boards.** As of this pass, so are Phoenix and Phoenix 2.
- **Repeats are banned** — but the identity is song + chart type + *level*. The same song at a
  different difficulty is a different chart and is legal.
- **Ties never happen in practice**; when they do, **earliest submission wins**.
- **Phoenix 1: A and below are worth zero, and a zero-point play is a non-play** — it does not
  count toward chart count and does not block replaying that chart. It still consumes session
  time.
- **Phoenix 2: the "A or below" rule is unsettled** and will be decided after the scoring
  experiment (§10). P2 ships on stock P2 pumbility, where the worst grade still pays 0.90.
- Song length scales value, with a 2-minute baseline.
- Charts 22+ carry an exponential bonus (Phoenix 1 only — see §5).
- **Stage break is deliberately outside the algorithm, on both mixes.** Passing and scoring are
  different concepts and MoM scores the latter — a failed SS was not a mash. A broken play is
  worth exactly what its score is worth. `StageBreakModifier` stays `1.0`; see §9.2 for the
  inheritance hazard this creates for PUMBILITY2+.

---

## 2. Why now — six defects, all confirmed against production-synced data

### 2.1 The runaway — **fixed in Slice 0**; the junk rows are still owner-run cleanup

[`MarchOfMurlocsHandler.cs:65`](../../ScoreTracker/ScoreTracker.EventCompetition/Application/MarchOfMurlocsHandler.cs:65)
maps "month the last season ended" to "month the next season ends" in quarters:

```
{12,1,2} → 3    {3,4,5} → 6    {6,7,8} → 9    {9,10,11} → 12
```

The switch is written longhand and **`6 => 9` was never typed**, so month 6 falls to `_ => 3`.
Spring 2026 ended June 30. On July 1 the cycle computed March 31 2026 — an end date three
months in the past — and then:

- `CycleMoM`'s idempotency guard only returns early when a **future**-dated MoM exists. Every
  row is past-dated, so it never trips.
- `oldEnd` is `MAX(EndDate)`, still June 30, so it recomputes the same wrong answer forever.
- `TryScheduleMoM` sees the newest MoM expired and republishes `CycleMoM`.

Once per day at 11:00 UTC since 2026-07-01. Measured blast radius:

| | |
|---|---|
| MoM tournament rows | 79 (8 real, **71 junk**) |
| Orphan `TournamentChartLevel` rows | **141,632** |
| Player data on junk rows | **zero** sessions, registrations, roles, photos |

The regression test added with the previous runaway fix covers March→June only, which is why
this hole survived.

**A second, latent variant:** `newEndDate` was built from `_dateTime.Now.Year` with the computed
month, so any cycle that ran *late* — missed season, downtime, manual trigger while behind —
minted another past-dated season and restarted the loop. Fixing only the switch arm would have
left this in place.

**Slice 0 fixed both**: the quarter map is arithmetic that cannot omit a month, and the season
advances a quarter at a time until it lies ahead of now, so a cycle five quarters behind lands
on the current quarter and creates one season rather than a backlog. §6 then removes the whole
*class* by making a duplicate season impossible in the schema.

### 2.2 MoM has never appeared in the nav — **fixed in Slice 0**

[`MarchOfMurlocsHandler.cs:114`](../../ScoreTracker/ScoreTracker.EventCompetition/Application/MarchOfMurlocsHandler.cs:114)
constructs each season with `isHighlighted: false`, and `ShellNav` renders its event links from
`Model.HighlightedEvents`. **Every MoM row in the database has `IsHighlighted = 0`**, including
Winter 2025 and Spring 2026. Only the static "March of Murlocs" menu link has ever existed.

This is why Spring 2026 ran a full quarter and received **zero submissions**, and why 39 days of
garbage went unnoticed.

### 2.3 Phoenix 2 sessions are graded on Phoenix 1 cutoffs

`ScoringConfiguration.Mix` defaults to `Phoenix` and MoM never sets it, so
`score.LetterGradeFor(Mix)` uses P1 floors for a P2 session. The floors moved below AAA, so P2
sessions are systematically **over**-scored — and because MoM runs
`ContinuousLetterGradeScale = true`, the interpolation is wrong across the whole band, not just
at boundaries.

### 2.4 The leaderboard ignores mix

[`EFTournamentRepository.cs:289`](../../ScoreTracker/ScoreTracker.EventCompetition/Infrastructure/EFTournamentRepository.cs:289)
selects every session for a tournament and ranks them together. P1 and P2 sessions currently
share one board — combined with 2.3, P2 players hold an unearned advantage.

### 2.5 Play timestamps are parsed and then discarded

`PiuGameGetRecentScoresResult.RecordedAt` is parsed to the second, with the site's own UTC
offset, at [`PiuGameApi.cs:714`](../../ScoreTracker/ScoreTracker.OfficialMirror/Infrastructure/Apis/PiuGameApi.cs:714).
[`OfficialSiteClient.cs:838`](../../ScoreTracker/ScoreTracker.OfficialMirror/Infrastructure/OfficialSiteClient.cs:838)
drops it when building `OfficialRecordedScore`, on exactly the path MoM's import uses.

### 2.6 The 1h45m rule is unenforceable

`TournamentSession.CanAdd` checks `TotalPlayTime + duration > MaxTime` — the **sum of song
durations**, not elapsed wall clock. Rest time is *modelled* as `MaxTime − TotalPlayTime`, i.e.
the system assumes the window was filled. A player could spread 30 charts across four hours and
be accepted. 2.5 is what makes this checkable for the first time.

### 2.7 Smaller things, fix in passing

- [`RecordTournamentSession.razor:227`](../../ScoreTracker/ScoreTracker/Pages/Competition/RecordTournamentSession.razor:227):
  `ValueChanged="v=>_editScore=v\n    =v"` — a wrapped-line typo that compiles.
- `StaminaTournament.razor` carries two hex literals (`#00FFFF`, `#FFFFFF`) in ApexChart series
  — a `UiColorTokenTests` violation waiting to be noticed.
- `StaminaTournament` and `RecordTournamentSession` inject `ITournamentRepository` **directly**,
  bypassing `IMediator`. The one hard architecture violation in the feature.
- `StaminaTournament.ShowUserCharts` divides by `Entries.Count() - 1`; a one-chart session throws.
- `MarchOfMurlocs.razor` computes `_hasQualifiers` in an N+1 mediator loop and never renders it;
  `_isLoading` is declared and never set.
- `RecordTournamentSession.DifficultyBubblePath` is dead — the `DifficultyBubble` component
  replaced it.

---

## 3. Locked decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | **MoM owns its own tables.** No dependence on `scores.Tournament`. | That table carries qualifiers, roles, co-op teams, randomizer micro-tournaments and MoM. Qualifiers leaves next; the table then dies. |
| D2 | **A filtered `UNIQUE(Year, Quarter)` is the real runaway fix.** | Makes a duplicate season *impossible* rather than merely fixed. The cycle becomes stateless — "does the current quarter's season exist?" — which also removes the late-catch-up variant. |
| D3 | **Four boards per season**: Phoenix·Singles, Phoenix·Doubles, Phoenix 2·Singles, Phoenix 2·Doubles. | Phoenix 1 is not dying, it is going offline-only. `MoMBoard` keys on MixId, so another mix is a row, not a migration. |
| D4 | **Phoenix 1 is manual-entry**; Phoenix 2 gets the import flow. | P1 loses its import path soon. |
| D5 | **Verification is deleted** — no photos, no approval queue, no admin notification, no `InPerson`/`Remote` distinction. | Nobody is verifying, and nobody has a reason to cheat yet. Zero photos have *ever* been attached to a MoM session. |
| D6 | **Videos survive as a showcase**, not a validation mechanism. YouTube links embed. | 29 of 62 historical sessions carry one; people want to flex. |
| D7 | **Migrate all legacy seasons.** Copy, never move. | 62 sessions of history. Copying keeps a botched migration recoverable and leaves `PhotoVerification` without orphans. |
| D8 | **Delta-only chart-level snapshots.** | Measured −60%. The equivalence is exact, not approximate (§9.3). |
| D9 | **`MoM*` naming**, not a generic "stamina ladder". | MoM is always going to be the only one. |
| D10 | **Hard block** on duration overflow; **soft warning** on timestamp span. | A session that *cannot* fit is invalid; a session that *looks* stitched together is a judgement call. |
| D11 | **PUMBILITY+ tracks the mix's own rating system.** | See §5. P1 needed heavy correction; P2 does not. |
| D12 | **Phoenix 2 ships admin-gated.** | Scoring gets min/maxed in a dedicated session before players see it. |
| D13 | **Empty ended seasons are pruned** when the next season is created. | Owner call. Keeps the table honest without a cron. |
| D14 | **The targets screen lives inside MoM.** | Self-encapsulated; it is a MoM page, not a pumbility page. |

### Explicitly rejected

- **Building PUMBILITY2+ on PUMBILITY+'s Phoenix 1 curve.** Rejected 2026-08-09: the quadratic
  base and the 22+ kicker exist to solve a *Phoenix 1* problem (see §5).
- **Lazy season creation** (materialise on first visit). The chart-level snapshot must freeze at
  season start to be fair, and `IChartScoringLevelRepository` is a current-state projection with
  no as-of query — a lazily-taken snapshot would give early and late submitters different balance.
- **A nullable / "combined" `ChartType` on a board.** Not needed: every legacy season maps to a
  single chart type (§7).

---

## 4. The balancing algorithm (Phoenix 1)

Previously undocumented — it existed only in code. **`CalculationType.Avalanche` is a selectable
formula in the enum and MoM does not use it.** MoM runs `CalculationType.Default` (all modifiers
multiplied) over four layers.

**1 — the `PumbilityPlus` base curve.** `BaseRating = 100 + 5(L−10)(L−9)` for L ≥ 10
([`DifficultyLevel.cs:155`](../../ScoreTracker/ScoreTracker.SharedKernel/ValueTypes/DifficultyLevel.cs:155));
levels 1–9 are 0 by default and hand-set to 10…90 in `CreateScoring()`.

**2 — grade modifiers, rewritten for stamina.** `ContinuousLetterGradeScale = true` interpolates
between rungs.

| Grade | Default | PUMBILITY+ |
|---|---|---|
| AAA | 1.10 | **1.00** |
| AA+ | 1.05 | **0.90** |
| AA | 1.00 | **0.75** |
| A+ | 0.90 | **0.50** |
| A and below | 0.4–0.8 | **0** |
| AAA+ … SSS+ | 1.15 … 1.50 | *unchanged* |
| Perfect Game | 1.50 | **1.60** |

**3 — the 22+ kicker**, applied in the MoM handler on top of the base:

| Level | 22 | 23 | 24 | 25 | 26 | 27 | 28 | 29 |
|---|---|---|---|---|---|---|---|---|
| Base | 880 | 1,010 | 1,150 | 1,300 | 1,460 | 1,630 | 1,810 | 2,000 |
| Bonus | +50 | +150 | +300 | +500 | +750 | +1,050 | +1,400 | +1,800 |
| **Effective** | **930** | **1,160** | **1,450** | **1,800** | **2,210** | **2,680** | **3,210** | **3,800** |

**4 — scoring difficulty, frozen per season.** At season creation each chart's effective level is

```
clamp(communityScoringLevel, nominal + 0.5, nominal + 1.5)
```

snapshotted into `TournamentChartLevel`. `GetBaseRating` then interpolates between
`LevelRatings[floor]` and `LevelRatings[ceil]`. The `− .5` offset in that interpolation means
**nominal+0.5 pays exactly the nominal level's rating and nominal+1.5 pays the full next
level's** — so a chart is never worth less than its number, and at most one level more.

Freezing per season is deliberate: mid-season re-rating cannot move the goalposts. **Preserve
this through any rewrite.**

Also set, and easy to miss: `AdjustToTime` is `false` in `CreateScoring()` and set to `true` by
the MoM handler · plates are all `1.0` (no effect) · `MinimumScore` is 0 · `SongTypeModifiers`
all 1.0 · `ChartModifiers` empty · **`StageBreakModifier` is 1.0 — stage break is deliberately
outside the algorithm** (§1), and PUMBILITY+ gets that by composing from a bare
`new ScoringConfiguration()` rather than from `PhoenixPumbilityScoring()` ·
`ChartTypeModifiers[CoOp] = 1.0` in `CreateScoring()` is a **no-op** restating the default, and
CoOp is filtered from the pool anyway.

---

## 5. PUMBILITY2+

**The principle (D11): PUMBILITY+ means *that mix's own pumbility, plus stamina adaptations*.**

Phoenix 1's pumbility rewards level so steeply that mashing high charts beats scoring well, so
PUMBILITY+ had to correct it — that is what the grade rewrite and the 22+ kicker are. **Phoenix 2
already solved scoring-versus-passing**, so its adaptation is nearly empty. Both configs are
then principled; neither is legacy.

```
PUMBILITY2+ = Phoenix2PumbilityScoring
            + ContinuousLetterGradeScale = true
            + AdjustToTime = true            (set by MoM, never in core — §9.5)
            + StageBreakModifier  = 1.0      (RESET — the base config carries 0.0 — §9.2)
            + LevelRatings[1..9]  = 0        (so CanAdd blocks sub-10 — §9.4)
            + non-matching chart types zeroed
            − no grade rewrite, no 22+ kicker, no PG bonus
```

Phoenix 2's base is `130 + 5L + 5·max(0, L−24)` — near-linear where Phoenix 1's is quadratic.

| Level | 10 | 20 | 22 | 25 | 26 | 29 |
|---|---|---|---|---|---|---|
| PUMBILITY+ (with kicker) | 100 | 650 | 930 | 1,800 | 2,210 | 3,800 |
| Phoenix 2 | 180 | 230 | 240 | 260 | 270 | 300 |

**This produces the intended incentive.** The L26/L20 base ratio is 270/230 = 1.174, so climbing
six levels pays whenever `grade(26) > 0.852 × grade(20)`:

| Play | vs a clean AAA level 20 |
|---|---|
| 26 at A+ (1.33 on a Single, 1.35 on a Double) | wins — ratio 0.94 / 0.96 |
| 26 at A (1.28) | wins, barely — 0.91 |
| 26 at F (0.90) | **loses** — 0.64 |

Phoenix 2 says *go as hard as you can still play competently*. Phoenix 1 with the kicker says
*go as hard as you can survive at all* — even A+ at 0.50 wins by 1.7×.

**Grade floors are identical from AAA (950,000) upward on both mixes.** The tables diverge only
below 950,000, which bounds the behavioural change precisely:

| | Phoenix 1 | Phoenix 2 |
|---|---|---|
| Zero cliff | < 825,000 | *none in this pass* |
| 0.50 → 1.00 ramp | 825k → 950k | *n/a — P2 uses its own 0.90 → 1.50* |

Implementation: keep the parameterless `ScoringConfiguration.PumbilityPlus` **exactly as it is**
and add a mix-aware factory beside it that only MoM calls (§9.5). Write P1's corrections as an
explicitly-labelled *Phoenix 1 anti-mash block* so the next reader does not carry them forward
by default.

---

## 6. Target schema

Five tables, replacing dependence on fifteen. Registered through EventCompetition's
`IDbModelContribution`; every table gets a row in
[DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md).

```
scores.MoMSeason
  Id          uniqueidentifier  PK
  Year        int               NULL   -- NULL for off-grid legacy seasons
  Quarter     tinyint           NULL   -- 1..4
  Name        nvarchar(100)            -- "Winter 2026"
  StartsAt    datetimeoffset
  EndsAt      datetimeoffset
  CreatedAt   datetimeoffset

  CREATE UNIQUE INDEX UX_MoMSeason_Quarter ON scores.MoMSeason (Year, Quarter)
      WHERE Quarter IS NOT NULL;          -- D2: the anti-runaway guarantee

scores.MoMBoard
  Id            uniqueidentifier PK
  SeasonId      FK -> MoMSeason
  MixId         uniqueidentifier
  ChartType     tinyint
  ScoringConfig nvarchar(max)            -- frozen serialized ScoringConfiguration
  UNIQUE (SeasonId, MixId, ChartType)

scores.MoMChartLevel                      -- balance snapshot, DELTA ROWS ONLY
  SeasonId, MixId, ChartId  PK
  Level       float

scores.MoMEntry
  Id                uniqueidentifier PK
  BoardId           FK -> MoMBoard
  UserId            uniqueidentifier      -- purge key (§9.7)
  TotalScore        int
  ChartsPlayed      int                   -- excludes zero-point plays on P1 (§1)
  RestTime          bigint
  AverageDifficulty float
  VideoUrl          nvarchar(500) NULL    -- D6: showcase, not validation
  SubmittedAt       datetimeoffset        -- tie-break: earliest wins
  UpdatedAt         datetimeoffset
  UNIQUE (BoardId, UserId)

scores.MoMEntryChart                      -- normalized; replaces the JSON blob
  EntryId, Ordinal  PK
  ChartId     uniqueidentifier
  Score       int
  Plate       nvarchar(20)
  IsBroken    bit
  SessionScore int
  BonusPoints int
  PlayedAt    datetimeoffset NULL         -- §2.5
```

**Why the scoring config is stored, not derived.** MoM's config is fully determined by
(mix, chart type) today, so rebuilding it on read is tempting — but then any tweak to the 22+
table retroactively rescores 2024. Four serialized rows per season is cheap insurance for a
competitive ladder.

**Why `MoMEntryChart` is normalized.** `GetLeaderboardRecords` currently calls `GetSession` per
player, each deserializing JSON and re-fetching charts — a live N+1 on every board render.
Normalized rows also give `PlayedAt` a home.

**Why the snapshot is keyed per (season, mix) rather than per board.** Singles and Doubles charts
are disjoint sets, so the row count is identical either way, but one key is simpler and a third
chart type would then cost nothing.

---

## 7. Migration

**Copy, never move (D7).** The old `Tournament` rows stay in place until that table is archived
wholesale per the never-drop-tables standard.

Eight legacy tournament rows collapse into **five seasons and eight boards** — and every one maps
to a single chart type, so no "combined" board is needed:

| Legacy tournament | Season | Year/Qtr | Board | Sessions |
|---|---|---|---|---|
| March of Murlocs Practice | Practice | NULL | Phoenix · Double | 2 |
| March of Murlocs | March of Murlocs | NULL | Phoenix · Double | 17 |
| MoM 2 – Singles | \ MoM 2 | NULL | Phoenix · Single | 10 |
| MoM 2 – Doubles | / (same dates) | NULL | Phoenix · Double | 17 |
| Winter 2025 – Singles | \ Winter 2025 | 2025 / 1 | Phoenix · Single | 5 |
| Winter 2025 – Doubles | / | | Phoenix · Double | 11 |
| Spring 2026 – Singles | \ Spring 2026 | 2026 / 2 | Phoenix · Single | 0 |
| Spring 2026 – Doubles | / | | Phoenix · Double | 0 |

Notes for whoever writes it:

- **Quarters are NULL for everything before Winter 2025.** MoM 2 ran June 8 → Aug 8, straddling
  Q2/Q3; the quarterly cadence only starts at Winter 2025.
- **Spring 2026 will not exist** if Slice 0's prune ran — it is an ended season with zero
  entries (D13).
- The 2023 "March of Murlocs" contains **one stray Singles play** among 566 Doubles. Copy it
  faithfully inside its entry rather than inventing a rule.
- Verification data is not carried across except `VideoUrl` (D6). Zero photos exist to lose.

---

## 8. Slices

Ordered so that UI decisions can still move the schema.

| Slice | Contents | Depends on |
|---|---|---|
| **0 — Stop the bleeding** ✅ *code landed* | Quarter map replaced with arithmetic; season year derived from the previous season with catch-up to the current quarter; seasons created highlighted. `MarchOfMurlocsHandler` only — no schema change. **Still owner-run: `mom-cleanup.sql`, including the empty-season prune.** | — |
| **1 — Settle on mocks** | UX/UI pass: season page, board page, record flow, targets screen. Design only, no code. | 0 |
| **2 — Own the data** | Five tables, model contribution, purge manifest, repository, migrate five seasons, cycle rewritten onto `MoMSeason`. Pages repointed, visually unchanged. | 1 |
| **3 — Per-mix boards** | Four boards, mix-correct grading and snapshots, PUMBILITY2+, Phoenix 2 admin-gated. | 2 |
| **4 — UX build** | Build Slice 1's mocks. Verification removal, rapid entry, paste box, CSV. | 3 |
| **5 — Timestamps** | Plumb `RecordedAt`, auto-select the contiguous run, soft span warning. | 4 |
| **separate session** | Phoenix 2 scoring min/max (≈6 rounds, owner estimate). | 3 |

**Slice 0 is urgent.** The job fires daily at 11:00 UTC and each run adds roughly 3,700 rows.

`mom-cleanup.sql` is dry-run by default, has been tested end to end, and refuses to delete any
season carrying player data.

---

## 9. Traps

### 9.1 `IsMoM = 1` is load-bearing in the cleanup

`BITE 7 - Co-Op` is a **real** tournament that also has `EndDate < StartDate`. Never loosen the
cleanup predicate to inverted-dates alone.

### 9.2 PUMBILITY2+ inherits a stage-break rule MoM does not want

**Reset `StageBreakModifier` to `1.0` when composing PUMBILITY2+.**

Both official pumbility configs — `PhoenixPumbilityScoring` *and* `Phoenix2PumbilityScoring` —
set `StageBreakModifier = 0.0`, which is correct for them: piugame does not count broken plays
in the pumbility pool. MoM's rule is the opposite and deliberate (§1): passing and scoring are
different concepts, a failed SS was not a mash, and a broken play is worth whatever its score is
worth.

Phoenix 1 never hit this because `CreateScoring()` composes from a bare
`new ScoringConfiguration()` (default `1.0`), not from the official config. **PUMBILITY2+ does
compose from the official config** — that is the whole point of D11 — so it inherits `0.0` and
will silently start zeroing broken plays unless the reset is explicit.

Worth a test: a broken play and a passed play at the same score must score identically on both
mixes.

### 9.3 Delta-only snapshots are an exact equivalence, not an approximation

Do not "fix" the missing rows back in. With an override of `L + 0.5`, `GetBaseRating` computes

```
LevelRatings[L] + (LevelRatings[L+1] − LevelRatings[L]) × (L + 0.5 − 0.5 − L)  =  LevelRatings[L]
```

which is byte-identical to the no-entry fallback path. Measured on Spring 2026: 4,425 rows →
1,785 real deltas.

Note also that on Phoenix 2 the snapshot shifts a chart by at most one level, worth ~2% on a
near-linear curve versus ~15% at L22 on Phoenix 1. `MoMChartLevel` is effectively a Phoenix 1
table going forward.

### 9.4 Sub-10 charts are addable-but-worthless on Phoenix 2

P2's zero for `level < 10` is a hard `return 0` inside `GetScore`, **not** in the level table, so
`GetScorelessScore` still returns `Phoenix2BaseRating(5) = 155`. `TournamentSession.CanAdd` gates
on `GetScorelessScore(chart) == 0`, so the picker will happily offer a level 5 that scores
nothing. Fix by setting `LevelRatings[1..9] = 0` in the P2 MoM config so `CanAdd` blocks them
itself.

### 9.5 `AdjustToTime` must stay `false` in core

`ScoringConfiguration.PumbilityPlus` has three consumers and only MoM wants time-adjustment:

- [`MarchOfMurlocsHandler.cs:97`](../../ScoreTracker/ScoreTracker.EventCompetition/Application/MarchOfMurlocsHandler.cs:97) — MoM, sets it true locally
- [`PlayerRatingSaga.cs:179`](../../ScoreTracker/ScoreTracker.PlayerProgress/Application/PlayerRatingSaga.cs:179) — writes the **stored** `PhoenixRecordStats.PumbilityPlus`
- [`PhoenixScoresController.cs:160`](../../ScoreTracker/ScoreTracker/Controllers/Api/PhoenixScoresController.cs:160) — exposes `PumbilityPlus` on the **public v1 API**

Hoisting it into the factory would silently re-value every stored stat and change numbers
partner tools consume. The contract test pins the wire *shape*, not the values, so nothing would
catch it.

### 9.6 Never cross-wire the official mirror with the tournament formula

`Phoenix2PumbilityScoring` mirrors piugame's own number and must stay discrete-grade /
additive-plate — `/Pumbility` and the reconciliation probes compare it against the live site.
PUMBILITY2+ is a tournament formula. Two configs, never merged. Comment both.

### 9.7 Ratchets that will go red if missed

- `MoMEntry` must join the vertical's `UserOwned` purge manifest with `UserId` as the purge key,
  or `AccountPurgeCoverageTests` fails.
- The `IDbModelContribution` must be listed in `VerticalModelContributions.All()`, or scaffolded
  migrations silently drop every MoM table.
- New localization keys land in **all nine locales**, inserted in alphabetical position — and
  `en-ZW` uses only the Murloc alphabet.
- No color literals in `Pages/`/`Components/`; the two ApexChart hex values in
  `StaminaTournament.razor` need tokens.

### 9.8 EventCompetition becomes the MoM vertical

Once qualifiers moves out, what remains is roles, invites, registrations and co-op teams (all
qualifier-side) plus randomizer settings (which belong to `Randomizer`). EventCompetition does
not survive the split as a distinct thing — it *becomes* MoM. Land the tables inside it now and
do any assembly rename after qualifiers leaves, so a mechanical rename never overlaps a schema
change.

---

## 10. Open

1. **Phoenix 2's "A or below" rule** — deferred to the scoring session. `MinimumScore` already
   exists (`if (score < MinimumScore) return 0`) and is a single knob that adds a floor without
   touching grade multipliers.
2. **Perfect Game on Phoenix 2** — left out this pass. Consequence to be aware of: with
   `PgLetterGradeModifier` equal to SSS+'s 1.50, continuous interpolation from 995,000 to
   1,000,000 is **flat**, so the top 5,000 points of score are worth nothing. One value to change
   once the targets screen shows how it plays.
3. **Targets screen shape** — locked to living inside MoM (D14); the page itself is Slice 1.
   Starting point is `AutoBuildSessionHandler` with a per-chart contribution breakdown, pointed
   at a chosen mix's scores.
