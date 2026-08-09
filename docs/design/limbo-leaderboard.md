# The limbo leaderboard — design

How low can you clear it. A per-chart board ranking the **lowest passing score** ascending, lit only
on charts flagged for it by hand, as an extra scope chip beside World / Region / Community / Rivals /
Competitive Peers / Official.

The seed charts are `Gargoyle - FULL SONG - S21` and `Yeo rae a S1` — the two the official game
already hangs low-score titles off (`PERFECT BREAKER`, ≤444,444; `HUMAN METRONOME`, ≤180,000; and
`GOD OF CONTROL`, clear with 160+ misses). **The titles are the inspiration, not the specification.**
This board has no threshold: it ranks whatever the lowest pass is, and a chart is on it because
somebody decided it should be, not because the game agreed.

---

## 1. Why the record table cannot answer this

`PhoenixRecord` holds one row per (user, chart, mix): the **best**. `BestAttemptPolicy.Beats` puts a
pass over a break and then takes the higher score, so a deliberate 300k clear never displaces a 900k
one. Manual entry is the only path that lowers a record (score-truth-model D9), and the next official
import puts the high score straight back, because the best page is truth (D3). A manual lowball
erases itself.

So a board built on the record table answers *"who has the worst personal best"* — on Gargoyle FS
S21, a list of people who scraped one clear and never came back. That is the opposite of the feat.

## 2. Nothing new has to be kept

`ScoreEventJournal` is append-only, one row per observed play, and
[OfficialSiteClient](../../ScoreTracker/ScoreTracker.OfficialMirror/Infrastructure/OfficialSiteClient.cs)
already sends **every dated recently-played play** through `RecordObservedPlaysCommand` on every
import — unconditionally, all charts, no record change required, judgement counts included. A
deliberate low pass is being written to production today with `IsBest = 0`.

The flag table therefore gates **which charts show the chip**, not what gets retained. No consumer, no
projection, no recompute, no backfill job.

## 3. Decisions

- **D1 — a chart is on the board because a row says so.** `scores.LimboChart` is (ChartId, MixId).
  Rows are inserted by hand-run SQL. There is no admin screen, no seeding, no derivation from the
  title list — the title charts are where this starts, not what defines it.

- **D2 — mix-specific.** Phoenix and Phoenix 2 share chart ids. Flagging a chart on one mix says
  nothing about the other, and the board never mixes rows across them.

- **D3 — one row per player: their lowest passing score.** `MIN(Score)` over journal rows for the
  chart where `IsBroken = 0` and `Score IS NOT NULL`. Every observed pass is a candidate, whatever it
  scored — there is no floor and no threshold.

- **D4 — a break is not a limbo run.** Failing with a low score is not the achievement; surviving
  with one is. Broken rows are excluded outright, which also keeps `Score = 0` walk-off residue off
  the board.

- **D5 — the population is every public user.** Filter `User.IsPublic` directly in the query rather
  than reading through the World community. World is equivalent in production today (verified
  2026-08-08: 820 public users, 0 missing from World), but a fresh or test database has **no World
  row at all** — system communities seed on first join — so the community route needs fixture setup
  that the direct filter does not. Non-public rows are dropped, not renamed "Anonymous", matching
  what the World scope shows.

- **D6 — the board is capped, not filtered by source.** A player whose only journaled pass came from
  the best-page walk or the 2026-06 backfill appears at their best score. Rather than filter those
  out — which would also drop a genuine first-play lowball, the purest case there is — the query
  takes `TOP N` ascending and the meaningless tail never renders.

