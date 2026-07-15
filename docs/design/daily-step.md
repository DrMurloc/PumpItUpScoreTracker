# Daily Step

**Status**: scoped (2026-07-12), owner decisions locked; ready to execute. A catalog-walk widget
(D12/D18 of [home-page-widgets.md](home-page-widgets.md)) — its own PR. Builds **inside the
`ScoreTracker.WeeklyChallenge` vertical**.

A **per-mix shared daily chart** with a mini leaderboard. Everyone on a mix competes on the same chart
each day; the board resets at **midnight ET**. Once a week, on a (pseudo-)random day, it's **Limbo
Day**: an easy chart (level ≤ 15) where the **lowest passing score wins**.

---

## 1. Locked decisions

From D12 (home-page-widgets.md) plus the owner scoping pass 2026-07-12:

| # | Decision |
|---|---|
| L1 | Name **Daily Step**. Widget `TypeId` = `daily-step`. Lives in the `WeeklyChallenge` vertical. |
| L2 | **Per-mix boards** (parallel, like Weekly) — Phoenix + Phoenix 2. Not one global chart. |
| L3 | Normal day: chart **level 16–24**, **singles/doubles only** (no co-op), highest score wins. |
| L4 | **Limbo Day**: once a week on a pseudo-random day, chart **level 1–15** (1–9s allowed), **lowest passing (non-broken) score wins**. |
| L5 | Resets at **midnight ET** (both Daily Step *and* the existing Weekly board — see §6, a regression the Hangfire extraction introduced). |
| L6 | **No chart-history / dedup table** — repeats across days are fine. **Keep per-user score history** (`UserDailyStepPlacing`) for a future view on the Weekly-charts rebuild. ✅ **Shipped** — that view is the "Your Daily Step History" section of the rebuilt `/WeeklyCharts` (weekly-charts-overhaul.md §4.5), reading `UserDailyStepPlacing` through `GetUserDailyStepHistoryQuery`. |
| L7 | **No standalone daily Discord post.** Instead, add a **Daily Step line to the community session-snapshot card** (CommunitySaga), mirroring its existing weekly-placement lines. |
| L8 | Limbo scoring is fed by the **recent-scores import hook** (§3), not by rebuilding the import to carry all attempts for every chart. |
| L9 | Distinct from the **per-player daily challenge** (a separate future session — do not conflate). |

---

## 2. Data + vertical shape (inside `WeeklyChallenge`)

Daily Step is another site-run rotating challenge board — same bounded context as Weekly Challenge —
so it reuses that vertical's DbContext contribution, `IPlayerStatsReader`/`IUserReader`, placement
policy, and leaderboard dialog. No new assembly.

### 2.1 Tables (new rows in DATABASE-SCHEMA.md), keyed by `MixId` (L2)

| Table | Columns (sketch) |
|---|---|
| `DailyStepChart` | Id PK, MixId, ChartId, ForDate, IsLimbo bit, ExpirationDate. 0–1 live row **per mix**. |
| `DailyStepEntry` | Id PK, UserId, ChartId, MixId, Score, Plate, IsBroken, CompetitiveLevel, Photo? — unique (UserId, ChartId, MixId). Mirror of `WeeklyUserEntry`. |
| `UserDailyStepPlacing` | Id PK, UserId, ChartId, MixId, ForDate, IsLimbo bit, Place, Score, Plate, IsBroken, CompetitiveLevel — written at rotation (L6). The retained history. |

No `PastDailyStepChart` (L6 — dupes accepted; and Limbo-day selection is stateless, §2.5).

### 2.2 Contracts (`WeeklyChallenge/Contracts/`, `[ExcludeFromCodeCoverage]`)

- **Queries**: `GetDailyStepQuery(mix)` → `DailyStepBoard(ChartId, ForDate, IsLimbo, ExpirationDate)?`;
  `GetDailyStepEntriesQuery(mix)` → live entries; `GetDailyStepPlacementQuery(userId, mix)` →
  `(Place, Total, IsLimbo)?` (the 1×1 widget **and** the community card read this).
