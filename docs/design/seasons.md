# Seasons

Status: **design complete; slice 0 built and bug-checked (2026-09-12), slice 1a next on the owner's go.** Scoped and decided 2026-09-12 with the owner
([scoping artifact, Round 2c](https://claude.ai/code/artifact/de8ed96c-d3c2-47fe-a25e-1c726348ff53):
the census, the re-scored feature table, the boolean analysis); mocks published and corrected the same
day (§13, four sheets, Round 2). The decisions in §3 are the owner's where marked and *decided unless
objected* otherwise. The build is the slow roll in §12; this document shipped with slice 0. If you are
picking this up cold: §1 says what it is, §3 what was decided and why, §12 what to build next and what
it touches (§12.2 is slice 1a), §13 what it should look like.

Every quarter your Phoenix 2 scores start over on a seasonal board while your all-time record stays
exactly where it is. You re-grind. Every chart carries a **season rating** one folder up or down from
its printed level, moved by how much people pooled it last season, so the charts worth chasing change
each time. In **seasonal view** every leaderboard ranks PIU Scores players by what they did this
season. Official imports only. Nothing typed by hand. Everyone who imports is in.

The whole feature is one idea: **the score journal stays single, the personal-best pool forks.** A
seasonal personal best is a second best-attempt row in the same table, flagged for the season; every
number derived from personal bests (PUMBILITY, folder completion, the boards) gets a season-flagged
row written by the same code run a second time over the seasonal pool. Reads take the season. At the
roll, the season's rows are copied to archive tables and deleted, and that deletion is the seal.

Phoenix 2 only. Phoenix 1 has no seasons and never will (it is going offline-only).

---

## 1. What a season is, in player terms

- **A season is a quarter**, on the March of Murlocs calendar: Winter · Spring · Summer · Fall, ending
  23:59:59 UTC−5 on the last day of March, June, September and December. One calendar and one word
  for the whole site; a March of Murlocs board is *the March of Murlocs board of Fall 2026*.
- **Seasonal view is a choice in the mix picker.** Under Phoenix 2 the picker offers *All-time* and
  the running season. The pill in the app bar reads **Phoenix 2 · Fall 2026** on every page while you
  are in it, so nobody wonders which numbers they are looking at. Seasonal view is always the *current*
  season; past seasons live on the season pages (§8.1), never in the picker.
- **What counts:** your best official-import play on each chart whose play time falls inside the
  season. Stage breaks never count; a finished fail counts as a fail (priced at zero, shown as history)
  exactly as it does all-time. Hand-typed scores, CSV uploads and API writes never count, and the quick
  record forms hide in seasonal view.
- **Season PUMBILITY** is the Phoenix 2 formula over your seasonal bests at season ratings: the Total,
  Singles and Doubles pools, the CO-OP rating, and **TOTAL PUMBILITY** (every chart summed, the
  "whole game" number). Unrounded below the UI like every PUMBILITY.
- **Season ratings** are a diversity rule, not a difficulty correction. Per folder, by how many
  pools held each chart last season: the most-pooled fifth moves down a level, the least-pooled fifth
  moves up one, from wherever the chart already sits, so two seasons running is two levels. A chart
  keeps its printed level and its printed folder everywhere; the rating changes only what the chart
  is worth and puts a **corner chevron on the bubble** (▲ for one up, ▲▲ for two, a number from
  three). **Season one has no balancing**; balancing starts at the first roll after launch.
- **Peers stay all-time.** Who your peers are and what they scored is the all-time site; only your
  own scores swap. **Rivals flip**: a rival is a named site player with season scores of their own, so
  the Rivals page and the head-to-head compare season against season. The Highlights feed, Official
  Leaderboards, Weekly Charts, Daily Step and March of Murlocs are all-time, and say so in one line
  while seasonal view is on.
- **Season one is Summer 2026**, backfilled from the journal from everyone's first Phoenix 2 entry.
  It ends 2026-09-30 and is not an interesting season; it exists so the machinery has a past.

## 2. Why

- **Re-grinding is the appeal.** Phoenix 1 veterans refreshed a median 20% of their top-50 pool in a
  quarter and 37% in half a year (§5): even six months does not re-set most of a pool on its own, so
  the re-grind is deliberate at any length, and a quarter keeps it achievable (fifty charts in thirteen
  weeks is four a week) and gives four boards a year to win.
- **Diversity.** The same staples sit in every pool (OVERNIGHT FLOWER in 54% of the S22 pools that
  touch the folder). Moving the staples down and the neglected up every quarter changes what is worth
  chasing without pretending to know what is hard.
- **Community boards for the casual segment.** "Best in Brazil this season" costs a flag on a page
  that already exists. Weekly Charts is the core segment's competition; seasons give the community
  segment a ladder that resets.
- **Imports matter more.** A below-best play is only ever seen while it sits on piugame's recently
  played page (§4.1); a season rewards importing often, and the import page is already the site's
  second-busiest page.

## 3. Decisions

| # | Decision | Rationale / who |
|---|---|---|
| D1 | **Quarters on the March of Murlocs calendar.** Ends 23:59:59 UTC−5 on the last day of Mar / Jun / Sep / Dec; names from MoM's `SeasonName` (Winter · Spring · Summer · Fall + year). | Owner, 2026-09-12. One calendar, one word: MoM already owns "Season" and both resx keys. Four boards a year. |
| D2 | **The toggle lives in the mix picker**, under Phoenix 2: *All-time* / *Fall 2026 season*. The pill reads "Phoenix 2 · Fall 2026". | Owner. A separate switch competes for the app bar and has to explain why it is greyed out on Phoenix 1. |
| D3 | **Season one is flat.** Every chart at its printed level. **Balancing starts at the first roll after launch**, computed from the season that roll seals. | Owner ("no balancing" for season one). A rating players could not see from day one would make the "what moved" page lie, so no season that was already running at launch is balanced retroactively. |
| D4 | **Official imports only. Manual, CSV and API writes never reach a seasonal row.** Quick record hides in seasonal view. | Owner. `ScoreJournalEntry.Source` already tells them apart; manual data stays sacred on the all-time side. |
| D5 | **No opt-in.** Every importer is in. | Owner. |
| D6 | **Peers stay all-time, and peer *selection* keys on your all-time standing.** On a peers surface only your scores swap. **Rivals flip:** the Rivals page, the rivals-of-you list and the head-to-head compare season scores on both sides, because a rival is a named site player who has season rows of their own. A board-only rival (an official-board tag) has no season data and shows all-time behind the existing asterisk mark. The Highlights feed stays all-time (it is play history). | Owner: peers all-time (2026-09-12); "Rivals page absolutely can support seasonal view" (same day, correcting Round 1's caption). The selection rule is the consequence of the first: chosen by a seasonal number, week one's peers are beginners. |
| D7 | **Season ratings are a symmetric diversity rule.** Per folder, charts ranked by weighted hold count in last season's pools (50 points at slot 1 → 1 at slot 50, the PUMBILITY tier lists' weighting): top 20% move −1, bottom 20% move +1, each from the chart's current season rating (D8); a chart in neither fifth **holds** where it is rather than snapping back to printed (decided unless objected — a snap-back would make a chart oscillate the season after it worked). Ties by fewer passers, then name. Folders with fewer than 50 folder-live players (three or more of their fifty in the folder) do not move. New charts enter at printed. | Owner: "underplayed charts are fine to rebalance too. The idea is diversity and to mix it up." The census (§5) showed the unheld charts are the unplayed ones, which is exactly what this rule moves. |
| D8 | **Season ratings compound.** Each roll moves a chart one step from its *current* season rating, bounded to levels 1–29; a chart in the bottom fifth two seasons running sits at +2, and the marker carries the magnitude by **chevron count** (▲, ▲▲, then a signed number from ±3 — owner, 2026-09-12, over the signed-pill option). The What-moved page prints the running total ("22 → 24, second season up"). | Owner, 2026-09-12: "charts can be up/downrated multiple seasons in a row … I'll keep an eye on if anything gets out of hand." The rule self-corrects: an uprated chart pays more, gets pooled more, and leaves the bottom fifth; a downrated staple pays less and gets dropped. Round 1's ±1 cap is withdrawn. |
| D9 | **Charts keep their printed level and folder everywhere.** The rating changes the price and the marker only. | Owner ("charts still show their official difficulty"). |
| D10 | **Seasonal rows live in the live tables, not in twin tables.** A season discriminator on the personal-best, player-stats, folder-level and chart-mix tables; every reader of those tables is audited once. | Owner: "I would rather eat a one-time cost of auditing every place that uses the affected tables than permanently accept maintaining migrations for 2x the tables." |
| D11 | **The discriminator is a `SeasonId` with `Guid.Empty` meaning all-time, not a bit.** | Decided unless objected. Same tables, same wipe-at-seal semantics, but a bit cannot hold two seasons at once, and the seven-day grace (D13) needs the ending season's rows and the new season's rows to coexist for a week. |
| D12 | **An EF global query filter on each flagged entity excludes seasonal rows unless a reader opts in.** Raw SQL bypasses it and gets a ratchet. | Decided unless objected. Turns the one-time audit into a permanent default: a reader added next year cannot leak seasonal rows by forgetting a filter. First use of a global filter in the codebase. |
| D13 | **The roll is the seal.** Seven days after the boundary: copy the ended season's rows into the archive tables, delete them in batches, and the season is sealed. During the seven days, in-window plays still land on the ended season. | Owner ("new season wipes/resets the rows. THAT'S what seals it"); the grace is decided unless objected. |
| D14 | **Archive tables hold past seasons only** — personal bests, chart balances, standings, folder completion — and live in the `scores` schema. | Owner. They are live-read tables (the past-season pages), so they are not `archive`-schema tables, which mean *retired feature*. |
| D15 | **The counting rule.** A recently-played play dated inside the window counts. A best-list card counts if it is dated inside the window **or** it raised an all-time record you already had. Anything else is an old score being seen for the first time and does not count. | Decided unless objected. A Phoenix 2 best-list card is stamped with the chart's *first* play, so an in-season upscore can wear a pre-season date ([stage-breaks-and-max-combo.md](stage-breaks-and-max-combo.md) §6); the "raised an existing record" half catches it, and a brand-new importer's old cards stay out. |
| D16 | **Seasonal view is always the current season.** Past seasons are reachable from the season pages (previous / next, past-seasons dialog) and read the archive tables; no other surface reads a past season. | Decided unless objected. Keeps every flag a bool at the read sites and confines the second read shape to one page family, the way MoM reads frozen sessions. |
| D17 | **One canonical URL per page; seasonal is view state** (setting + cookie), never a route or query string. Crawlers see all-time. | Decided unless objected. No duplicate content, no sitemap change, the static pages resolve it the way they resolve the mix. |
| D18 | **Not seasonal in v1:** titles, rating-history graphs, highlights and milestones, the community tier lists (Score / Pass / PG / Popularity / PUMBILITY lens), chart similarity, official-rank estimates. Tier-list *rows* show your seasonal score and the marker. | Decided unless objected. Titles are piugame facts; the rest are nightly community projections whose seasonal twins would double a six-job fan-out for a thin population. |
| D19 | **Per-score PUMBILITY (`PhoenixRecordStats`) is not mirrored.** Seasonal pools are priced live from seasonal bests. | Decided unless objected. It is a pricing cache of 1.08M rows the census work already prefers to bypass; the season pass prices its fifty in milliseconds. |
| D20 | **Season boards are static SSR** like the MoM season page, print their player count on every board, and under the floor say "12 players so far" instead of crowning three. | Decided unless objected. 376 Phoenix 2 importers today; honesty is copy, not code. |
| D21 | **Excluded pages carry one line under the title** while seasonal view is on: *All-time. Weekly Charts are not part of seasons.* Never a banner. | Decided unless objected. |
| D22 | **Deleting an account deletes its seasonal and archived rows too**, leaving gaps in past boards, as the weekly placings already do. | Decided unless objected. Consistent with the site's delete-my-data stance; the alternative (a blanked placeholder) keeps a score row tied to a deleted person. |
| D23 | **Backfill replays the journal per quarter since Phoenix 2 launched.** Summer 2026 is archived by the backfill; the season running at launch becomes the live one, flat. | Owner ("we'll just be backfilling from everyone's first entry into Phoenix 2"). |
| D24 | **A new `Seasons` vertical owns the season, the roll, the archive tables and the season pages.** The flagged rows stay with the verticals that own their tables; the season pass is a second call into their existing sagas. | Decided unless objected. Follows the WeeklyChallenge template; the Seasons vertical references nothing back. |
| D25 | **The 50-card list is an accepted risk, and the player is told once.** The first time an account turns seasonal view on, a dialog explains the feature and says, in substance: *this depends on your recent scores, which piugame caps at 50. Import after each session, or mid-session if you play big ones.* Dismissed once (`Universal__SeasonsIntroSeen`), never again. | Owner, 2026-09-12: "accepted risk. always has been." The dialog is mocked before it is built (§13). |
| D26 | **The Account page's peers section carries a disclaimer**: peers, and what they scored, are always all-time; in seasonal view your own scores swap and theirs do not. | Owner. The Play page keeps all-time peers by D6; the explanation lives where the player picks their peer sources. |
| D27 | **Everything player-facing ships behind `Seasons:EnableUI`** (config, default false). Off: no picker option, no pill state, no captions, no marker, no season routes, no widget — for anyone who is not an admin. Admins always see the full UI. The data side (the season row, the flagged rows, the season pass, the roll, the rollup, the backfill, the admin console) runs regardless, so seasonal history accumulates behind the scenes before anyone else sees it. | Owner: "we're gonna build this a small bit at a time … maintaining seasonal stuff behind the scenes for a little bit before we expose it to users other than me." Same shape as `PreventRecurringJobs`: a flag read once at startup, flowing from the AppHost locally. |
| D28 | **Hardmode is a first-class citizen of a season: the season's hardmode chart list is locked at the roll and does not move until the next one.** The roll takes a copy of the all-time weekly list as it stands that day (the Hardmode census, board players included) and freezes it for the season; it does not run a second census of its own. The season running at launch takes the copy at launch. | Owner, 2026-09-12: "hard mode charts are identified at the beginning of the season and lock in until the next one." All-time Hardmode keeps its weekly recompute on the PUMBILITY page; a season gets a season-long board on the season page. Measured why the copy and not a site-only census: over PIU Scores pools alone the "every unheld chart" clause admits 2,242 charts today against the census's 1,211 with board players — the electorate is the point of that census, and a season should not redefine it. |
| D29 | **Season hardmode PUMBILITY** is the hardmode pooling — Singles, Doubles, Combined, no pool gate, ranked on the total however many charts are held — over *seasonal* bests restricted to the season's list, priced at season ratings. The same six columns the Hardmode build put on `PlayerStats` (`HardmodeRating`, `HardmodeSinglesRating`, `HardmodeDoublesRating` and the three `*ChartsHeld` counts), on the season's row. | Follows from D28 and the standing Hardmode rules (no full-pool gate, ever). PIU Scores players only: official-board players do not import, so they are not in a season (their all-time hardmode rows live in `scores.OfficialHardmodeRating`, which seasons never touch). |
| D30 | **Where it shows.** The season page carries a **Hardmode tab** (Singles · Doubles · Combined, season-long). The PUMBILITY page's Hardmode tab in seasonal view shows *your* season hardmode standing, pool and the locked qualifying list, and links to the season board; in all-time view it is exactly what the Hardmode build ships (weekly list, PIU Scores and Official Boards tabs). | Owner: "All-time does the weekly leaderboards in PUMBILITY page, and seasons has a season-long leaderboard on the season page." |
| D31 | **`scores.HardmodeChart` (ChartIntelligence, `HardmodeChartEntity`) gains the `SeasonId` discriminator** (D10 shape): the all-time rows are rewritten by the census (`HardmodeCensusSaga`, `RebuildHardmodeChartsCommand`), a season's rows are written once at the roll (a copy of that day's all-time list, D28) and archived at the seal. | Decided unless objected. No second census, no second weekly job. The Hardmode board merged to main on 2026-09-12 (PR #333), so slice 5 has no external dependency left. |
| D32 | **Season boards are the site's standard leaderboard row, nothing bespoke.** Rank through `RankDelta` (previous rank from the nightly rollup, so the arrows light up); the player through `UserLabel` with the 26px avatar and the flag wash under the name; the figures in `olb-grid` columns; row highlighting by the shared utility set with its precedence you → both → rival → community (`.olb-row-me`, `.is-both`, `.is-rival`, `.is-community`), which is how community members and rivals stand out on every board. Board-only rivals never appear (site players only, D29). | Owner, 2026-09-12: "make sure our leaderboards are using all the standard elements … avatar, flag, rank coloring, community/rivals highlighting." |

Explicitly rejected: a separate season calendar from MoM's (two "seasons" on one site); twin tables
per flagged entity (D10); reading seasonal bests from the journal on every request instead of keeping
the seasonal personal-best rows (viable at 8 ms per player, but a second read shape everywhere the
record table is read; the flagged rows keep every read identical in shape); balancing season one from
all-time pools (D3); a difficulty-based rating read off the tier-list bands (the owner wants diversity,
not correctness).

## 4. The model

### 4.1 The seasonal personal best

The seasonal personal best is a row in the personal-best table with the season's id, keyed
`(UserId, ChartId, MixId, SeasonId)`, written by the same handler that writes the all-time row
(`UpdatePhoenixRecordHandler`), with the same best-attempt policy (a pass outranks a break, then score,
plate as tiebreak) starting from an empty pool at the season start. Judgements and max combo ride
along as they do all-time.

Which imported score becomes a seasonal write is D15. Three facts about the source pages make it what
it is:

- **Recently played is one un-paged list of 50 cards** (`PiuGameApi.GetRecentScores`, a single GET).
  A play below your all-time best exists for the site only if it is still on that list when you
  import. 2,162 of 2,186 Phoenix 2 imports carried at most 50 judged plays; repeat importers import
  every 1.7 days at the median and within 7.6 days at the 90th percentile; about one import in fifteen
  arrived with the list full, the only moment plays can have been lost (§5).
- **A best-list card is dated with the chart's first play.** `EnrichBestsFromRecentPlays`
  (`OfficialSiteClient.cs`) prefers the producing play's own time when that play is still on the
  list; otherwise the record carries the card's date. An in-season upscore whose producing play has
  scrolled off therefore arrives with a pre-season date, which is why D15 also counts a best-list card
  that raised an existing all-time record: the import already knows it did (`UpscoreCount`).
- **`Source` is on every journal row and every record** (`ScoreJournalEntry.OfficialImportSource`).
  The seasonal writer runs only for `officialImport`; the manual, CSV and API paths never call it.

Undo-an-import replays the all-time bests from the surviving journal rows (`SessionUndoReplay`); the
seasonal rows are rebuilt by the same replay with the season window applied. Delete-my-data's mix wipe
deletes the seasonal rows with the all-time ones (same table, same `UserId`).

### 4.2 Season PUMBILITY and the player-stats rows

`PlayerRatingSaga.RecalculateCore` prices a player's bests, assembles the top-50 pools and writes
`PlayerStats`. The season pass is the same call with the season: it reads the seasonal bests through
the flagged reader, prices them with `ScoringConfiguration.PumbilityScoring(Phoenix2)` carrying the
season ratings in `ChartLevelSnapshot` (the per-chart override hook March of Murlocs already uses,
`includeLevelOverride: true`), and writes the season's `PlayerStats` row. TOTAL PUMBILITY is a new
column on that row: the sum over every priced chart, the same shape the CO-OP rating already has.
Milestones and titles run **quiet** on the season pass (the `quiet: true` path exists for bulk
re-prices) — no seasonal lamps, gains or titles in v1 (D18).

Your own seasonal number on a page is always the row; the Breakdown page's fifty are priced live from
the seasonal bests the way the top-50 handler prices all-time ones today (D19).

### 4.3 Season ratings — the roll

At the seal of season *N* (D13), for season *N+1*, over the sealed season's per-type pools:

1. For every player with a full 50-chart pool of the type, take the pool; weight slot *k* as `51 − k`.
2. For every folder (type × printed level), the **folder-live** players are those with three or more
   of their fifty in the folder. Folders with fewer than 50 live players are skipped.
3. Rank the folder's charts by their weighted hold among live players; ties by fewer passers among live
   players, then by name.
4. Top 20% → current season rating − 1. Bottom 20% → current season rating + 1. Everything else holds
   its current rating (D7, D8); a chart in a skipped folder holds too; a chart new since the season
   started enters at printed. Bounded to 1–29.
5. Write one seasonal chart-mix row per Phoenix 2 chart for season *N+1* (§6.2) and the season's
   "what moved" list (§8.1), each row carrying the delta from printed and the delta this roll.

The rule moves a fixed fifth each way whatever the population's shape, which is what "mix it up" asks
for. The census (§5) is the evidence that "held by nobody" means "played by nobody" on today's data:
under a threshold rule the whole rebalance would have been a popularity rebalance anyway; the rank rule
just says so and stays stable as the population grows. The admin console shows the dry run per folder
before a roll (§8.3).

### 4.4 Peers

`IPlayerVisibilityReader`, `IPeerStandingReader`, the PUMBILITY peer pools and the peer score stores
are untouched. Two things follow from D6 inside the flag: the peer group is chosen by the all-time
`PlayerStats` row (rung or competitive level), and only the viewer's scores come from the seasonal
reader. The Play page then reads exactly as the owner framed it: all-time peers, your seasonal
scores, gains priced at season ratings.

Rivals are the other case. `GetRivalScoresForChartsQuery` is the one read every rival surface goes
through, and it reads site players through the ledger reader, so under the flag both sides of a
head-to-head are season bests. Board-only rivals (official-board tags) have no season rows; they keep
their all-time sweep score behind the `rival-official-mark` asterisk the roster already uses.

### 4.6 Hardmode in a season

The Hardmode board ([hardmode-leaderboard.md](hardmode-leaderboard.md), merged 2026-09-12 as PR #333)
is: a list of the charts that sit in the fewest pools per folder (weighted hold, 50 → 1 by slot; cut =
25 or 25% of the folder, whichever is lower, plus every chart nobody holds; folders of four or fewer
take exactly one; ties by scoring level descending then name; all levels, with a volatility note at
level 17 and below), rebuilt by `HardmodeCensusSaga` in ChartIntelligence into `scores.HardmodeChart`,
and three pools — Singles, Doubles, Combined — priced with the normal PUMBILITY formula over that
list only, no full-pool gate, ranked on the total, recomputed for a player when they import. Two
leaderboards, PIU Scores and Official Boards, in a Hardmode tab on the PUMBILITY page; six columns on
`PlayerStats` (`HardmodeRating`, `HardmodeSinglesRating`, `HardmodeDoublesRating` and the three
`*ChartsHeld` counts) and `scores.OfficialHardmodeRating` for board players.

In a season (D28–D31):

- **The list is locked.** The roll computes it once from the sealed season's PIU Scores pools — the same
  census that produces the season ratings — and writes the season's rows; nothing rewrites them until
  the next roll. The volatility note is replaced by *locked for Fall 2026*.
- **The number is seasonal.** The same three pools over seasonal bests restricted to the season's list,
  priced at season ratings (a hardmode chart is low-hold by definition, so many carry a +1; that is the
  season's price and it applies here too). Written on the season's `PlayerStats` row by the season pass.
- **The board is season-long, on the season page**, PIU Scores players only, with the standing card,
  pool count beside every total and the title rails asking for charts, as the all-time tab does.
- **The PUMBILITY page's Hardmode tab flips like the rest of the section**: your season standing, your
  season hardmode pool, the locked qualifying list by folder, a link to the season board.
- **Season one** has no sealed predecessor, so the season running at launch copies the all-time weekly
  list as it stands at launch and locks that.

The Hardmode board is on main, so seasons mirror its table and its six columns with no dependency left.

### 4.5 The view

The selected mix reaches every page as one field on `ShellContext`, resolved by `ShellModelFactory`
from the `Universal__CurrentMix` setting and the `CurrentMix` cookie, re-seeded into each circuit by
`ShellMixSeed`, written by `MixController` through a redirect (only a response can write the anonymous
cookie). The season is a second field on that same context: a `Universal__SeasonalView` setting, a
`SeasonalView` cookie, a second root parameter on the seed, one more branch in the same controller.
`IUiSettingsAccessor.GetSelectedMix()` gains a sibling that returns the view (mix + seasonal), which is
what the flipping pages pass down.

Reads take the view explicitly where a query record already carries the mix (a defaulted parameter, so
every existing caller compiles unchanged). Background consumers have no request scope, so the
import-time seasonal writes are explicit calls, never reads of an ambient accessor.

**Cache keys are the one place a miss is silent.** About twenty memory-cache key shapes are spelled by
hand at their call sites; only `OfficialCacheKeys` is a shared builder. A key that includes the mix and
forgets the season serves all-time numbers under a seasonal pill with no error anywhere. Before any
seasonal read ships: one shared key builder that takes the view, and a shrink-only architecture ratchet
(the `UiColorTokenTests` shape) failing any hand-spelled key that names a mix. This is its own PR
(§12, slice 0).

## 5. What the data says

Measured 2026-09-12 on the prod-synced local database (restore ≈ 2026-09-10); Phoenix 2 mix id
`A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948`. Phoenix 2 was 60 days old, so its whole history fits in one
window; the turnover read uses Phoenix 1 veterans.

| Measure | Value |
|---|---|
| Phoenix 2 importers / records / full singles pools / full doubles pools | 376 / 53,512 / 180 / 130 (Phoenix 1 had 1,645 importers) |
| Journal rows in the window, by source | officialImport 71,165 (357 players) · manual 3,347 (20) · csv 1,625 (5) |
| Of 76,137 plays: became a record / below-best pass / finished fail / stage break | 59,106 / **5,040** / 11,991 / 8,830 |
| Imports carrying ≤ 50 judged plays | 2,162 of 2,186 (the recently-played list is 50 cards) |
| Repeat importers · median / p75 / p90 gap between imports | 272 · 1.7 / 3.9 / 7.6 days |
| Imports that arrived with the list full | 148 of 2,186 (≈ 1 in 15) |
| Seasonal best for the heaviest importer (1,120 rows → 768 charts) | 8 ms |
| Season board for the busiest chart, every player | 13 ms |
| Every player, every chart (45,062 pairs) | 339 ms |
| P1 veterans (≥ 9 months in, active): top-50 entries set within 90 / 120 / 180 / 365 days | 28% / 33% / 42% / 65% (singles); median player 20% in 90 d, 37% in 180 d |
| Folders with ≥ 50 folder-live players today | S18–S23, D18–D24 |
| Charts held by ≤ 2% of live players yet passed by ≥ 10% of them, per folder S19–D24 | 0–4 |
| S22 most pooled (of 119 pools touching the folder) | OVERNIGHT FLOWER 54% · Rise Up 47% · Freedom Dive 41% |
| S22 pooled by nobody | DISTRICT V (1 passer) · Vulcan (1) · Halcyon (3) · BATTLE NO.1 (3) |

Under D7, S22 moves 21 charts each way per season and S20 27.

## 6. Target schema

### 6.1 The season

```
scores.Season                                  -- Seasons vertical
  Id          uniqueidentifier PK
  Year        int
  Quarter     tinyint                          -- 1..4
  Name        nvarchar(100)                    -- "Fall 2026", MoM's SeasonName
  StartsAt    datetimeoffset
  EndsAt      datetimeoffset
  SealedAt    datetimeoffset NULL              -- set by the roll (D13)
  IsBalanced  bit                              -- D3: false for every season running at launch
  CREATE UNIQUE INDEX UX_Season_Quarter ON scores.Season (Year, Quarter);   -- MoM's anti-runaway
```

### 6.2 The discriminator on the live tables (D10, D11)

`SeasonId uniqueidentifier NOT NULL DEFAULT '00000000-…'` (`Guid.Empty` = all-time) on:

| Table | Today's key | Becomes | Owner |
|---|---|---|---|
| `scores.PhoenixRecord` | unique `(UserId, ChartId, MixId)` | unique `(UserId, ChartId, MixId, SeasonId)` | ScoreLedger |
| `scores.PlayerStats` | PK `(UserId, MixId)` | PK `(UserId, MixId, SeasonId)`; + `TotalPumbility float` | PlayerProgress |
| `scores.PlayerFolderLevel` | PK `(UserId, MixId, ChartType, Level)` | + `SeasonId` | PlayerProgress |
| `scores.ChartMix` | unique `(ChartId, MixId)` | unique `(ChartId, MixId, SeasonId)`; a seasonal row's `Level` is the season rating | Catalog / Data |
| `scores.HardmodeChart` | presence rows per (ChartId, MixId) | + `SeasonId`; all-time rows rewritten by the census, season rows written at the roll (D31) | ChartIntelligence |

Every existing row gets `Guid.Empty`. The unique-index changes on `PhoenixRecord` (1.12M rows) are a
rebuild inside the deploy migration bundle — name both the dropped and the created index explicitly
(EF collapses two indexes over the same columns into one unless both carry names). Global query filters (D12) on all four
entities: `SeasonId == Guid.Empty` unless the reader opts in.

**Not mirrored:** `PhoenixRecordStats` (D19), `UserTitle` / `UserHighestTitle`, `PlayerHistory`,
`ScoreHighlight`, `PlayerMilestone`, `PlayerHighlight`, every ChartIntelligence table (D18).

### 6.3 The archive (D14)

```
scores.SeasonPersonalBest    (SeasonId, UserId, ChartId, MixId) PK; Score, Plate, IsBroken, RecordedDate,
                             Perfects..Misses, MaxCombo                       -- purge manifest: yes
scores.SeasonStanding        (SeasonId, UserId, MixId) PK; the PlayerStats columns + SkillRank,
                             SinglesRank, DoublesRank, CoOpRank, TotalRank      -- purge manifest: yes
scores.SeasonFolderLevel     (SeasonId, UserId, MixId, ChartType, Level) PK; Size, Played, TierScore
scores.SeasonChartLevel      (SeasonId, ChartId, MixId) PK; Level; PrintedLevel; Delta (Level − Printed),
                             MovedThisRoll (−1/0/+1), HoldRank, HoldWeight, LivePlayers, IsHardmode
                                                                               -- the "what moved" page
                                                                               -- and the sealed hardmode list
```

All four are registered through the Seasons vertical's `IDbModelContribution` and listed in
`VerticalModelContributions.All()`. The user-keyed three go in the vertical's `UserOwned` manifest
(D22); `SeasonChartLevel` has no user key.

### 6.4 The audit (D10)

The production code that touches the four flagged entities, measured 2026-09-12:

| Vertical | Files |
|---|---|
| ScoreLedger | `EFPhoenixRecordsRepository` (31 references, the one implementation of the record port), `EFPhoenixRecordStatsRepository`, `EFScorePopulationRepository`, `EFLedgerStatsRepository`, `EFAccountPurgeRepository`, **`PeerScoreStore` (raw `DbDataReader` SQL against `scores.PhoenixRecord` and `scores.ChartMix` — the one production reader the query filter cannot cover)** |
| PlayerProgress | `EFPlayerStatsRepository`, `EFPlayerFolderLevelRepository`, `EFAccountPurgeRepository` |
| Catalog | `EFChartRepository` (`ChartMix` → the per-mix chart dictionary; gains a per-season variant and cache key) |
| Data | `ChartAttemptDbContext`, `DevCatalogWriter` (one raw `DELETE` by user, correct as is) |

Eleven files, one raw-SQL reader. Tests seed these tables in a handful of places and keep working
because the column defaults to all-time. The API v2 and webhook payloads read through the same
repositories and therefore through the filter; a content test (not just the approval-pinned shape)
asserts a seasonal row never reaches them.

## 7. Jobs, the roll, the backfill

| Job | Cadence | What it does |
|---|---|---|
| `roll-season` | Daily 11:15 UTC (after `try-schedule-mom`) | Stateless like MoM's: "does the quarter I stand in have its row?" → create it, `IsBalanced` per D3. Then, for any ended season past its seven days and not yet sealed: compute season ratings for the running season from the ended one (§4.3, only if the ended season `IsBalanced` or launch has passed), copy the ended season's flagged rows into the archive tables, delete them in batches of 10,000, stamp `SealedAt`, publish `SeasonSealedEvent`. Idempotent at every step; a crash mid-way resumes on the next tick. |
| `rollup-season-stats` | Nightly | Recompute every player's season `PlayerStats` row from seasonal bests (0.3 s for everyone today), so a board never depends on an import having fired the pass. |

The import path itself: `HighlightCaptureSaga` runs the rating step in-process today; the season pass
is a second in-process step behind it, failure-isolated, quiet (§4.2). The flush job that recovers
stranded batches covers it for free, and `RebuildLatestSessions` replays it.

**Backfill (D23)** is an admin button, one-shot and idempotent: for every quarter from Summer 2026 to
the running one, replay the journal (official imports, in-window, D15) into seasonal personal bests,
compute the standings, archive the ended quarters and leave the running one live and flat. Season
ratings are not backfilled (D3).

## 8. Surfaces

The full table with sizes is in the scoping artifact; this is the shape.

### 8.1 New

- **Season boards** (static SSR, the `Seasons` vertical, the MoM season page as the template): tabs
  PUMBILITY (Total · Singles · Doubles · CO-OP), **Hardmode (Singles · Doubles · Combined, D30)**, Folder
  completion, TOTAL PUMBILITY; your standing card;
  player counts on every board (D20); previous / next season; the past-seasons dialog (lazy island,
  MoM's). Past seasons read the archive tables (D16). Route to be mocked; `/Seasons` is free
  (MoM deliberately has no such route).
- **What moved this season**: a section of the season page, per folder: the charts that moved up and
  down this roll with jackets, the hold counts behind them and the running total from printed
  ("22 → 24 · second season up"), from `SeasonChartLevel`. The trust device for the rebalance and the
  page a Discord card links to.
- **The bubble marker**: `DifficultyBubble` gains an overlay slot (it has none — the bubble is an image,
  or a CSS chip for legacy slots); a corner chevron, ▲ or ▼, stacked once more for a second step and a
  signed number from three (D8), in a new mix-invariant token pair (`--season-up` / `--season-down`,
  outside both the difficulty and rarity ramps); pure CSS so static pages carry it; dropped below 24px
  rather than shrunk. Discord bubbles are guild emoji and print "S22 ▼21" in text instead. Mocked (§13,
  sheet B).
- **A Season widget** on the home page: your ranks, days left, what moved. Existing widgets flip
  through the page-level context they already carry (mix today, view tomorrow).
- **The intro dialog** (D25): shown once, on the first page rendered after the picker switch lands
  (the switch is a redirect through the mix controller, so the dialog is an island on the landing page
  keyed off `Universal__SeasonsIntroSeen`, the pattern the recap pointer popup used). Three beats: what a
  season is, what counts, the 50-card import advice. One button.
- **The Account page peers disclaimer** (D26): one paragraph in the peers section.

Everything in this section and §8.2 is gated by `Seasons:EnableUI` (D27) for non-admins.

### 8.2 Flipped by the view

PUMBILITY section (all three pages: Play with all-time peers and seasonal you, Breakdown's seasonal
fifty and number, Phoenix 1 hidden), Community leaderboards (Rankings from the seasonal stats rows, By
Chart and play counts through the flag), Chart search (facets, states, min/max, export), the Chart page
(season best and all-time best together; the community and rivals scopes read season bests; peers,
official and Limbo scopes stay all-time), the Player page (number, tiles, folder completion), the
Rivals page and head-to-head (D6),
tier-list rows (your score and the marker; the lists stay all-time), the import page and session card
(one added line: *Fall 2026: +N PUMBILITY · #rank*).

### 8.3 Unchanged, with the caption (D21)

Weekly Charts, Daily Step, March of Murlocs, Official Leaderboards, the Highlights feed, Titles,
Sessions, the recap, tools and calculators, the randomizer, comments. (Rivals and the head-to-head
flip, D6.)

**Admin:** a season console — live and next season, roll now, the balancing dry run per folder, re-price
a season, pin one chart's season rating by hand, the backfill button.

## 9. Where the flag is not enough

Under D10 every one of these is the same move: a mirrored write with the seasonal pool.

1. **Numbers somebody already wrote** — `PlayerStats`, folder levels, and everything in D18. The
   season pass writes the first two; the rest stay all-time.
2. **Ranking everybody** — the season boards, community Rankings, the folder-completion board sort the
   season's `PlayerStats` rows; the nightly rollup guarantees they exist.
3. **Writes during import** — the session card line and any Discord card are an extra pricing pass, not
   a flipped read.
4. **The season ratings** — made by the roll, read through the chart reader's flag.
5. **The seal** — the archive copy and the delete (D13); nothing checks a sealed flag afterwards because
   there is nothing left to write to.
6. **Carrying the flag** — §4.5; the cache-key builder and its ratchet are the real cost.
7. **Peer selection** — D6, a rule inside the flag.

Two seams that look like breaks and are not: static pages resolve the view from the request the way
they resolve the mix, and undo / delete need nothing beyond the replay in §4.1.

## 10. Concerns

### 10.1 Bug-check checklist

The owner's ruling on this list (2026-09-12): the first item is an accepted risk handled by copy
(D25), the second is why peers stay all-time (D6, D26), and the rest is technical coverage. It is kept
here as the checklist for the bug-checking sessions, one line of *what to verify* per item.

| # | Concern | What to verify |
|---|---|---|
| 1 | **The counting rule and the 50-card list** (§4.1, D15). Accepted risk. | Import a session with plays below your all-time best → they appear as seasonal bests. Push 51+ plays between imports → the oldest is missing and nothing errors. A best-list-only upscore on a chart you already had → lands on the current season. A first-ever importer's old cards → no seasonal rows. The intro dialog shows once and never again. |
| 2 | **The Play page with seasonal you** (D6). | Peer group identical in both views for the same player. Your scores swap, theirs do not. Switch view, reload: no projection or cohort cache serves the other view's numbers (keys carry the view). Week one: gains list is long and correct, not empty. On the Rivals page both sides swap; a board-only rival keeps the asterisk and an all-time score. |
| 2b | **Compounding ratings** (D8). | A chart in the bottom fifth two seasons running reads +2 on the What-moved page and ▲▲ on the bubble; a chart at level 29 or 1 stops at the bound; the season ChartMix row holds the absolute level and the archive row the delta. |
| 3 | **Undo and delete.** | Undo an import → its seasonal bests replay from the surviving journal rows, not left behind. Wipe a mix → seasonal rows gone with the all-time ones. Purge an account → archive rows gone; the coverage ratchet names every archive table; the decoy account keeps its rows. |
| 4 | **Cache keys** (§4.5). | The ratchet fails a hand-spelled key that names a mix. Two browsers, same account, one per view, reloading the same page in turn: numbers never cross. |
| 5 | **The audit and the filter** (§6.4, D12). | Every LINQ read of the four tables returns all-time rows unless it opts in. The peer score store's raw SQL filters explicitly. API v2 players/stats, charts and chart scores, and a webhook payload, carry no seasonal row (a content test, not the shape test). |
| 6 | **Season boundaries** (D11, D13). | During the seven days after a boundary, an in-window play lands on the ended season and a new play on the running one. After the seal, a late play for the ended season is ignored and the archive is unchanged. The roll is idempotent when triggered twice, and resumes after a crash mid-copy. |
| 7 | **Static pages and the canonical URL** (D17). | The chart page and the boards render the view from the cookie with no circuit; anonymous default is all-time; no seasonal URL exists and the sitemap is unchanged. |
| 8 | **The flag** (D27). | With `Seasons:EnableUI` off, a non-admin sees no picker option, pill state, caption, marker, route or widget, while the admin sees all of it and the data side keeps running (rows, roll, rollup). |
| 9 | **Quiet season pass** (§4.2, D18). | An import in seasonal territory emits no seasonal milestone, lamp, title, highlight or Discord line beyond the one session-card line. |
| 10 | **Season ratings** (§4.3, D3, D8). | Season one flat. First balanced season: every moved chart is one step from where it stood, a fifth each way per eligible folder, skipped folders and the middle three fifths hold, new charts at printed, nothing outside 1–29, the What-moved section matches the dry run. Pricing on the Breakdown page uses the season rating; the folder a chart sits in does not move. |
| 11 | **Hardmode in a season** (§4.6, D28–D31). | The season's list does not change between rolls while the all-time list moves weekly. The season hardmode number counts only seasonal bests on the season's list, at season ratings, with no pool gate. The season page's Hardmode tab and the PUMBILITY page's Hardmode tab in seasonal view agree on your standing. Season one's list equals the all-time list at launch. |

### 10.2 Size and growth

| Where | Today | At Phoenix 1 scale | Notes |
|---|---|---|---|
| Seasonal personal bests, live | ~50k rows per quarter (45,062 pairs measured) | ~250k | Bounded: deleted at the seal. +5–25% of the all-time table at any moment; one index rebuild at the migration. |
| Archive, per quarter | ~50k bests + ~400 standings + ~16k folder rows + 4.5k chart levels | ~250k + 2k + 80k + 4.5k | Permanent. Roughly 50–250 MB a year with indexes, against a database growing about 29 MB a day today (Azure storage metric, Aug–Sep 2026; 6% of the 50 GB cap used). |
| `PlayerStats` / folder / chart-mix seasonal rows | hundreds / thousands | thousands | Nothing. |
| `PhoenixRecordStats` | not mirrored (D19) | — | The one table where mirroring would have been 1.08M rows of pricing cache. |
| Compute at import | one extra pricing pass, milliseconds per player | same | Inside the existing failure-isolated chain. |
| Nightly rollup | 0.3 s for everyone | a few seconds | |
| The seal | ~50k row copy + batched delete | ~250k | Batches of 10,000 on a hot table; never one transaction. |
| Journal | unchanged by design | | Seasons should raise import frequency, which raises journal rows: the intended effect, perhaps +10–20%. |
| Memory caches | a second per-mix chart dictionary (a few MB); read models keyed by view double lazily | | The peer stores are not duplicated (D6). |

The one table that materially grows is the personal-best table plus its archive, and both are bounded
per season and small beside the journal.

## 11. Traps and ratchets that will go red if missed

- `AccountPurgeCoverageTests` / `AccountPurgeTests`: every user-keyed archive table in the `UserOwned`
  manifest, a decoy row per type; `PlayerStats` seasonal rows purge through the same `UserId`.
- `ModelContributionRegistrationTests`: the Seasons contribution in `VerticalModelContributions.All()`.
- `MessageTaxonomyTests` / `BusMessageSerializationTests`: `RollSeasonCommand` (trigger, plain record),
  `SeasonSealedEvent` (past tense, JSON round-trip; no opaque value types without a converter).
- `VerticalBoundaryTests`: only `Contracts/` and `Wiring/` public; the cross-vertical reads go through
  `IScoreReader` (gains the view) and the published stats reader, never a join.
- `UiColorTokenTests`: the marker's colors are tokens, emitted by `MixThemes`.
- `RenderModeDeclarationTests`: the season pages are static; islands declare their own circuit.
- `ResxKeysAreStoredAlphabetically`, `LocalizationKeyTests`, the Murloc alphabet ratchet: every new
  string in nine locales; `Season` / `Seasons` already exist and belong to MoM's dialog copy.
- `DiagnosticExposureTests`: the admin dry run may print internals; the What-moved page prints counts only.
- `PumbilityPrecisionTests`: TOTAL PUMBILITY and the season pools are doubles; rounding at the razor.
- `CacheKeyTests` (§4.5, §12.1): no hand-spelled key that names a mix, and no `CacheKeys.Mix` key
  whose parts name a player.
- Never edit an applied migration; the `PhoenixRecord` index change is a new migration with both index
  names spelled out.

## 12. The slow roll — slice order

Owner, 2026-09-12: infrastructure first, tracking seasonal bests behind the scenes; then the site flips
over one surface at a time behind `Seasons:EnableUI` so each can be tested alone; then the flag turns
on for everyone. Each slice is PR-shaped, lands green on its own, docs first and i18n last inside it.
"What you can test" is what the owner sees as an admin at the end of that slice; nothing in slices 0–5
is visible to anyone else.

| # | Slice | What lands | What you can test | Gate to the next |
|---|---|---|---|---|
| 0 | **Cache-key builder and ratchet** — *built 2026-09-12, branch `claude/seasons-feature-scoping-ca5bc5`* | Every hand-spelled memory-cache key moved to `CacheKeys` (`Mix` / `Viewer`, with mix-id overloads for the catalog); `CacheKeyTests` ratchets it with an allowlist that started at 27 files / 32 statements and ended empty; `LedgerCacheKeys` and `OfficialCacheKeys` delegate. No feature code. | Nothing visible. Fast suites green; the allowlist is empty. | Merged. |
| 1a | **The schema, reading nothing new** | `Seasons` vertical skeleton; `scores.Season`; the `SeasonId` columns (`Guid.Empty` = all-time) on the personal-best, player-stats, folder-level and chart-mix tables, with the key and index changes; the EF global query filters; the reader audit (§6.4) including the peer store's raw SQL; the `Seasons:EnableUI` flag read at startup with the admin bypass (nothing behind it yet). No writer, no season row. | The whole site behaves exactly as before. The API v2 content test and the audit prove no read changed. This is the deploy that carries the index rebuild on the record table, alone, so its cost is measured once and by itself. | A prod smoke after deploy: numbers on the PUMBILITY page, a community board and API v2 unchanged. |
| 1b | **Tracking begins** | `roll-season` (create the quarter's row; seal an ended one after seven days: archive copy, batched delete, `SealedAt`); the seasonal write in the import chain with the counting rule (D15); the season pass in the rating saga (quiet) writing the season `PlayerStats` row and folder levels; the nightly rollup; the flagged score reader answering seasonal bests; the archive tables and purge manifests; undo/delete replay; the backfill button; the admin season console (live season, roll now, re-price, backfill). | Press **Backfill seasons**: Summer 2026 appears sealed with its archive rows and standings, Fall 2026 appears live; your own seasonal personal bests exist in SQL; an import you make writes a Fall 2026 row beside the all-time one; an undo removes it. Nothing player-facing changes. | Integration tests for the writer, the replay and the seal; one week of imports accumulating in prod while nobody sees them. |
| 2a | **The view, with nothing flipped** | Picker option and pill, setting + cookie through the mix redirect, the shell seed, the intro dialog (once), the caption on excluded pages, the Account peers disclaimer. Every page still shows all-time numbers. | As admin: switch to Fall 2026, see the pill everywhere including static pages, get the intro once, see the caption on Weekly Charts; a non-admin sees none of it. The cookie survives a new tab; the anonymous default is all-time. | Plumbing proven before any number depends on it. |
| 2b | **The marker** | `DifficultyBubble` overlay slot, chevron count (▲ / ▲▲ / signed number from ±3), the mix-invariant token pair, the chart page header line; the Discord text form. Ratings are flat, so nothing shows until the admin console pins one chart's season rating by hand. | Pin 4NT S22 to 21 in the console: ▼ appears on every bubble that draws 4NT in seasonal view, at every size, and nowhere in all-time view. Unpin, it vanishes. | Sheet B's ladder holds up in the real components. |
| 2c | **PUMBILITY section flips** | Frame number and bar, Play (all-time peers, your season scores, season gains), Breakdown's fifty and titles-worth, day-one state; Phoenix 1 page hidden in seasonal view. | Your Fall 2026 number and fifty; Play's gains make sense; peers identical in both views; switching views and reloading never crosses numbers. | The first real surface on the flagged reader; the caches carry the view. |
| 2d | **Chart page and Chart search flip** | Record card with two numbers, the *This season* scope on the chart board, quick record hidden; search facets, states, min/max, export on seasonal bests; markers on every bubble. | A chart you played this season shows season best over all-time; the season scope lists the right players; the SRP filters on season scores. | Static page reads the view from the request correctly. |
| 2e | **Player, Community, Rivals flip** | Player page number, tiles, folder completion; Community Rankings on the season stats rows, By Chart and play counts; the Rivals page and head-to-head on season scores both sides, board-only rivals asterisked. | Rankings of your club for Fall 2026; head-to-head against a rival on season scores; a board-only rival still shows the all-time sweep score with the mark. | |
| 2f | **The rest of the flips** | Tier-list rows (your score and marker; lists stay), the session card line and Discord line, the Season widget, existing widgets via the page context. | An import's session card shows the Fall 2026 gain and rank move; the widget shows your standing. | |
| 3 | **The season page** | Static boards page: standing card, PUMBILITY (Total · Singles · Doubles · CO-OP), Folder completion, Total PUMBILITY; player counts and the floor; previous / next; the past-seasons dialog; standard rows with highlighting. Behind the flag route-wise (admin only). | Fall 2026 live boards with your rivals and clubmates lit; Summer 2026 sealed from the archive; the CO-OP board's honest count. | Sheet D held; boards read only the season stats rows and archives. |
| 4 | **Balancing** | The roll computes season ratings from the sealed season's pools (D7, compounding per D8), writes the season chart-mix rows and `SeasonChartLevel`; the What-moved section on the season page; the admin dry run per folder. | Run the dry run against Fall 2026's pools today: 22 up and 22 down in S22, the lists match Sheet D's preview; nothing moves until the real roll. | Must be merged before the first roll you want balanced (the first roll after launch, D3). |
| 5 | **Hardmode in seasons** | `SeasonId` on `scores.HardmodeChart`; the roll copies that day's list; the six hardmode columns on the season's `PlayerStats` row; the season page's Hardmode tab; the PUMBILITY Hardmode tab in seasonal view with the locked note. | Your season hardmode standing on both tabs agrees; the season list does not change when the census rewrites the all-time one. | Hardmode merged 2026-09-12 (PR #333); no external dependency. |
| 6 | **Flip the flag** | `Seasons:EnableUI = true` in production config. No code. Optional the same week: a front-door line and a Discord announcement. | Everyone sees what you have been seeing. | The §10.1 checklist walked once as a bug-check session; the backfill run; slices 1b–3 live for long enough that Fall 2026's boards are real. |
| 7 | **Fun leaderboards** | Plays, judgements, steps, stage breaks, play days; seasonal and all-time ("since 2026-07-30"); a journal rollup table of its own. | | Slice 2 of the feature proper; can slot anywhere after 3. |

Later, in this order of value: season rewards (decide how many places pay before a season seals), the
Discord season feed (a fourth `DiscordFeedKind`), API v2 (`season=` on players/stats and charts, a
seasons list, the boards — all additive), a per-season recap.

Two calendar notes. The Summer → Fall boundary is 2026-09-30; whenever slice 1b ships, the backfill
creates Summer 2026 sealed and Fall 2026 live and flat, so no boundary has to be caught live. The first
balanced season is whichever quarter begins at the first roll after slice 4 is in production — Winter
2027 if slice 4 lands before New Year, Spring 2027 otherwise; either way the season already running at
that point stays flat (D3).

### 12.1 Slice 0 — the cache-key builder and its ratchet: technical scope

**What it is.** One shared builder for memory-cache keys, so that when the view arrives (slice 1a/2a)
every key that depends on the viewer's scores or on chart levels gains the season segment in one
place, and a ratchet that keeps it that way. No behavior change; a pure refactor that lands alone.

**Inventory (measured 2026-09-12, production code only, ~100 `IMemoryCache` call sites).** Two
builders already exist — `LedgerCacheKeys` (ScoreLedger, internal) and `OfficialCacheKeys`
(OfficialMirror, internal) — and everything else spells its key at the call site. Sorting by what the
cached object contains:

| Class | Key today | Holds | View? |
|---|---|---|---|
| `EFPhoenixRecordsRepository.ScoreCache(userId, mix)` (ScoreLedger; evicted in the same file ×4 and in ScoreLedger's `EFAccountPurgeRepository`) | per user × mix | the player's best scores | **yes** — seasonal bests differ |
| `EFPlayerStatsRepository.CacheKey(mix, userId)` (PlayerProgress) | per user × mix | the `PlayerStats` row | **yes** — the season row is a different row |
| `EFChartRepository.ChartCacheKey(mixId)` (Catalog) | per mix | the chart dictionary incl. `Chart.Level` | **yes** — a season's chart-mix rows change the level |
| `PumbilityProjectionCache.Key(userId, mix)` (PlayerProgress; evicted by `PumbilityProjectionCacheConsumer`) | per user × mix | the projection sweep: all-time peers against the viewer's pool | **yes** — the viewer's pool is seasonal |
| `PumbilityCohortCache` key (PlayerProgress) | mix × pool × band | the peer cohort reading; the viewer's fifty is placed onto it per request, never stored (its own header says so) | no |
| `BlendedTierListHandler`, `PersonalizedBreakdownHandler`, `ProjectedScoresHandler` (ChartIntelligence) | mix × lens × type × level × user | the personalized lens over the viewer's scores | **yes** for the user-keyed variants; the `"community"` variants stay all-time |
| `PumbilityFoldersHandler`, `PumbilityPoolCompositionHandler` (ChartIntelligence) | mix (× user) | community pools; the user variant reads the viewer's pool | user variant **yes**, community no |
| `PeerStandingReader.RowsKey(mix, setKey, chartId)` and its roster/levels/band/members keys (Rivals) | mix × peer set (× chart) | the peers' rows | no for peer sets (all-time by D6); **yes** when the set is the viewer's rivals (D6, rivals flip) |
| `SearchChartsHandler` `ChartSearch__Community__{mix}` (Catalog) | per mix | the community vocabulary and aggregates; the viewer's bests are read outside the cache | no |
| `ChartUrlResolver__{mix}` (Web) | per mix | slugs | no |
| `ChartCatalogCache` (Web, home widgets) | per mix, **circuit-scoped** | the widget chart catalog from `GetChartsQuery` | no — a circuit holds one view at a time, so it follows whatever the query returns |
| `RecapSaga` top-50 sets `(mix, "Top50", type, userId)` and `HighlightCaptureSaga` cohort `(mix, "Cohort", userId, type, level)` (PlayerProgress, both import-time) | per user × mix | one player's own top-50 chart ids; the scores at one level of the players around one player — the user id stands in for their competitive band | **yes, by shape** — each entry is one player's, so it declares `Viewer`; both consumers read all-time (D6, D18) and, running at import with no ambient view (§4.5), pass all-time explicitly once the segment lands. Declared `Mix` in the first cut; the slice 0 bug check (2026-09-12) caught it, and the ratchet's second fact now would. |
| `EFTierListRepository.TierListKey(mix, name)`, `ChartVerdictHandler` keys, `EFChartFolderBaselineRepository.CacheKey(mix)`, `CohortScoreProvider` keys, `RecapSaga` shared inputs, `PlayerHighlightCapturer` rarity, `EFTitleRepository.CacheKey(mix)`, `EFCommunitiesRepository` `CompRanges_{mix}`, `BoardPeerReader` keys, `OfficialCacheKeys.*`, `LedgerCacheKeys.*` (Limbo, population, stage breaks), Weekly and Daily Step keys, tournament keys, `EFLedgerStatsRepository`, `EFChartRepository` videos / song names / `MixLevelsCacheKey`, `EFUserRepository`, `ShellModelFactory`, `FrontDoor`, `AccountProofService`, `ToolKeySaga`, `ToolHostAllowlist`, `EFChartStepChartRepository`, `EFChartSkillMetricRepository`, `GetChartMetricsHandler`, `EFAvatarRepository`, `EFArchivedSkillTagRepository`, `SkiaShareCardRenderer`, `SendGridAdminNotificationClient` | various | community projections, catalog facts, identity, settings, tooling | **no** — all-time or view-free by design (D6, D18) |

So the view-sensitive set is small — four classes for certain, three handlers' user variants, one
reader's rival sets, two import-time per-player caches that will hand it all-time explicitly — and
the point of the builder is that the rest declare themselves all-time on purpose rather than by
omission.

**The builder.** A static `CacheKeys` in `ScoreTracker.SharedKernel` (a pure string function over
`MixEnum` and parts; SharedKernel references nothing and needs nothing here), with two entry points
that produce the same shape today and diverge in slice 1a:

- `CacheKeys.Mix(owner, mix, parts…)` — a key that must never vary by view (community projections,
  catalog facts). This is a declaration, and the ratchet lets it through.
- `CacheKeys.Viewer(owner, mix, parts…)` — a key whose object depends on the viewer's own scores or
  on chart levels. In slice 1a it gains the season segment (`Guid.Empty` = all-time) and every caller
  in the table above passes the view through.

`LedgerCacheKeys` and `OfficialCacheKeys` stay where they are (their vertical-local names are
load-bearing for the eviction pairs) and build through the shared one. Per-user keys with no mix
(`SettingsCacheKey(userId)`, feedback, claims) are out of scope: they hold no score.

**The ratchet.** `ArchitectureTests/CacheKeyTests` in the `UiColorTokenTests` shape: scan production
`.cs` and `.razor` for (a) a memory-cache call (`GetOrCreate`, `GetOrCreateAsync`, `Set`, `Remove`,
`TryGetValue`) whose key argument is a string literal or interpolation containing a mix token (`mix`,
`Mix`, `mixId`, `MixId`, `MixEnum`), and (b) any method or constant named `*Key*` whose body is an
interpolation containing a mix token, outside `SharedKernel/CacheKeys.cs`, `LedgerCacheKeys.cs`,
`OfficialCacheKeys.cs`. Shrink-only allowlist, empty at the end of slice 0. It cannot see a key built
from a variable it does not recognize; that is why the builder is the primary control and the ratchet
the backstop. A second fact guards the builder's other door: a `CacheKeys.Mix` call whose parts name
a player (`userId`, `viewerId`, `playerId`) fails outright, no allowance — a Mix key never varies by
who is looking, so an entry that is one player's belongs under `Viewer`. The first fact cannot see
that: the key is built by the builder, through the wrong door. The slice 0 bug check (2026-09-12)
found the two above declared Mix.

**Reach.** Production files touched: ScoreLedger (`EFPhoenixRecordsRepository`, `EFAccountPurgeRepository`,
`LedgerCacheKeys`), PlayerProgress (`EFPlayerStatsRepository`, `EFTitleRepository`, `PumbilityProjectionCache`,
`PumbilityCohortCache`, `CohortScoreProvider`, `RecapSaga`, `PlayerHighlightCapturer`, `HighlightCaptureSaga`),
ChartIntelligence (`EFTierListRepository`, `ChartVerdictHandler`, the five handlers above), Catalog
(`EFChartRepository`, `EFChartFolderBaselineRepository`, `SearchChartsHandler`), Rivals (`PeerStandingReader`),
Communities (`EFCommunitiesRepository`), WeeklyChallenge (`EFWeeklyTourneyRepository`, `EFDailyStepRepository`,
`WeeklyTournamentSaga`), OfficialMirror (`OfficialCacheKeys` delegating), Web (`ChartUrlResolver`,
`ChartCatalogCache`), plus the new `SharedKernel/CacheKeys.cs` and the new test. Roughly thirty files,
every edit a key expression swapped for a builder call. Tests that spell keys today
(`EFMoMRepositoryTests`' `TourneyCacheKey`, the exploration `PeerCacheProbeTests`) follow. No entity,
no migration, no contract, no page markup, no job, no resx, no config.

**Blast radius.** Behavior: none intended — the strings change shape, and a deploy cold-starts every
in-memory cache anyway, so nothing observable moves. The one real risk is a writer/evictor pair that
ends up spelled differently after the refactor, which is a stale-cache bug that only shows under
traffic (the peer-cache and Limbo eviction paths have integration coverage; the record-score cache's
four evictors and the projection cache's consumer do not, and get a unit test each that the evict key
equals the write key). Rollback is a revert with no data implication. Nothing outside the process is
touched: no SQL, no API shape, no partner-visible change.

### 12.2 Slice 1a — the schema, reading nothing new: where it lands

- **Entities and keys.** `ScoreLedger/Infrastructure/Entities/PhoenixRecordEntity.cs` (unique
  `(UserId, ChartId, MixId)` → add `SeasonId`), `PlayerProgress/Infrastructure/Entities/PlayerStatsEntity.cs`
  (PK), `PlayerFolderLevelEntity.cs` (PK), `Data/Persistence/Entities/ChartMixEntity.cs` (unique
  `(ChartId, MixId)`); registrations in `ScoreLedgerModelContribution`, `PlayerProgressModelContribution`
  and `ChartAttemptDbContext`. One migration from `ScoreTracker.Data` with the CompositionRoot startup
  project; both index names spelled out; `SeasonId` default `Guid.Empty` on every existing row.
- **Global query filters** on the four entities in the same contributions; `IgnoreQueryFilters()` only
  inside the seasonal read paths that arrive in 1b.
- **The audit** (§6.4): `PeerScoreStore` gains `AND SeasonId = @allTime` in both raw statements;
  `DevCatalogWriter`'s delete is already correct. A content test in `Tests.Api` asserts a seeded
  seasonal row never appears in players/stats, charts, chart scores or a webhook payload.
- **The Seasons vertical skeleton**: `ScoreTracker.Seasons` with `Contracts/`, `Wiring/` (`AddSeasons()`,
  `AddSeasonsConsumers()`, `SeasonsModelContribution` registering `scores.Season`), listed in
  `VerticalModelContributions.All()`; referenced from `Web` and `CompositionRoot`; package allowlist
  row in `CLAUDE.md`.
- **The flag**: `Seasons:EnableUI` bound at startup in `Program.cs`, forwarded from the AppHost like
  the other sections, exposed through a small `ISeasonsUiGate` (true for admins always). Nothing reads
  it yet.
- **`CacheKeys.Viewer` takes the view** (§12.1): a season parameter, `Guid.Empty` = all-time, and every
  `Viewer` caller passes it — the page's view where a request carries one (all-time until the flips in
  slice 2), and all-time explicitly at the two import-time sites, `RecapSaga`'s top-50 sets and
  `HighlightCaptureSaga`'s cohort, background consumers having no ambient view (§4.5). Nothing reads a
  season yet, so every key still carries all-time.

### 12.3 Slice 1b — tracking begins: where it lands

- **The writer.** `OfficialLeaderboardSaga.SaveBests` already knows the session and the mix; it passes
  the counting-rule facts (play time, whether the card raised an existing record) down to
  `UpdatePhoenixRecordHandler`, which upserts the seasonal row with the same `BestAttemptPolicy` when
  the source is `officialImport` and the running season's window holds the play (D15). Manual, CSV
  and API paths never reach it.
- **The season pass.** `HighlightCaptureSaga.Consume` runs `PlayerRatingSaga.CaptureSessionStats` a
  second time with the season, `quiet: true`; the folder-level write in the same saga runs twice the
  same way. `RebuildLatestSessionsConsumer` and the flush rail replay both.
- **The reader.** `IScoreReader` gains a `ScoreScope` parameter (default all-time) on `GetBestScores`,
  `GetChartScores`, `GetPlayerScores` (both overloads), `GetClearCount`, `GetChartScoreAggregates`;
  `EFPhoenixRecordsRepository` implements it with `IgnoreQueryFilters()` + `SeasonId == scope`.
  `IChartRepository.GetCharts(mix)` gains the same for the season's chart-mix rows.
- **The roll.** `RecurringJobRunner.PublishRollSeason` + the `Program.cs` registration
  (`roll-season`, `15 11 * * *`) → `RollSeasonCommand` → `SeasonRollSaga` in the Seasons vertical:
  create the quarter's row (MoM's `CurrentQuarter` / `EndOfSeason` arithmetic, copied), seal ended
  seasons past seven days (archive copy per table, delete in batches of 10,000, `SealedAt`,
  `SeasonSealedEvent`). Row in `docs/SCHEDULED-JOBS.md`.
- **The rollup.** `rollup-season-stats` nightly → every player's season `PlayerStats` row from seasonal
  bests (the same `RecalculateCore` with the season).
- **The archive.** `SeasonPersonalBest`, `SeasonStanding`, `SeasonFolderLevel`, `SeasonChartLevel`
  entities in the Seasons vertical, the user-keyed three in its `UserOwned` manifest; rows in
  `docs/DATABASE-SCHEMA.md`.
- **Undo and delete.** `SessionUndoReplay` / `SessionReplayBuilder` rebuild the seasonal rows with the
  window applied; `WipeUserScoresCommand` covers the seasonal rows by the same `UserId`; the account
  purge covers the archive through the manifest.
- **The backfill.** An admin button publishing `BackfillSeasonsCommand`: per quarter since Summer 2026,
  replay the journal into seasonal bests, compute standings, archive the ended quarters, leave the
  running one live and flat.
- **The console.** `Pages/Admin/Seasons.razor`: live and next season, roll now, backfill, re-price a
  season; the rating pin and the dry run arrive with slices 2b and 4.
- **Tests.** `Tests.Integration`: writer (in-window play, best-list upscore, first-ever importer,
  manual excluded), replay on undo, seal idempotency and resume, backfill idempotency, purge of the
  archive through the four-way ratchet. `Tests`: the counting rule as a pure policy, the roll's
  quarter arithmetic, the message taxonomy and JSON round trip of `RollSeasonCommand` /
  `SeasonSealedEvent`.

Slices 2 onward are specified by §4.5, §8, §13 and D25–D32; each one's PR opens by copying the
relevant rows of §12 and the sheet it is gated on into its description.

## 13. Mocks, and which slice each one gates

Slices 0 and 1 have no player-facing surface and need no mock. Every later slice is gated on the
sheet that settles its visual decisions; a sheet is one artifact built on production-synced data.

| Sheet | Gates | What it settles |
|---|---|---|
| **A · The view** | slice 2 | The picker's Phoenix 2 entry with *All-time* / *Fall 2026 season* as radio rows (desktop menu and the mobile More sheet, running season only); the pill in both states and where the days-left count lives; the intro dialog (D25) with its copy; the excluded-page caption (D21) in place; the Account page peers disclaimer (D26). |
| **B · The marker in place** | slice 2 | The corner chevron at 64 / 40 / 24 px *inside* the surfaces that draw bubbles: a tier-list card, a chart-search row, the chart page header, a PUMBILITY chart card, the share card; the size ladder; magnitude by chevron count (chosen) against a signed pill. Settles glyph, size floor and the token pair. |
| **C · The flipped surfaces** | slice 2 | The PUMBILITY frame header on day one ("Fall 2026 · day 1", 0) and mid-season; the Play page with the all-time-peers caption; the Breakdown's seasonal fifty; the Hardmode tab in seasonal view (your season standing, the locked list); the chart page record card with season best and all-time best together and the *This season* scope chip; the session card line, web and Discord (text, since Discord bubbles are emoji). |
| **D · The season boards** | slice 3 | The boards page: standing card, tabs including Hardmode (D30), a board under the floor, previous / next, a sealed past season; the past-seasons dialog rows; the Season widget at its sizes. |
| **E · What moved** | slice 4 | The What-moved page per folder with hold counts. The admin dry run needs no mock. |

Round 1 of the mocks was published 2026-09-12, every figure DrMurloc's real Phoenix 2 data as of that
day (the mock's "now" is Nov 14, Fall 2026, with Summer 2026 sealed; the few season numbers that have
to differ from all-time to show a state are marked ~). Not checked in — each sheet embeds its art
(about 1.6 MB). **Sources, data pulls and the regeneration recipe live in the owner's
`Downloads\seasons-mocks-2026-09-12\` (README inside)**: the four sheets, `assets.js`, `mockkit.js`,
`build_assets.py`, `sql/q1–q5.sql` with their outputs, and the downscaled art. The artifacts
themselves are readable by Claude through the Artifact tool (`read` with the URL).

| Sheet | Artifact |
|---|---|
| A · The view | https://claude.ai/code/artifact/f0e9deaf-4f10-4f21-9d99-3dcf0b3e672f |
| B · The marker in place | https://claude.ai/code/artifact/466830b0-aecf-493b-87a0-3b3557e7f802 |
| C · The flipped surfaces | https://claude.ai/code/artifact/9021bbd9-674a-4c0a-9584-5ff2e24146a5 |
| D · The season page (What moved folded in as a section) | https://claude.ai/code/artifact/0f5495c1-68bd-4060-8051-edcbd4f7afd1 |

Sheet E was folded into D: What moved is a section of the season page reached from its hero, not a
route of its own.

Round 2 (same day, after the owner's six corrections): the picker lost its sealed-season entry and the
phone sheet uses the same radio rows; the Rivals caption became an Official Leaderboards one (Rivals
flips, D6); sheet B gained the magnitude section (chevron count vs signed pill, D8); sheet D's boards
were redrawn on the site's standard row (D32) with the owner's real rivals and clubmates highlighted,
and What moved carries jackets and the running total.

## 14. Open

- The 20 / 20 / 50 numbers in D7 are first guesses; re-read them after the first balanced season with
  the census in §5 re-run.
- Season rewards: what the top of a sealed board earns, and how many places.
- Whether folder completion in seasonal view should count broken plays as "played" the way the
  all-time folder does.
- Whether the Play page's "players holding your title" comparison stays visible in seasonal view (a
  seasonal number against an all-time cohort) or hides.

## 15. Docs to update in the build PRs

`CLAUDE.md` (the `Seasons` vertical in the layer table and the vertical list; the global-query-filter
convention; the cache-key ratchet), `docs/ARCHITECTURE.md` (vertical list, code map, the view beside
the mix in the shell section), `docs/DATABASE-SCHEMA.md` (`Season`, the four archive tables, the
`SeasonId` columns), `docs/SCHEDULED-JOBS.md` (`roll-season`, `rollup-season-stats`),
`docs/UX-GUIDELINES.md` (the season marker tokens and the caption rule), `docs/DOMAIN.md` (season,
season rating, seasonal personal best, TOTAL PUMBILITY), the nine `docs/LOCALIZATION-*.md` glossaries
(season vocabulary), and `docs/API.md` when the additive endpoints land.