- **D7 — rank colour and score furniture are unchanged, deliberately.** `ThemeScales.RarityStyle`
  reads a percentile derived from *place*, so #1 (the lowest score) takes the top of the rarity ramp,
  and every row keeps the letter grade its score earns — so the board reads as a gold-crowned wall of
  F grades. Both were raised as possible bugs and both are the point (owner, 2026-08-08).

  **Rows carry no plate**, which is the one place the board differs from its siblings. Settled at
  implementation: `ScoreEventJournal.Plate` is `nvarchar(max)`, so it cannot ride the covering index
  without a key lookup per row, and picking the plate belonging to the *minimum-score* row needs a
  correlated per-group aggregate that does not translate. It costs nothing to lose — a limbo pass is
  a Rough Game by construction, and `ScoreBreakdown` renders no plate image when it has none, so the
  row simply reads score-and-grade.

- **D8 — no footnote.** The Official scope explains its snapshot date; this one explains nothing.
  Players will work out that a run has to be imported to count (owner, 2026-08-08).

## 4. What the numbers say

Prod-synced local database, 2026-08-08. `ScoreEventJournal`: 1,104,414 rows, 548.7 MB.

The journal's three existing indexes all lead with `UserId`, so a cross-user per-chart read has no
seek. Measured, `WITH (ONLINE = ON)`:

| Include list | Build | Size |
|---|---|---|
| `UserId, Score, IsBroken, Source, OccurredAt` | 6,429 ms | 140.5 MB |
| **`UserId, Score, IsBroken, OccurredAt`** | **1,445 ms** | **111.8 MB** |
| `UserId, Score, IsBroken` | 2,912 ms | 101.0 MB |

Build times are cache-state noise, not a width signal — the middle option is bigger than the lean one
and built in half the time. The finding is that all three are single-digit seconds. Drop was 4 ms.

`Source` is dropped from the include list: `nvarchar(32)` for 29 MB, and D6 means nothing filters on
it. `OccurredAt` is kept for 11 MB, because the component's tie loop orders on `RecordedAt` and ties
are plausible at the low end.

Query against the busiest chart in the journal (975 rows, 899 distinct users): **178 ms** unindexed,
**16–24 ms** indexed. Local is Enterprise Developer on NVMe against Azure SQL in production — do not
read those as prod timings. The real assurance is that
[20260710031131_PhoenixRecordCohortReadIndex](../../ScoreTracker/ScoreTracker.Data/Migrations/20260710031131_PhoenixRecordCohortReadIndex.cs)
put the same operation — online covering index, comparable table, same annotation pair — through the
deploy bundle on 2026-07-10.

## 5. Caching

Two caches, different lifetimes, because they answer different questions at different rates.

| Cache | Key | TTL | Eviction |
|---|---|---|---|
| Flag set | `LimboCharts__{mix}` | **5 min** | **None available** — see below |
| Board | `LimboBoard__{mix}__{chartId}` | 24h | On any journal write for that (chart, mix) |

The flag set is read on every chart view, to decide whether the chip renders at all. It is written
**only by hand-run SQL**, which the application cannot observe — so there is no hook to bust it on.
Its TTL is therefore short rather than long: five minutes, so flagging a chart lights its chip on the
same visit rather than needing a restart. The read is a handful of rows off a two-column table, so
the shorter window costs nothing worth measuring, and the alternative — a 24h entry with no way to
clear it — turns every INSERT into "I ran it and nothing happened."

The board cache does have a hook. `RecordObservedPlaysHandler` is where limbo runs actually land, and
`UpdatePhoenixRecordHandler` journals a first pass that may be someone's only one — both evict the
key. Eviction is a `Remove` on a key that usually does not exist, so no flag check is needed on the
write path. Key formats live in a shared `LedgerCacheKeys`, following `OfficialCacheKeys`: a format
private to its reader is exactly what leaves an evicting writer guessing.

**Not `ScoreImportCompletedEvent`**, though it looks made for this — it is already on the bus, already
consumed by two sagas, and its payload names every chart in the recently-played window whether or not
the run beat anything. Two things rule it out. It is **published before the rows it describes are
written**: `OfficialSiteClient` publishes it inside `GetRecordedScores`, and `OfficialLeaderboardSaga`
only sends `RecordObservedPlaysCommand` after that call returns — so on the in-memory transport the
consumer can evict before the journal write lands, and a click in that window caches a pre-write board
for 24 hours. And it is **official-import only**, published from that single call site, so a manual or
CSV entry — the one path that can lower a record and move the MIN — would never evict. Evicting from
the write handlers costs two lines, runs after the writes, and covers every source.