- **Message (bus trigger)**: `RotateDailyStepCommand(mix = Phoenix)` in `Contracts/Messages/`.
- Entry shape reuses `WeeklyTournamentEntry` (identical fields, same vertical).

### 2.3 Cross-boundary contracts (shared, so OfficialMirror can feed the board without a vertical→vertical ref)

- **Event** `DailyStepScoreObservedEvent(EventId, OccurredAt, UserId, Mix, ChartId, BestScore, BestPlate,
  BestIsBroken, LowestPassScore?, LowestPassPlate?)` in **`Domain/Events/`** (same home as
  `ScoreImportCompletedEvent`; both the OfficialMirror publisher and the WeeklyChallenge consumer
  reference Domain, no vertical coupling). Carries **both** the best and the lowest-passing recent
  score for one chart; the consumer, which owns the board, picks by `IsLimbo`.
- **Reader port** `IDailyStepReader.GetCurrentChartIds(mix)` in **`Domain.SecondaryPorts/`** →
  the daily chart id(s) for a mix (0–1). Lets the importer learn "which chart is today's daily" the
  same way high-traffic reads use `IScoreReader`/`IPlayerStatsReader`. Implemented by
  `EFDailyStepRepository` in WeeklyChallenge.

### 2.4 Saga (`WeeklyChallenge/Application/DailyStepSaga.cs`, internal)

One feature-grouped class:

- `IConsumer<RotateDailyStepCommand>` — the daily rotation (§2.5).
- `IConsumer<DailyStepScoreObservedEvent>` — the **single intake path** (§3). Reads its
  `DailyStepChart` (knows `IsLimbo`); upserts the caller's `DailyStepEntry`: **keep-max** of `Best*`
  on a normal day, **keep-min** of `LowestPass*` on a Limbo day (skip when `LowestPass` is null — no
  passing run). Competitive level via `IPlayerStatsReader`, exactly like `WeeklyTournamentSaga`.
- `IRequestHandler` for the three queries.