Both are per-instance `IMemoryCache`. On a scale-out the worst case is one instance serving a board up
to 24h stale, which for this feature is not worth a distributed cache.

## 6. Technical scope

| Layer | Work |
|---|---|
| SharedKernel | none |
| Domain | none — the flag repo is a vertical-internal port, and Web reaches it through MediatR |
| Application | none |
| **ScoreLedger** | `Contracts/Queries/`: `GetLowestPassingScoresQuery(ChartId, Mix)` returning the existing `UserPhoenixScore` (no new DTO) and `GetLimboChartsQuery(Mix)`. `Domain/`: `ILimboChartRepository`, plus one method on `IScoreJournalRepository`. `Application/`: two handlers, `LedgerCacheKeys`, eviction in the two write handlers. `Infrastructure/`: `LimboChartEntity`, `EFLimboChartRepository`, the MIN query. `Wiring/`: one DI line, the table mapping, the index |
| Data | one migration — `scores.LimboChart` and `IX_ScoreEventJournal_ChartId_MixId` |
| CompositionRoot | none — `ScoreLedgerModelContribution` is already in `VerticalModelContributions.All()` |
| **Web** | [ChartLeaderboardScopes.razor](../../ScoreTracker/ScoreTracker/Components/ChartLeaderboardScopes.razor) only. No CSS, no host changes |

### The component

Additive except one edit: the ranking loop hardcodes `OrderByDescending`, and has to become
direction-aware. Everything else is a new arm on an existing switch — enum member, `Scopes` entry,
`ScopeLabel`, `IsAvailable` (reading the cached flag set, cheap enough to load eagerly unlike the
Official chip's board read), and a `ScoresForScopeAsync` case.

All three hosts — `ChartDetails.razor`, `ChartDetailsDialog.razor`, `PlayerSessions.razor` — pass
`InitialScope` explicitly, so none of them change.

Rows reuse `.weekly-lb-*` and `ScoreBreakdown`; the chip reuses `.cld-chip`. No new CSS class, no new
colour token, nothing added to the `UiColorTokenTests` allowlist. `.cld-scopes` is `flex-wrap: wrap`
with `white-space: nowrap` chips, so a seventh chip wraps rather than overflowing — it costs a row of
height at 390px, which is accepted.

Localization is one key, `Lowest Passing`. The existing empty state — *"Nobody here has played this
yet."* — reads correctly for an empty limbo board, and D8 removed the footnote.

### Ratchets not tripped

`LimboChart` carries no `*UserId` column, so `AccountPurgeCoverageTests` never sees it: no manifest
entry, no exemption, and the journal rows the board reads are already covered by ScoreLedger's purge.
No new consumer (MassTransit tripwire), no new page (`RenderModeDeclarationTests`), no colour literal,
and the message taxonomy is satisfied by `*Query : IQuery<T>` under `Contracts/Queries/`.

## 7. What this is not

- **Not a competition.** No verification, no evidence, no approval queue. If the board ever needs to
  settle an argument, that is the qualifiers shape and a different design.
- **Not retroactive before 2026-07-30.** Non-best observation journaling began then; every earlier
  row is `IsBest = 1`. The board starts shallow and fills going forward.
- **Not complete, ever.** `recently_played.php` is fetched as a single page with no pagination
  ([PiuGameApi](../../ScoreTracker/ScoreTracker.OfficialMirror/Infrastructure/Apis/PiuGameApi.cs)).
  Play it, don't import, it is gone — unfixable after the fact, and the whole support burden of the
  feature.

## 8. Docs to update in the same PR

| Doc | Change |
|---|---|
| [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md) | a `scores.LimboChart` row |