Wiring: `AddConsumer<DailyStepSaga>()` in `AddWeeklyChallengeConsumers`; repo + reader bindings in
`AddWeeklyChallenge`; three table mappings in `WeeklyChallengeModelContribution`. **Add `DailyStepSaga`
to the explicit consumer allowlist in `VerticalBoundaryTests`** (it's a ratchet list, not a scan).

### 2.5 Rotation (`RotateDailyStepCommand` consumer)

1. If today's `DailyStepChart` for the mix is unexpired → no-op (idempotent; safe re-fire).
2. Snapshot the finishing board into `UserDailyStepPlacing` — places computed with the finishing
   chart's own direction (asc for Limbo, desc otherwise). Then clear `DailyStepChart` + `DailyStepEntry`
   for the mix.
3. `IsLimbo = DailyStepLimboPolicy.IsLimboDay(today)` — **deterministic per ISO week** (§2.6): no
   persistence, no RNG state, exactly one Limbo day/week, a different weekday each week.
4. Pick the chart: bucket = **S/D**, level **16–24** normal / **1–15** Limbo (L3/L4). No dedup (L6).
   `IRandomNumberGenerator` picks one.
5. Insert the new `DailyStepChart` with the next reset as `ExpirationDate` (§6 — next midnight ET).

The recurring job publishes `RotateDailyStepCommand(mix)` for **each** supported mix (Phoenix,
Phoenix 2) — daily cadence can't rely on the manual per-mix trigger the Weekly page uses.

### 2.6 Two pure domain policies (DomainTests-pinned)

- **`DailyStepLimboPolicy`** — `LimboDayOfWeek(isoYear, isoWeek)` = a stable arithmetic hash → one
  weekday; `IsLimboDay(date)` compares it to the date's ISO weekday. Stateless, so it survives L6's
  "no history table." Deterministic ≠ persisted-random: a determined user could in principle
  reverse-engineer the weekday, but it varies weekly and needs no oracle. Swap to a persisted random
  pick later if that ever matters.
- **Placement** — normal day reuses `WeeklyChartSuggestionPolicy.ProcessIntoPlaces` (score desc). Limbo
  adds **`ProcessIntoPlacesAscending`** beside it: passing entries only (drop `IsBroken`), lowest score
  = place 1.

---

## 3. Limbo intake — the recent-scores hook (L8)

Confirmed feasible: `OfficialSiteClient.GetRecordedScores` (runs on **every** official import) already
fetches the recent-plays feed and groups it per chart —
[OfficialSiteClient.cs:298](../../ScoreTracker/ScoreTracker.OfficialMirror/Infrastructure/OfficialSiteClient.cs)
`chartGroup` holds **all individual recent plays** for a chart (line 307 collapses them to best). The
recent-plays feed is the only source of a deliberate low-but-passing run; the paginated best-score
import can never carry it.

The hook, at that grouping point:

- Inject `IDailyStepReader` into `OfficialSiteClient`; read the mix's daily chart id(s) once per import.
- For a `chartGroup` whose `chart.Id` is a daily chart, compute `BestScore` (already there) **and**
  `LowestPassScore = chartGroup.Where(s => !s.IsBroken).Min(s => s.Score)` (null if no pass), and
  publish `DailyStepScoreObservedEvent`. One extra event per daily chart the player recently played —
  targeted, not "all attempts for everything" (L8).

`DailyStepSaga` consumes it and applies the mode (§2.4). **Known v1 limitation** (mirrors Weekly):
manual single-score records don't reach the board — the official import is the only intake. Fine for
v1; revisit if players want to log a Limbo run by hand.

---

## 4. Widget (Web `Components/HomeWidgets/`)

`DailyStepWidget.razor` + `DailyStepConfig` + `DailyStepConfigPanel.razor`, registered in
`WidgetRegistry` under **Compete**, `SupportedMixes` = Phoenix + Phoenix 2. Declares the four
render-contract params (`Widget`, `EffectiveMix`, `EditMode`, `OnChartClick`).

- **Sizes**: **1×1** (today's chart art + your place/total + a Limbo badge on Limbo days) and **2×1**
  (art card + top rows + the trophy that opens the leaderboard dialog).
- **Config v1**: mix scope only (`Follow current mix` / Phoenix / Phoenix 2), like `WeeklyConfig`.
- **States**: no board → "Today's Step posts at midnight." Freshness "resets in …" from `ExpirationDate`.
  Chart rows raise `OnChartClick` (the shared `ChartDetailsDialog` rule).
- **Leaderboard**: generalize `WeeklyLeaderboardDialog` to accept entries + a sort direction (its
  round-3 note already earmarked it for Daily Step), so Limbo renders **ascending** and dedupes the code.
- l10n keys land in all nine locales in the same commit.

---

## 5. Community card (L7)

`CommunitySaga`'s session-snapshot card already renders weekly-placement lines
([CommunitySaga.cs:496](../../ScoreTracker/ScoreTracker.Communities/Application/CommunitySaga.cs)).
Add a parallel **Daily Step** achievement line reading `GetDailyStepPlacementQuery(userId, mix)` — the
caller's place/total on today's chart, with a Limbo tag on Limbo days. Best-effort like the weekly read
("a flex, not a fact the card owes anyone"); a failure costs the line, never the card.

---

## 6. Weekly-reset timing fix (L5 — regression)

**Diagnosis**: `DateTimeOffsetAccessor.Now` is server-local = **UTC** on Azure. The Weekly board
expires Monday 03:00 UTC, but the reset users *see* is gated by the `update-weekly-charts` cron at
`0 9 * * *` = **09:00 UTC = 5am EDT** — the Hangfire-era cron slot is the bug. Owner wants **midnight ET
Monday**.

**Fix**: move the rotation to the midnight-ET UTC slot (**OPEN-T** — exact slot below) and set the
board `ExpirationDate` to match that moment (so the widget countdown is honest), instead of the
current 03:00. The daily job shares the same slot; on Mondays both the daily chart and the weekly board
reset at midnight ET together.

**OPEN-T**: fixed-UTC cron can't hit midnight ET year-round.
- `0 5 * * *` — **midnight EST**, 1am during EDT. Matches the codebase's "EST = UTC-5" convention
  (Program.cs comment) and every other job's drift. *(rec)*
- `0 4 * * *` — true **midnight EDT now**, 11pm during EST.

Also **OPEN-U**: land this weekly fix **in the Daily Step PR** (shared cron + reset concept) or split it
into a tiny standalone fix PR? (rec: same PR, its own commit, clearly flagged.)

---

## 7. Commit plan (one PR)

Dependency spine is C1 → C2 → C3; C4–C8 each depend only on C3 and are independently orderable —
this sequence keeps the scheduling edits adjacent and reads as foundation → brain → it rotates →
scores flow → visible → announced → weekly bug fix. End-to-end live after C6.

| C | Content |
|---|---|
| C1 | **Data foundation**: entities `DailyStepChart`/`DailyStepEntry`/`UserDailyStepPlacing` + `IDailyStepRepository` (vertical) + `IDailyStepReader` (Domain port) + `EFDailyStepRepository` + model-contribution mappings + repo/reader DI bindings + `DailyStep` migration + DATABASE-SCHEMA rows |
| C2 | **Domain policies**: `DailyStepLimboPolicy` + `ProcessIntoPlacesAscending` (on `WeeklyChartSuggestionPolicy`) + DomainTests |
| C3 | **Contracts + saga**: `GetDailyStep`/`Entries`/`Placement` queries + DTOs, `RotateDailyStepCommand`, `DailyStepScoreObservedEvent` (Domain/Events), `DailyStepSaga` (rotate + single intake + query handlers) + consumer wiring + `VerticalBoundaryTests` allowlist + component tests |
| C4 | **Rotation job**: `RecurringJobRunner.PublishRotateDailyStep` (per mix) + Program.cs `rotate-daily-step` @ `0 5 * * *` + SCHEDULED-JOBS row |
| C5 | **Official-import Limbo hook**: inject `IDailyStepReader` into `OfficialSiteClient`, publish `DailyStepScoreObservedEvent` for daily charts; import test |
| C6 | **Widget**: `DailyStepWidget` + config + panel + registry entry + generalized `LeaderboardDialog` (update its one consumer) + l10n ×9 + site.css + UX-GUIDELINES note |
| C7 | **Community card**: Daily Step placement line in `CommunitySaga`'s snapshot card via `GetDailyStepPlacementQuery` |
| C8 | **Weekly reset fix (bundled, isolated)**: Program.cs `update-weekly-charts` `0 9`→`0 5` + `WeeklyTournamentSaga` expiration to match + SCHEDULED-JOBS update + saga test tweak + home-page-widgets §4 readiness flip 🔨→✅ |

---

## 8. Leaderboard beef-up (owner, 2026-07-13; mock 09bf3592)

The widget renders the board inline, not just your standing.

- **1×1**: a **split** (a list clipped in the square) — left: a short top three, each `place · name ·
  letter grade` ("PG" instead of the grade on a Perfect Game; full score on hover). Right: your standing
  — `#place` + grade/score, or **"Not played" + how many are on the board** when you haven't. Community
  green / you blue tints carry across.
- **1×2 / 1×3**: the full board (up to 50), scrollable (`dash-scroll`), with your row **always floating** at
  the bottom below the board (the board flex-scrolls, the row stays put). Supported sizes moved from
  `1x1/2x1` to **`1x1/1x2/1x3`** — the layout keys on `Rows > 1` (tall), not columns.
- **Row** = place (rarity ramp) · avatar + flag + name · **grade + score**. Plate and the source tags stay
  off the card; they live in the dialog.
- **Glows**: your row blue (`--daily-you`), community members green (`--daily-community`) — shared
  communities minus `IsRegional` minus the `"World"` system community, unioned in-widget from
  `GetMyCommunitiesQuery` + `GetCommunityMembersQuery`.
- **Controls** ride the `WidgetHeaderSlot` (from the Quick Record merge, PR #147): Record — now a small
  MudDialog — and the full-board `LeaderboardDialog`. That dialog is the rich view: plate, ✓ verified /
  *manual* tags, up to 50 rows (its `MaxPlaces` is parameterized so Weekly stays 10). A `HeaderSlot == null`
  body fallback keeps the controls reachable in bare renders.
