# Seasons

Status: **design complete; slices 0, 1a and 1b merged (2026-09-12 · 09-13 · 09-15); the feedback-form round of 2026-09-22 folded in as D39–D50 (the season formula, the Leaderboards vertical and its section, the two CO-OP boards, themes, the side boards, the retrofit); slice 1c next on the owner's go.** Scoped and decided 2026-09-12 with the owner
([scoping artifact, Round 2c](https://claude.ai/code/artifact/de8ed96c-d3c2-47fe-a25e-1c726348ff53):
the census, the re-scored feature table, the boolean analysis); mocks published and corrected the same
day (§13, four sheets, Round 2). The decisions in §3 are the owner's where marked and *decided unless
objected* otherwise. The build is the slow roll in §12; this document shipped with slice 0. If you are
picking this up cold: §1 says what it is, §3 what was decided and why, §12 what to build next and what
it touches (§12.2 is slice 1a), §4.7 how the boards are fed and ranked, §13 what it should look like.

Every quarter your Phoenix 2 scores start over on a seasonal board while your all-time record stays
exactly where it is. You re-grind. Every chart carries a **season rating** one folder up or down from
its printed level, moved by how much people pooled it last season, so the charts worth chasing change
each time. In **seasonal view** every leaderboard ranks PIU Scores players by what they did this
season. Official imports only. Nothing typed by hand. Everyone who imports is in.

The whole feature is one idea: **the score journal stays single, the personal-best pool forks.** A
seasonal personal best is a second best-attempt row in the same table, flagged for the season; every
number derived from personal bests (PUMBILITY, folder completion, the boards) gets a season-flagged
row written by the same code run a second time over the seasonal pool. Reads take the season. At the
roll, the ended season is stamped sealed and never written again; its rows stay where they are, cold
under their season number, and the next season starts from an empty pool.

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
- **Season PUMBILITY** is the Phoenix 2 formula over your seasonal bests at season ratings, with one
  change: every point counts. Between two grades the multiplier climbs continuously instead of stepping,
  and a Perfect Game pays half a rung more than a bare SSS+ (D39). Plates add exactly what they add
  all-time. The Total, Singles and Doubles pools; **Completion**, every Single and Double you passed
  this season summed, the "whole game" number (D40); and two CO-OP boards, a completion percent and a
  best-twenty PUMBILITY at the site's own co-op levels (D41, D42). Unrounded below the UI like every
  PUMBILITY.
- **The boards are one section, PIU Scores Leaderboards, read in both views** (D49): PUMBILITY,
  CO-OP, Hardmode, Completion and five **side boards** (plays, play time, Bads, Rough Games, notes hit;
  D48) over the all-time record under an all-time pill and over the season under the season pill.
  Seasonal view adds the season's **themes**: two or three hand-picked chart lists a season — Banya,
  a step artist, gimmicks, stamina — each a PUMBILITY board of its own (D46). Hardmode's season list is
  locked at the roll (D28, D45).
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
| D4 | **Official imports only. Manual, CSV and API writes never reach a seasonal row.** Quick record hides in seasonal view. | Owner. `ScoreJournalEntry.Source` already tells them apart; manual data stays sacred on the all-time side. Enforced in two places, because there are two write paths: `SeasonCountingPolicy` on the import, and the undo's `ReplaySeasons` — which must filter source *before* `BestOf`, since that treats a manual row as authoritative. |
| D5 | **No opt-in.** Every importer is in. | Owner. |
| D6 | **Peers stay all-time, and peer *selection* keys on your all-time standing.** On a peers surface only your scores swap. **Rivals flip:** the Rivals page, the rivals-of-you list and the head-to-head compare season scores on both sides, because a rival is a named site player who has season rows of their own. A board-only rival (an official-board tag) has no season data and shows all-time behind the existing asterisk mark. The Highlights feed stays all-time (it is play history). | Owner: peers all-time (2026-09-12); "Rivals page absolutely can support seasonal view" (same day, correcting Round 1's caption). The selection rule is the consequence of the first: chosen by a seasonal number, week one's peers are beginners. |
| D7 | **Season ratings are a symmetric diversity rule.** Per folder, charts ranked by weighted hold count in last season's pools (50 points at slot 1 → 1 at slot 50, the PUMBILITY tier lists' weighting): top 20% move −1, bottom 20% move +1, each from the chart's current season rating (D8); a chart in neither fifth **holds** where it is rather than snapping back to printed (decided unless objected — a snap-back would make a chart oscillate the season after it worked). Ties by fewer passers, then name. Folders with fewer than 50 folder-live players (three or more of their fifty in the folder) do not move. New charts enter at printed. | Owner: "underplayed charts are fine to rebalance too. The idea is diversity and to mix it up." The census (§5) showed the unheld charts are the unplayed ones, which is exactly what this rule moves. |
| D8 | **Season ratings compound.** Each roll moves a chart one step from its *current* season rating, bounded to levels 1–29; a chart in the bottom fifth two seasons running sits at +2, and the marker carries the magnitude by **chevron count** (▲, ▲▲, then a signed number from ±3 — owner, 2026-09-12, over the signed-pill option). The What-moved page prints the running total ("22 → 24, second season up"). | Owner, 2026-09-12: "charts can be up/downrated multiple seasons in a row … I'll keep an eye on if anything gets out of hand." The rule self-corrects: an uprated chart pays more, gets pooled more, and leaves the bottom fifth; a downrated staple pays less and gets dropped. Round 1's ±1 cap is withdrawn. |
| D9 | **Charts keep their printed level and folder everywhere.** The rating changes the price and the marker only. | Owner ("charts still show their official difficulty"). |
| D10 | **Seasonal rows live in the live tables, not in twin tables.** A season discriminator on the personal-best, player-stats, folder-level and Hardmode tables; every reader of those tables is audited once. Season ratings are the one exception, in their own table (D33). | Owner: "I would rather eat a one-time cost of auditing every place that uses the affected tables than permanently accept maintaining migrations for 2x the tables." |
| D11 | **The discriminator is a `smallint` season number, 0 = all-time — never a bit, never a Guid, never NULL.** The number is the calendar quarter, `YYYYQ`: 20263 is Summer 2026, 20264 is Fall 2026. | Owner, 2026-09-13 ("good call on smallint"; "index health is my biggest concern all around"). A bit cannot say WHICH season, and every past season's rows sit beside the running one forever — there are no archive tables (D14), so the discriminator has to name a quarter rather than mark a row as seasonal. A Guid spends sixteen bytes per index copy on a value with four distinct values a year. NULL was weighed and rejected: a key column cannot be NULL, and every season-taking query would grow an `IS NULL` branch that costs the seek. The calendar number is two bytes, monotonic and readable in raw SQL without a join. |
| D12 | **An EF global query filter on each flagged entity excludes seasonal rows unless a reader opts out of it.** Named `AllTime`, so a seasonal reader drops that one filter by name and nothing else. Raw SQL bypasses it and gets a ratchet; the purge and the wipe drop it on purpose, because a player's rows go in every season. | Decided unless objected. Turns the one-time audit into a permanent default: a reader added next year cannot leak seasonal rows by forgetting a filter. First use of a global filter in the codebase. |
| D13 | **There is no grace period, and the seal is a stamp rather than a wipe.** A season is closed at its boundary: exactly one season is ever writable, and the next roll stamps `SealedAt` on the one that just ended. From the boundary on, no import, undo or replay writes a row carrying that season's number, and the rows stay in place under it. **If you did not import before the season ended, those plays are lost.** The backfill is the one exception, and it is not an exception to the rule so much as to the inference: it replays a named window from the journal (D23), so it can rebuild a closed season that live tracking never saw. | Owner, 2026-09-14, overruling the seven-day grace decided a day earlier: *"UX and maintenance is much more complicated to explain for leaderboards shifting a week after the end."* A board that keeps moving for a week after the quarter closed has to be explained on every surface that shows it, and the thing it buys — a late importer's last week — is the thing the rule is asking players to do on time. The stamp-not-wipe half is the 2026-09-13 revision of the 2026-09-12 wipe-at-seal ruling, once season-first indexes (D34) made a sealed season a cold range with nothing to gain from a copy and a delete. |
| D14 | **No archive tables.** Past seasons are read from the same tables by season number; a sealed season's rows are a contiguous cold range in every season-first index (D34). If a table ever hurts, one season is one range to move. | Owner, 2026-09-13 ("lowers duplicate tables and column overhead"), replacing the four archive tables of the first cut. Growth is about 50k best rows a quarter today and ~250k at Phoenix 1 scale (§10.2). |
| D15 | **The counting rule.** The window is always the running season's, because that is the only one an import can write to (D13). A recently-played play dated inside it counts. A best-list card counts if it is dated inside it **or** it raised an all-time record you already had. Anything else — including a play dated in a season that has closed — is an old score being seen for the first time and does not count. | Decided unless objected. A Phoenix 2 best-list card is stamped with the chart's *first* play, so an in-season upscore can wear a pre-season date ([stage-breaks-and-max-combo.md](stage-breaks-and-max-combo.md) §6); the "raised an existing record" half catches it, and a brand-new importer's old cards stay out. |
| D16 | **Seasonal view is always the current season.** Past seasons are reachable from the Leaderboards section in seasonal view (previous / next, the past-seasons dialog; D49) and read the same tables by season number (D14); no other surface reads a past season. | Decided unless objected. Keeps every flag a bool at the read sites and confines the second read shape to one page family, the way MoM reads frozen sessions. |
| D17 | **One canonical URL per page; seasonal is view state** (setting + cookie), never a route or query string. Crawlers see all-time. | Decided unless objected. No duplicate content, no sitemap change, the static pages resolve it the way they resolve the mix. |
| D18 | **Not seasonal in v1:** titles, rating-history graphs, highlights and milestones, the community tier lists (Score / Pass / PG / Popularity / PUMBILITY lens), chart similarity, official-rank estimates. Tier-list *rows* show your seasonal score and the marker. | Decided unless objected. Titles are piugame facts; the rest are nightly community projections whose seasonal twins would double a six-job fan-out for a thin population. |
| D19 | **Per-score PUMBILITY (`PhoenixRecordStats`) is not mirrored.** Seasonal pools are priced live from seasonal bests. | Decided unless objected. It is a pricing cache of 1.08M rows the census work already prefers to bypass; the season pass prices its fifty in milliseconds. |
| D20 | **The boards are static SSR** — the section of D49, like the MoM season page — print their player count on every board, and under the floor say "12 players so far" instead of crowning three. | Decided unless objected. 376 Phoenix 2 importers today; honesty is copy, not code. |
| D21 | **Excluded pages carry one line under the title** while seasonal view is on: *All-time. Weekly Charts are not part of seasons.* Never a banner. | Decided unless objected. |
| D22 | **Deleting an account deletes its seasonal rows too, sealed seasons included** (the purge drops the all-time filter), leaving gaps in past boards, as the weekly placings already do. | Decided unless objected. Consistent with the site's delete-my-data stance; the alternative (a blanked placeholder) keeps a score row tied to a deleted person. |
| D23 | **Backfill replays the journal per quarter since Phoenix 2 launched.** Summer 2026 is written and sealed by the backfill; the season running at launch becomes the live one, flat. | Owner ("we'll just be backfilling from everyone's first entry into Phoenix 2"). |
| D24 | **A new `Seasons` vertical owns the season row and the roll.** The flagged rows stay with the verticals that own their tables, `ChartSeason` with Catalog (D33); the season pass is a second call into their existing sagas. *Amended 2026-09-22:* the pages it was to own are the Leaderboards section, owned by the `Leaderboards` vertical (D43, D49). | Decided unless objected. Follows the WeeklyChallenge template; the Seasons vertical references nothing back. |
| D25 | **The 50-card list is an accepted risk, and the player is told once.** The first time an account turns seasonal view on, a dialog explains the feature and says, in substance: *this depends on your recent scores, which piugame caps at 50. Import after each session, or mid-session if you play big ones.* Dismissed once (`Universal__SeasonsIntroSeen`), never again. | Owner, 2026-09-12: "accepted risk. always has been." The dialog is mocked before it is built (§13). |
| D26 | **The Account page's peers section carries a disclaimer**: peers, and what they scored, are always all-time; in seasonal view your own scores swap and theirs do not. | Owner. The Play page keeps all-time peers by D6; the explanation lives where the player picks their peer sources. |
| D27 | **Everything player-facing ships behind `Seasons:EnableUI`** (config, default false). Off: no picker option, no pill state, no captions, no marker, no season routes, no widget — for anyone who is not an admin. Admins always see the full UI. The data side (the season row, the flagged rows, the season pass, the roll, the rollup, the backfill, the admin console) runs regardless, so seasonal history accumulates behind the scenes before anyone else sees it. | Owner: "we're gonna build this a small bit at a time … maintaining seasonal stuff behind the scenes for a little bit before we expose it to users other than me." Same shape as `PreventRecurringJobs`: a flag read once at startup, flowing from the AppHost locally. |
| D28 | **Hardmode is a first-class citizen of a season: the season's hardmode chart list is locked at the roll and does not move until the next one.** The roll takes a copy of the all-time weekly list as it stands that day (the Hardmode census, board players included) and freezes it for the season; it does not run a second census of its own. The season running at launch takes the copy at launch. | Owner, 2026-09-12: "hard mode charts are identified at the beginning of the season and lock in until the next one." All-time Hardmode keeps its weekly recompute on the PUMBILITY page; a season gets a season-long board on the season page. Measured why the copy and not a site-only census: over PIU Scores pools alone the "every unheld chart" clause admits 2,242 charts today against the census's 1,211 with board players — the electorate is the point of that census, and a season should not redefine it. |
| D29 | **Season hardmode PUMBILITY** is the hardmode pooling — Singles, Doubles, Combined, no pool gate, ranked on the total however many charts are held — over *seasonal* bests restricted to the season's list, priced at season ratings. The same six columns the Hardmode build put on `PlayerStats` (`HardmodeRating`, `HardmodeSinglesRating`, `HardmodeDoublesRating` and the three `*ChartsHeld` counts), on the season's row. **Storage superseded by D45 (2026-09-22):** the number stands; its home is the standings table, and the six columns stay all-time only. | Follows from D28 and the standing Hardmode rules (no full-pool gate, ever). PIU Scores players only: official-board players do not import, so they are not in a season (their all-time hardmode rows live in `scores.OfficialHardmodeRating`, which seasons never touch). |
| D30 | **Where it shows.** The Leaderboards section's Hardmode page carries the season board (Singles · Doubles · Combined, season-long; D49 — "the season page" of the first design). The PUMBILITY page's Hardmode tab in seasonal view shows *your* season hardmode standing, pool and the locked qualifying list, and links to the season board; in all-time view it is exactly what the Hardmode build ships (weekly list, PIU Scores and Official Boards tabs). | Owner: "All-time does the weekly leaderboards in PUMBILITY page, and seasons has a season-long leaderboard on the season page." |
| D31 | **`scores.HardmodeChart` (ChartIntelligence, `HardmodeChartEntity`) gains the `SeasonId` discriminator** (D10 shape): the all-time rows are rewritten by the census (`HardmodeCensusSaga`, `RebuildHardmodeChartsCommand`), a season's rows are written once at the roll (a copy of that day's all-time list, D28) and stay in place at the seal (D14); the column itself lands in slice 1a with the others. **Superseded by D45 (2026-09-22):** the column landed and stays, at zero; a season's Hardmode list is a frozen list-board in the Leaderboards vertical. | Decided unless objected. No second census, no second weekly job. The Hardmode board merged to main on 2026-09-12 (PR #333), so slice 5 has no external dependency left. |
| D32 | **Season boards are the site's standard leaderboard row, nothing bespoke.** Rank through `RankDelta` (previous rank from the nightly rollup, so the arrows light up); the player through `UserLabel` with the 26px avatar and the flag wash under the name; the figures in `olb-grid` columns; row highlighting by the shared utility set with its precedence you → both → rival → community (`.olb-row-me`, `.is-both`, `.is-rival`, `.is-community`), which is how community members and rivals stand out on every board. Board-only rivals never appear (site players only, D29). | Owner, 2026-09-12: "make sure our leaderboards are using all the standard elements … avatar, flag, rank coloring, community/rivals highlighting." |
| D33 | **`ChartMix` is untouched; a season's chart ratings live in `scores.ChartSeason`**, keyed `(SeasonId, MixId, ChartId)`, sparse: a row only where the season rating differs from the printed level, carried forward each season while it differs. Owned by Catalog as chart data; written through a Catalog command by the roll (slice 4) and the admin pin (2b); reads overlay it on the printed level. | Owner, 2026-09-13: "ChartMix is sacred. They're two different concepts." |
| D34 | **Season-first keys and indexes.** `SeasonId` leads the primary keys of the stats, folder-level and Hardmode tables and the record table's board index, so all-time rows stay one dense range that seasonal inserts never split and each season appends as its own cold range. The one exception is the record table's unique index, which keeps the user first: EF's foreign-key convention would otherwise add a fourth index on `UserId`, and the per-player read seeks user then season regardless. | Owner, 2026-09-13 ("all the indexes you suggested make sense"); the exception found at build time. |
| D35 | **Seasons references nothing back.** ScoreLedger and PlayerProgress reference `Seasons.Contracts` for the events they consume; the question "which season holds this play, and is it still open" crosses as the Domain port `ISeasonReader`, implemented in the Seasons vertical, never as a join and never as a reference from Seasons outward. | Decided at the 1b build (2026-09-14). Keeps the vertical graph acyclic when later slices read season stats and bests from the other side. |
| D36 | **The season boundary is MoM's minute.** `SeasonCalendar` copies `MarchOfMurlocsHandler`'s quarter arithmetic and its `SeasonOffset` (UTC-5), so the two quarterly features never disagree about when a quarter ends; a season is closed from the second after that minute (D13). | Decided at the 1b build. One boundary, two features. |
| D37 | **The backfill never seals.** It creates every quarter since Summer 2026 unsealed and asks for their replays; the next `roll-season` tick (or Roll now on the console) stamps the ones whose windows have closed. | Decided at the 1b build: D13 says a backfill never writes a sealed season, and the seal stays in one place. |
| D38 | **Seasonal folder completion counts a broken in-window play as played**, exactly as the all-time folder does — the folder code runs twice, unchanged. | Inherited at the 1b build; §14's open item stands only if the owner wants the two to differ. |
| D39 | **Season PUMBILITY is the official Phoenix 2 formula with the continuous grade scale on and a Perfect Game half a rung above SSS+: 1.505.** Everything else is the official configuration untouched — the base curve, the Singles bump, the sub-10 zero, the additive plate table. Between two cutoffs a score earns a multiplier interpolated between the two rungs; inside the SSS+ band it climbs from 1.50 at 995,000 to 1.505 at a million. A third `ScoringConfiguration` factory beside PUMBILITY and PUMBILITY+, never merged with either; every seasonal pricing site uses it (the season pass, the Breakdown's live fifty, Play's gains, every season board) and no all-time surface does. The number keeps the word PUMBILITY — the pill is its qualifier, and the intro dialog (D25) says in one beat that season PUMBILITY is continuous and pays past SSS+. | Owner, 2026-09-22: "modified PUMBILITY for seasons, including continuous PUMBILITY gain between letter grades and extend the pumbility gain beyond SSS+ up to PG (like PUMBILITY+). Plate contribution remains the same"; on the size of the extension, "half a rung. We don't want it to be dramatic or else SSS+s become the meta." The ladder's top step is 0.01 a rung, so a Perfect Game is worth SSS+ plus 0.005 of base; PUMBILITY+'s 1.60 would have made the last 5,000 points worth ten rungs. It is not PUMBILITY+ — that has its own base curve, zeroes grades below A+ and ignores plates — hence a third configuration, and the same word rather than a third brand (decided unless objected). |
| D40 | **Completion is one board: TOTAL PUMBILITY over Singles and Doubles, every non-broken seasonal best summed at season ratings, no level split.** Co-ops are not in the sum (they have their own two boards, D41) and charts below level 10 price at zero, as the official rule says. There is no separate folder-completion board; folder completion stays a per-player figure on the player page. | Owner, 2026-09-22: "all one completion leaderboard, no separate boards for 20+ and 19 below. Meaning it's acceptable that someone playing all the 12s gets a ton of pumbility." Co-ops out and sub-10 at zero are decided unless objected: keeping co-ops inside the whole-game sum at a flat base would count them twice on a made-up price, and the sub-10 rule is the formula's. The column has existed on the season row since slice 1b; the all-time pass fills it with the same definition, so the word means one thing in both views. |
| D41 | **CO-OP is two boards.** *CO-OP Completion*: the official-shape CO-OP Rating (a flat base of 80, every co-op summed) as a percent of the all-Perfect-Game maximum over the mix's co-ops, with the passed-charts percentage the community page already computes as a secondary column. *CO-OP PUMBILITY*: the best twenty co-ops, each priced on the Doubles curve at its CO-OP Difficulty Level (D42), continuous in a season like everything else. The flat official rating stays what it is where it is official — the player page and the [CO-OP] title ladder — and stops being a board of its own. | Owner, 2026-09-22: "I want harder co ops priced higher. I think we have two boards of Co-Op completion (perhaps just a percent out of possible score across all co ops) [and] the Co-Op PUMBILITY (top 20 co op charts, PUMBILITY based off of our levels)." The Completion half is shippable now; the PUMBILITY half waits on D42. The secondary column is decided unless objected — it is the figure the owner kept on the community board in August. |
| D42 | **A CO-OP Difficulty Level is a catalog fact: one level per co-op chart on the 1–29 scale, in its own table beside the chart data, computed by a ChartIntelligence census that combines how hard the chart is to pass with how hard it is to score, and pinnable by hand in the console.** It replaces the per-position community vote (`CoOpRating`, up to five averaged levels per chart) as the thing that prices a co-op. The census method is a loop (§14): the site already anchors a Single or Double by weighting each player's score by the chart's distance from that player's competitive level; a co-op has no folder, but its players have Doubles competitive levels, and the co-op pass tier list per player count gives the pass side. The table is the truth, so a first pass can be seeded and hand-tuned, and a re-census is a re-price. | Owner, 2026-09-22: "This would replace CoOp scoring level algorithm which sucks with just CoOp Difficulty Levels. Needs to be some difficulty combination of how hard it is to pass and how hard it is to score. Tier listing is easy. Anchoring it to levels is the hard part." Where it lives and the anchoring sketch are decided unless objected. `ChartMix` stays untouched (D33). |
| D43 | **The PIU Scores boards are their own vertical, `Leaderboards`, fed by events.** It owns the section (D49), the board definitions and their frozen chart lists, the standings and the side-board aggregates; it ranks and never prices a personal number. PlayerProgress keeps writing the season stats row (your own number on the PUMBILITY section), Seasons keeps the calendar, Catalog keeps the chart facts (tags, co-op levels), ChartIntelligence keeps the Hardmode census. Two facts are missing today and get published: PlayerProgress publishes a plain bus fact after **every** pass, all-time and seasonal — the existing `PlayerStatsUpdatedEvent` is an in-process notification with no season on it, and the quiet season pass announces nothing — and ScoreLedger publishes a plays-journaled fact **after** the journal write, carrying the plays' chart ids, judgements and plates, because `ScoreImportCompletedEvent` fires before the rows exist. The Hardmode-rebuilt event, the season opened and sealed events, the chart-difficulty event and the session-undone event already exist and are consumed. A nightly rebuild from the published read ports is the truth the event path approximates (§4.7). | Owner, 2026-09-22: "I was thinking of having the PIU Scores leaderboards be their own topics/sagas or whatever that react to the events from score journal/personal best/player stats/etc." The vertical reads bests and stats through `IScoreReader` and the published stats reader, the catalog and the census through their published queries — never a join (ADR-001 D2). |
| D44 | **A board is one ranked number, and every board has the same standing row**: board key, season (0 = all-time), mix, player, value, charts held, rank, previous rank, as-of. A tab with three sub-boards is three keys (PUMBILITY Total · Singles · Doubles; Hardmode Singles · Doubles · Combined); the five side boards are five keys. Boards are projections over the pool: adding one late — a theme in week six — costs one rebuild, never a backfill. The nightly rebuild stamps yesterday's rank as the previous rank, which is what `RankDelta` lights up from. | Decided unless objected. One table, one page template, one purge-manifest entry; the section renders every view from the same shape, and the World card's deferred "your world rank" (communities-overhaul.md) falls out of it. |
| D45 | **A list-board is PUMBILITY over a frozen chart list, and Hardmode in a season is one.** The roll copies that day's all-time census list into the season's Hardmode board (D28), a theme's list is what its matcher resolved (D46), and a list-board's standing is the pool over seasonal bests restricted to the list, at season ratings, under season PUMBILITY. Supersedes the storage half of D29 and D31: the six Hardmode columns stay all-time only, and the Hardmode chart table's season column (slice 1a) stays at zero, unused; season Hardmode standings live in the standings table like every other board's. | Decided unless objected, once themes made the shape general. All-time Hardmode keeps its current implementation and the section renders it through the existing read; folding it into the list-board mechanism is a later cleanup (§14), not a gate. |
| D46 | **A theme is a matcher, and the list it resolves freezes for the season.** A `ThemeMatcher` is a rule over catalog facts — song artist contains, step artist is, chart carries tag, song type is, duration at least, level in range, chart type is, explicit list — combined with and, or and not, stored serialized on the theme, so "Banya" is authored once and re-resolves next year with the new Banya songs in it. The console previews a matcher as a dry run; the admin activates it for a season with hand includes and excludes on top; the resolved list freezes at activation and nothing re-resolves it until the next season. Two or three themes a season, chosen by hand with Claude, never auto-rotated; **seasonal only**, no all-time twin. | Owner, 2026-09-22: "we'll need some sort of abstract 'Theme matcher' class that lets us build out rules engines for these"; "I might need to work with you to come up with 2 or 3 of these per season to mix things up. I don't know that I want to auto-rotate these"; all-time boards yes, "not seasonal themed boards". Freeze at activation is decided unless objected — the same shape as Hardmode, census in, frozen list out — and it is what lets a theme be added mid-season: the seasonal bests already exist, so its board fills after one rebuild. |
| D47 | **Gimmick is a chart tag in Catalog.** A chart-tag table, hand-tagged now through the console, seedable later from the stepfile tempo-map detection proven on 2026-08-29 and never built. Tags are a matcher input; nothing else reads them in v1. | Owner, 2026-09-22: "Gimmick is meta data we'll need to tag in." Stamina has signals the catalog already carries — song type, duration, the piucenter Stamina-and-Runs badges, the steps-per-second the March of Murlocs rest-chart facts compute — so it starts as a rule to refine by census (§14), with the explicit list as the fallback matcher kind: "either come up with a good rule to target stamina charts, [or] refine down to a finite list." |
| D48 | **The side boards are five journal aggregates: plays, play time, Bad count, Rough Game count, notes hit.** Plays are official-import journal rows; play time is the song's duration summed over them; Bad count and notes hit (every judgement but a miss) sum the judgement counts on each row; Rough Game counts the plays whose plate was Rough Game. Both views: the all-time figures run from 2026-07-30, when non-best journaling began, and say so. Fed by the plays-journaled fact (D43), rebuilt nightly from the journal reader, a player's rows rebuilt on an undo. | Owner, 2026-09-22 (the five boards: "Total-play-time, Chart Play Count, Bad Count, Rough Game Count, Total notes hit (so all judgements except misses)"). Official-import rows only is decided unless objected — a hand-typed score is a score, not a play, and carries no judgements. A stage break counts the full song's length, and the 50-card ceiling (D25) applies as it does to everything read from the journal. |
| D49 | **Every board shows all-time too, on one PIU Scores Leaderboards section that reads the view; themes are the one seasonal-only kind.** The section absorbs the World community's Rankings tab and the Hardmode page's two board tabs: the Communities directory's World card links to the section, club and regional leaderboards stay on the community page, and the Hardmode page keeps your standing, pool and qualifying list and links out. In seasonal view the section carries the season chrome — the standing card, days left, previous / next, the past-seasons dialog, What moved — and the theme pages; in all-time view none of it. The all-time half is not a seasons surface and ships to everyone, outside `Seasons:EnableUI` (D27). Replaces the season-only "season page" of the first design; D16, D20 and D30 now read against the section. | Owner, 2026-09-22: "We should make sure all those leaderboards show for 'All time' too (not seasonal themed boards). I think we just end up with a PIU Scores Leaderboards page, and in seasonal view it adds the seasonal themes"; and "yes" to absorbing the World and Hardmode boards rather than rendering them twice. Retire, don't port. |
| D50 | **A sealed season's standings may be re-priced once, as a retrofit, when the formula changes before anyone has seen the boards.** The seal still freezes the bests (D13); the standings are a derivation of them. The rollup command already takes a named season and today prices only unsealed ones; named a sealed one explicitly it prices that season regardless, and the console exposes it as one press. Season PUMBILITY (D39) is the first use: Summer 2026, once, while `Seasons:EnableUI` is off. | Owner, 2026-09-22, over a standing "re-price" feature: "no. we'll allow a one time retrofit. If no one ever saw the leaderboards, they won't know that we repriced." |

Explicitly rejected: a separate season calendar from MoM's (two "seasons" on one site); twin tables
per flagged entity (D10); reading seasonal bests from the journal on every request instead of keeping
the seasonal personal-best rows (viable at 8 ms per player, but a second read shape everywhere the
record table is read; the flagged rows keep every read identical in shape); balancing season one from
all-time pools (D3); a difficulty-based rating read off the tier-list bands (the owner wants diversity,
not correctness). Since the 2026-09-22 round: a season-only boards page beside the all-time boards
(D49); a standing re-price feature for sealed seasons (D50 allows one retrofit); PUMBILITY+'s 1.60 for a
Perfect Game, and a third brand for the season number (D39); a folder-completion board (D40); the
per-position co-op vote as a price (D42); an all-time twin for a theme (D46).

## 4. The model

### 4.1 The seasonal personal best

The seasonal personal best is a row in the personal-best table with the season's id, keyed
`(UserId, SeasonId, ChartId, MixId)` (D34), written by the same handler that writes the all-time row
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

The window that matters is the **running** season's, and there is no grace (D13). Import before the
quarter ends or the plays you did not import are not in that season's boards — not late, not lost in a
queue, simply not counted, the same way a play that scrolled off the 50-card list before your next
import is not counted. A boundary is therefore the one moment the 50-card ceiling above has teeth: an
import on 1 October cannot seat anything in a season that ended on 30 September, however recent the
plays on the card are.

Undo-an-import replays the all-time bests from the surviving journal rows (`SessionUndoReplay`); the
seasonal rows are rebuilt by the same replay with the season window applied. Delete-my-data's mix wipe
deletes the seasonal rows with the all-time ones (same table, same `UserId`).

### 4.2 Season PUMBILITY and the player-stats rows

`PlayerRatingSaga.RecalculateCore` prices a player's bests, assembles the top-50 pools and writes
`PlayerStats`. The season pass is the same call with the season: it reads the seasonal bests through
the flagged reader, prices them with **season PUMBILITY** (D39) carrying the season ratings in
`ChartLevelSnapshot` (the per-chart override hook March of Murlocs already uses,
`includeLevelOverride: true`), and writes the season's `PlayerStats` row. Milestones and titles run
**quiet** on the season pass (the `quiet: true` path exists for bulk re-prices) — no seasonal lamps,
gains or titles in v1 (D18).

**Season PUMBILITY** is `Phoenix2PumbilityScoring` with two settings changed and nothing else:
`ContinuousLetterGradeScale = true`, so a score between two cutoffs earns a multiplier interpolated
between the two rungs, and `PgLetterGradeModifier = 1.505`, so the SSS+ band climbs from 1.50 at
995,000 to 1.505 at a million instead of lying flat. The base curve, the Singles bump, the sub-10 zero
and the additive plate table are the official ones, so a plate adds exactly what it adds all-time. It
is its own factory (`SeasonPumbilityScoring`), a third configuration beside PUMBILITY and PUMBILITY+
that is never merged with either (the March of Murlocs doc's §9.6 rule, extended). The all-time pass
calls the official factory exactly as before, so slice 1b's "this cannot have changed an all-time
number" stays a fact. Where the two differ most is the wide low bands: a 949,999 prices just under an
AAA rather than as an AA+. Every rung above S is a hundredth apart, so the top of the ladder barely
moves, and a Perfect Game is worth half of one of those hundredths more than a bare SSS+ — deliberately
not dramatic (D39).

**Completion** (D40) is the sum over every non-broken seasonal best of a Single or Double, at season
ratings under season PUMBILITY: the `TotalPumbility` column slice 1b put on the row, now filled on the
all-time row too with the same definition under the official formula, so the word means one thing in
both views. `TotalRating` keeps its own label and its co-ops. A co-op prices, in either formula, on the
Doubles curve at its CO-OP Difficulty Level once one exists (D42); until then it prices at the flat
official base, which only the CO-OP Completion board and the title ladder read (D41).

Your own seasonal number on a page is always the row; the Breakdown page's fifty are priced live from
the seasonal bests the way the top-50 handler prices all-time ones today (D19), under the same season
configuration. A sealed season's row can be re-priced once by the retrofit (D50): the rollup, named a
sealed season on purpose.

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
5. Write a `ChartSeason` row for season *N+1* for every chart whose rating differs from printed (§6.3,
   D33) — a chart back at printed gets no row — each carrying the printed level, the move this roll and
   the hold behind it; the What-moved section (§8.1) reads those rows.

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

- **The list is locked.** The roll copies that day's all-time census list (D28) into the season's
  Hardmode board — a frozen list-board in the Leaderboards vertical (D45), not the Hardmode chart
  table's season column, which stays at zero — and nothing rewrites it until the next roll. The
  volatility note is replaced by *locked for Fall 2026*.
- **The number is seasonal.** The same three pools over seasonal bests restricted to the season's list,
  priced at season ratings (a hardmode chart is low-hold by definition, so many carry a +1; that is the
  season's price and it applies here too), under season PUMBILITY (D39). Written as three standing rows —
  Singles, Doubles, Combined — by the Leaderboards vertical when the season pass's stats fact arrives
  (D43, D44), never on the season's `PlayerStats` row.
- **The board is season-long, on the section's Hardmode page in seasonal view** (D49), PIU Scores players only, with the standing card,
  pool count beside every total and the title rails asking for charts, as the all-time tab does.
- **The PUMBILITY page's Hardmode tab flips like the rest of the section**: your season standing, your
  season hardmode pool, the locked qualifying list by folder, a link to the season board.
- **Season one** has no sealed predecessor, so the season running at launch copies the all-time weekly
  list as it stands at launch and locks that.

The Hardmode board is on main; a season mirrors neither its table nor its six columns (D45) — it copies
the list and ranks it like any other list-board, so the only dependency is the published Hardmode query
the roll reads the list through.

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

### 4.7 The boards

The boards are a vertical of their own (D43), and the whole vertical is one idea: **a standing is a
projection over the pool, computed twice — once per event for one player, once a night for everybody.**

- **What comes in.** Three facts and four events. The **stats fact** PlayerProgress publishes after
  every pass carries the player's stats row and the season it was priced for: the PUMBILITY keys,
  Completion and CO-OP Completion take their value straight from the payload, and the list-board keys
  re-read that player's seasonal bests through `IScoreReader` — safe, because the pass ran after the
  writes — and price the pool over each frozen list. The **plays fact** ScoreLedger publishes after the
  journal write carries the plays themselves, and the side-board keys add them up (D48). The season
  events (D37) open and seal a season's boards; the Hardmode-rebuilt event refreshes the all-time
  Hardmode list; the chart-difficulty event re-prices the CO-OP board when a level moves (D42); a
  session-undone event rebuilds that player's rows.
- **What is written.** One standing row per board key, season, mix and player (D44). The event path
  upserts one player's rows. The nightly `rebuild-leaderboards` recomputes every row for all-time and
  every open season from the published ports, re-ranks each board and stamps the previous rank; it is
  the truth, the event path is the fast approximation, and a dropped in-memory message costs a day.
- **List-boards** (D45): `LeaderboardBoard` names the board and its kind, `LeaderboardBoardChart` holds
  its frozen list. Hardmode's season list is written by the roll from the census through the published
  Hardmode query; a theme's is written at activation by its matcher (D46), which is serialized on the
  board row so the same theme re-resolves next season.
- **Where the vertical must not look.** Bests and stats through the ports, the catalog through Catalog's
  queries, the census through ChartIntelligence's; never a join onto their tables. It references
  `Seasons.Contracts` for the season events, and nothing references it back.
- **What stays elsewhere.** The season stats row is still PlayerProgress's and still the number on the
  PUMBILITY section; a club's community page still ranks its members from it. The vertical only ranks.

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
  Id          smallint PK                      -- the calendar quarter, YYYYQ: 20264 = Fall 2026 (D11); never identity
  Name        nvarchar(100)                    -- "Fall 2026", MoM's SeasonName
  StartsAt    datetimeoffset
  EndsAt      datetimeoffset
  SealedAt    datetimeoffset NULL              -- set by the roll (D13); the seal
  IsBalanced  bit                              -- D3: false for every season running at launch
```

The key is the quarter itself, so MoM's anti-runaway unique index over (year, quarter) is the primary
key here.

### 6.2 The discriminator on the live tables (D10, D11, D34)

`SeasonId smallint NOT NULL DEFAULT 0` (0 = all-time, D11) on:

| Table | Today | After slice 1a | Owner |
|---|---|---|---|
| `scores.PhoenixRecord` (1.13M rows) | PK `Id`; unique `(UserId, ChartId, MixId)`; `(MixId, ChartId)` incl. user, score, plate, broken; `(ChartId)`, EF's foreign-key index | unique `(UserId, SeasonId, ChartId, MixId)`; `(SeasonId, MixId, ChartId)` with the same includes; `(ChartId)` unchanged (D34). Both rebuilt indexes named, built online, created before the old ones drop so uniqueness never lapses | ScoreLedger |
| `scores.PlayerStats` | PK `(UserId, MixId)` | PK `(SeasonId, UserId, MixId)`; + `TotalPumbility float` (empty until 1b) | PlayerProgress |
| `scores.PlayerFolderLevel` | PK `(UserId, MixId, ChartType, Level)` | PK `(SeasonId, UserId, MixId, ChartType, Level)` | PlayerProgress |
| `scores.HardmodeChart` | PK `(MixId, ChartId)` | PK `(SeasonId, MixId, ChartId)`; all-time rows rewritten by the census through the filter; the season rows D31 planned never come (D45), the column stays at zero | ChartIntelligence |

Every existing row gets 0, and the column keeps its default so the previous app version's inserts land
as all-time during the deploy window. `scores.ChartMix` is not touched (D33). Global query filters
(D12), named `AllTime`, on all four entities: `SeasonId == 0` unless the reader drops that filter by
name. `UserDataPurge` drops it always — a purge that ran through the filter would leave every season
row behind while the coverage test stayed green.

**Not mirrored:** `PhoenixRecordStats` (D19), `UserTitle` / `UserHighestTitle`, `PlayerHistory`,
`ScoreHighlight`, `PlayerMilestone`, `PlayerHighlight`, every ChartIntelligence table (D18) — Hardmode's
included since D45: its season column landed in 1a and stays at zero.

### 6.3 Season ratings (D33)

```
scores.ChartSeason                             -- Catalog, beside ChartMix
  SeasonId       smallint          PK, season first
  MixId          uniqueidentifier  PK
  ChartId        uniqueidentifier  PK; FK → Chart
  Level          int               -- the season rating, 1–29
  PrintedLevel   int               -- ChartMix.Level at the roll, so the marker survives a re-level
  MovedThisRoll  smallint          -- −1, 0, +1
  HoldWeight     float NULL        -- the weighted hold that ranked it; NULL when pinned
  HoldRank       int NULL
  LivePlayers    int NULL
  IsPinned       bit               -- set by the admin console (2b), cleared by the next roll
```

Sparse: a row exists only where `Level` differs from `PrintedLevel`, carried forward each season while
it differs; season one is flat and has none (D3), so the table holds roughly a thousand rows a season
once balancing runs. Reads overlay it on `ChartMix.Level`; a chart with no row is at printed. Written
by the roll (slice 4) and the admin pin (2b) through a Catalog command, never by a join from another
vertical. Registered through Catalog's `IDbModelContribution`; no user key, so no purge manifest.

### 6.4 The audit (D10)

The production code that touches the four flagged entities, measured 2026-09-12:

| Vertical | Files |
|---|---|
| ScoreLedger | `EFPhoenixRecordsRepository` (31 references, the one implementation of the record port), `EFScorePopulationRepository`, `EFLedgerStatsRepository`, `EFAccountPurgeRepository`, **`PeerScoreStore` (raw `DbDataReader` SQL against `scores.PhoenixRecord` and `scores.ChartMix` — the one production reader the query filter cannot cover)** |
| PlayerProgress | `EFPlayerStatsRepository`, `EFPlayerFolderLevelRepository`, `EFAccountPurgeRepository`, `EFHardmodeRatingRepository` (five sites on the stats entity, all through the filter; it writes the Hardmode pool totals onto the stats row, which go per season in slice 5) |
| Catalog | `EFChartRepository` (the per-mix chart dictionary; in 1b gains a per-season variant that overlays `ChartSeason`, D33) |
| ChartIntelligence | `EFHardmodeChartRepository` (rewrites the all-time list through a filtered query, so it needs no change; the season rows it must not touch are invisible to it) |
| Data | `ChartAttemptDbContext`; `DevCatalogWriter` (one raw `DELETE` by user, correct as is; its bulk copy sets `SeasonId = 0` by hand, because it builds rows from the live schema and refuses a null before SQL sees the default); `UserDataPurge` (deletes through EF and therefore through the filter — it drops the `AllTime` filter, or a purge would leave every season row behind) |

Twelve files, one raw-SQL reader, one filter-blind purge. Tests seed these tables in a handful of
places and keep working because the column defaults to all-time. The API v2 and webhook payloads read
through the same repositories and therefore through the filter; integration tests seed a season row per
table and assert every existing reader ignores it (the API approval suite mocks the mediator and cannot
see a row), and the purge decoy carries a season row so account deletion is proven to cross seasons.

`SeasonRawSqlTests` (§11) keeps production raw SQL honest from here on: a string literal naming one of
the four tables names `SeasonId`, deletes excepted because a delete by user crosses seasons the way the
purge does. The exploration workbench's probes (`PeerCacheProbeTests`, `HardmodeCensusProbeTests`,
`OfficialCensusProbeTests`, `RealSessionShowcaseTests`) read `scores.PhoenixRecord` raw with no season
predicate — manual-only and outside both the audit and the ratchet, but from 1b on they count rows
production never sees; add `SeasonId = 0` when one is next used.

### 6.5 The boards (D43–D48)

```
scores.LeaderboardBoard                        -- Leaderboards vertical: one row per board and season
  SeasonId      smallint          PK           -- 0 = all-time (D11)
  MixId         uniqueidentifier  PK
  Key           nvarchar(60)      PK           -- pumbility-total · pumbility-singles · … · hardmode-combined · theme-<slug>
  Kind          tinyint                        -- Pool50 · Pool20CoOp · SumAll · Ratio · ListPool · Plays
  Name          nvarchar(100)
  MatcherSpec   nvarchar(max) NULL             -- the serialized ThemeMatcher; themes only (D46)
  FrozenAt      datetimeoffset NULL            -- when the list was resolved: the roll for Hardmode, activation for a theme

scores.LeaderboardBoardChart                   -- the frozen list of a list-board (D45)
  SeasonId, MixId, Key, ChartId   PK

scores.LeaderboardStanding                     -- one row per board and player (D44); UserId is the purge key
  SeasonId, MixId, Key, UserId    PK           -- season first (D34)
  Value         float
  ChartsHeld    int
  Rank          int
  PreviousRank  int NULL                       -- stamped by the nightly rebuild
  AsOf          datetimeoffset

scores.ChartTag                                -- Catalog (D47)
  MixId, ChartId, Tag nvarchar(50) PK

scores.CoOpDifficulty                          -- Catalog (D42); written by ChartIntelligence's census and the admin pin
  MixId, ChartId    PK
  Level         int                            -- 1–29
  PassLevel     float NULL                     -- the two halves the census blended
  ScoreLevel    float NULL
  IsPinned      bit
  ComputedAt    datetimeoffset
```

Season-first keys throughout (D34), so a sealed season's standings are one cold range. The standings
table is the only user-keyed one and carries the vertical's purge manifest; the board and list tables
are catalog-shaped and need none. All five land in the migrations of the slices that first write them
(§12: 3, 5 and 8), never ahead of a writer.

## 7. Jobs, the roll, the backfill

| Job | Cadence | What it does |
|---|---|---|
| `roll-season` | Daily 11:15 UTC (after `try-schedule-mom`) | Stateless like MoM's: "does the quarter I stand in have its row?" → create it, `IsBalanced` per D3. Then, for any season whose window has closed and that is not yet sealed (no grace, D13): compute season ratings for the running season from the ended one (§4.3, only if the ended season `IsBalanced` or launch has passed), stamp `SealedAt` (D13: the rows stay where they are), publish `SeasonSealedEvent`. Idempotent at every step; a crash mid-way resumes on the next tick. |
| `rollup-season-stats` | Nightly | Recompute every player's season `PlayerStats` row from seasonal bests (0.3 s for everyone today), so a board never depends on an import having fired the pass. Prices every unsealed season; named a sealed one on purpose it prices that too — the retrofit (D50). |
| `rebuild-leaderboards` | Nightly, after the rollup and the roll | Recompute every standing for all-time and every open season from the published ports (D43, D44), re-rank each board, stamp yesterday's rank as the previous rank. The retrofit is the same rebuild for a named sealed season after its rollup has been re-run. |

The import path itself: `HighlightCaptureSaga` runs the rating step in-process today; the season pass
is a second in-process step behind it, failure-isolated, quiet (§4.2). The flush job that recovers
stranded batches covers it for free, and `RebuildLatestSessions` replays it.

**Backfill (D23)** is an admin button, one-shot and idempotent: for every quarter from Summer 2026 to
the running one, create the season row if missing and ask for its replay — the journal (official
imports, in-window, D15) into seasonal personal bests, then the standings through the rollup. It
never seals (D37): the ended quarters are stamped by the next `roll-season` tick, or by Roll now. It is
also the only thing that writes into a window that has already closed — a live import cannot (D13) — and
it may do so because it is handed the season rather than inferring one, replaying a window whose bounds
the roll already fixed. Season ratings are not backfilled (D3).

## 8. Surfaces

The full table with sizes is in the scoping artifact; this is the shape.

### 8.1 New

- **The PIU Scores Leaderboards section** (static SSR, the `Leaderboards` vertical, the Official
  Leaderboards section as the template: one frame, a routed page per board, an entry under Compete
  beside Official Leaderboards): **PUMBILITY** (Total · Singles · Doubles), **CO-OP** (CO-OP PUMBILITY ·
  CO-OP Completion, D41), **Hardmode** (Singles · Doubles · Combined; PIU Scores and Official Boards
  tabs in all-time view, PIU Scores only in a season, D29), **Completion** (D40), **Plays** (the five
  side boards, D48), and in seasonal view one page per theme (D46). Every board reads the view (D49):
  all-time numbers under an all-time pill, the season's under the season pill, with the season chrome
  only in seasonal view — your standing card, days left, previous / next, the past-seasons dialog (lazy
  island, MoM's), What moved. Past seasons read the same tables by season number (D14, D16). Player
  counts on every board and the floor copy (D20); the standard row (D32). Absorbs the World community's
  Rankings tab and the Hardmode page's two board tabs (D49). `/Leaderboards` is free; the theme route is
  mocked with Sheet D.
- **What moved this season**: a section of the Leaderboards page in seasonal view (D49), per folder: the charts that moved up and
  down this roll with jackets, the hold counts behind them and the running total from printed
  ("22 → 24 · second season up"), from `ChartSeason` (D33). The trust device for the rebalance and the
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
  keyed off `Universal__SeasonsIntroSeen`, the pattern the recap pointer popup used). Four beats: what a
  season is, what counts, that season PUMBILITY is continuous and pays past SSS+ (D39), the 50-card
  import advice. One button.
- **The Account page peers disclaimer** (D26): one paragraph in the peers section.

Everything in this section and §8.2 is gated by `Seasons:EnableUI` (D27) for non-admins — except the
all-time half of the Leaderboards section, which is not a seasons surface and ships to everyone (D49).

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

**Admin:** a season console — live and next season, roll now, the balancing dry run per folder, the
retrofit re-price of a named season (D50), pin one chart's season rating by hand, the backfill button —
and the boards' console: theme definitions with the matcher editor, its dry run and the per-season
activation with hand includes and excludes (D46), chart tags (D47), the CO-OP Difficulty Level pin (D42).

## 9. Where the flag is not enough

Under D10 every one of these is the same move: a mirrored write with the seasonal pool.

1. **Numbers somebody already wrote** — `PlayerStats`, folder levels, and everything in D18. The
   season pass writes the first two; the rest stay all-time.
2. **Ranking everybody** — the Leaderboards vertical's standings (D43, D44), fed by the stats fact and
   rebuilt nightly; a club's community Rankings sort the season's `PlayerStats` rows, which the nightly
   rollup guarantees exist.
3. **Writes during import** — the session card line and any Discord card are an extra pricing pass, not
   a flipped read.
4. **The season ratings** — made by the roll, read through the chart reader's flag.
5. **The seal** — the `SealedAt` stamp (D13) and the rule behind it: the writer, undo, replay and the
   backfill refuse a sealed season's number, so nothing is ever written to a season after its seal.
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
| 6 | **Season boundaries** (D11, D13). | A play imported the minute before the boundary lands on the season that is ending; the same play imported a minute after lands nowhere, and an upscore imported after it lands on the season that just started. Seal or no seal, a closed season takes nothing. The roll is idempotent when triggered twice, and resumes after a crash mid-copy. |
| 7 | **Static pages and the canonical URL** (D17). | The chart page and the boards render the view from the cookie with no circuit; anonymous default is all-time; no seasonal URL exists and the sitemap is unchanged. |
| 8 | **The flag** (D27). | With `Seasons:EnableUI` off, a non-admin sees no picker option, pill state, caption, marker, route or widget, while the admin sees all of it and the data side keeps running (rows, roll, rollup). |
| 9 | **Quiet season pass** (§4.2, D18). | An import in seasonal territory emits no seasonal milestone, lamp, title, highlight or Discord line beyond the one session-card line. |
| 10 | **Season ratings** (§4.3, D3, D8). | Season one flat. First balanced season: every moved chart is one step from where it stood, a fifth each way per eligible folder, skipped folders and the middle three fifths hold, new charts at printed, nothing outside 1–29, the What-moved section matches the dry run. Pricing on the Breakdown page uses the season rating; the folder a chart sits in does not move. |
| 11 | **Hardmode in a season** (§4.6, D28–D31). | The season's list does not change between rolls while the all-time list moves weekly. The season hardmode number counts only seasonal bests on the season's list, at season ratings, with no pool gate. The season page's Hardmode tab and the PUMBILITY page's Hardmode tab in seasonal view agree on your standing. Season one's list equals the all-time list at launch. |
| 12 | **Season PUMBILITY** (D39, D50). | Every all-time number is identical before and after, to the cent. In a season a 999,999 SSS+ prices above a 995,000 SSS+ by less than a rung's worth; a million prices at 1.505 of base plus the Perfect Game plate; a 949,999 prices just under an AAA rather than as an AA+; a plate adds exactly what it adds all-time. The retrofit re-prices Summer 2026 once, and a second press changes nothing. |
| 13 | **Standings** (D43, D44). | For every board the nightly rebuild lands on the numbers the event path produced. Previous ranks light the arrows the morning after a move. A theme activated in week six has standings after one rebuild. An undone session drops its plays from the side boards. The purge removes a player's standings in every season, and the decoy keeps its rows. The all-time boards match the World Rankings and the Hardmode tabs they replaced, row for row, on the day they replace them. |
| 14 | **CO-OP** (D41, D42). | CO-OP Completion is the flat rating over the all-Perfect-Game maximum; CO-OP PUMBILITY is the best twenty at site levels and moves when a level is pinned; the title ladder and the player page still read the flat official rating. |
| 15 | **Themes** (D46, D47). | A matcher's dry run equals the list it freezes; hand includes and excludes survive; the frozen list does not change when a song is re-tagged or a new Banya song lands mid-season, and does change at the next season's activation. A theme appears only in seasonal view. |

### 10.2 Size and growth

| Where | Today | At Phoenix 1 scale | Notes |
|---|---|---|---|
| Seasonal personal bests | ~50k rows per quarter (45,062 pairs measured) | ~250k | Permanent, in place (D14): about +4% of the record table per quarter today, each season its own cold range under the season-first indexes (D34). Three index rebuilds once, at the 1a migration. |
| All season rows together | ~50k bests + ~400 stats + ~16k folder rows + ~1k chart ratings a quarter | ~250k + 2k + 80k + 1k | Roughly 50–250 MB a year with indexes, against a database growing about 29 MB a day today (Azure storage metric, Aug–Sep 2026; 6% of the 50 GB cap used). |
| `PlayerStats` / folder / `ChartSeason` rows | hundreds / thousands / ~1k | thousands | Nothing. |
| Standings (D44) | players × keys — a few thousand rows a season, the same again for all-time | tens of thousands | Nothing; the rebuild is one pass over the ports. |
| `PhoenixRecordStats` | not mirrored (D19) | — | The one table where mirroring would have been 1.08M rows of pricing cache. |
| Compute at import | one extra pricing pass, milliseconds per player | same | Inside the existing failure-isolated chain. |
| Nightly rollup | 0.3 s for everyone | a few seconds | |
| The seal | one `UPDATE` on the season row | same | Nothing moves (D13). |
| Journal | unchanged by design | | Seasons should raise import frequency, which raises journal rows: the intended effect, perhaps +10–20%. |
| Memory caches | a second per-mix chart dictionary (a few MB); read models keyed by view double lazily | | The peer stores are not duplicated (D6). |

The one table that materially grows is the personal-best table, by a bounded amount per season and
small beside the journal.

## 11. Traps and ratchets that will go red if missed

- `AccountPurgeCoverageTests` / `AccountPurgeTests`: the season rows purge through the same `UserId` as
  the all-time ones — `UserDataPurge` drops the `AllTime` filter, and the decoy carries a season row
  per discriminated table.
- `AppHostForwardingTests`: the `Seasons` section is forwarded, or the flag reads empty locally.
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
- `SeasonRawSqlTests` (§6.4): production raw SQL naming a discriminated table names `SeasonId`; deletes
  excepted.
- Never edit an applied migration; the `PhoenixRecord` index change is a new migration with both index
  names spelled out.
- `AccountPurgeCoverageTests` / `AccountPurgeTests` again, for the Leaderboards vertical: the standings
  table names its `UserOwned` manifest and the decoy plants a standing row.
- `MassTransitResolvesEveryVerticalConsumer`: `AddLeaderboardsConsumers` joins both the `Program.cs`
  hook list and the test's own mirror of it.
- `BusMessageSerializationTests`: the stats fact carries a stats record and a `SeasonId`, the plays
  fact judgement counts and plates — every value type on them needs its converter on the type.
- `VerticalBoundaryTests`: the Leaderboards vertical's public surface is `Contracts/` and `Wiring/`;
  its reads go through ports and published queries.
- `SeasonRawSqlTests`: any raw SQL the rebuild reaches for names `SeasonId`.

## 12. The slow roll — slice order

Owner, 2026-09-12: infrastructure first, tracking seasonal bests behind the scenes; then the site flips
over one surface at a time behind `Seasons:EnableUI` so each can be tested alone; then the flag turns
on for everyone. Each slice is PR-shaped, lands green on its own, docs first and i18n last inside it.
"What you can test" is what the owner sees as an admin at the end of that slice; nothing seasonal in
slices 0–5 is visible to anyone else — the one exception is slice 3's all-time half of the Leaderboards
section, which is not a seasons surface (D49) and ships to everyone.

| # | Slice | What lands | What you can test | Gate to the next |
|---|---|---|---|---|
| 0 | **Cache-key builder and ratchet** — *built 2026-09-12, branch `claude/seasons-feature-scoping-ca5bc5`* | Every hand-spelled memory-cache key moved to `CacheKeys` (`Mix` / `Viewer`, with mix-id overloads for the catalog); `CacheKeyTests` ratchets it with an allowlist that started at 27 files / 32 statements and ended empty; `LedgerCacheKeys` and `OfficialCacheKeys` delegate. No feature code. | Nothing visible. Fast suites green; the allowlist is empty. | Merged. |
| 1a | **The schema, reading nothing new** — *merged 2026-09-13, PR #337* | `Seasons` vertical skeleton; `scores.Season` and `scores.ChartSeason` (D33); the `SeasonId` columns (`smallint`, 0 = all-time, D11) on the personal-best, player-stats, folder-level and Hardmode tables with season-first keys and indexes (D34); the named `AllTime` query filters (D12); the reader audit (§6.4) including the peer store's raw SQL and the filter-blind purge; `CacheKeys.Viewer` taking the season; the `Seasons:EnableUI` flag read at startup with the admin bypass (nothing behind it yet). No writer, no season row. | The whole site behaves exactly as before. The integration filter tests and the audit prove no read changed. This is the deploy that carries the index rebuilds on the record table, online and alone, so their cost is measured once and by itself. | A prod smoke after deploy: numbers on the PUMBILITY page, a community board and API v2 unchanged; then eyeball the peer warm and cohort reads, which now seek the renamed board index with fresh statistics. |
| 1b | **Tracking begins** — *built 2026-09-14, branch `claude/seasons-slice-1b`* | `roll-season` (create the quarter's row; seal one whose window has closed: `SealedAt`, D13); the seasonal write in the import chain with the counting rule (D15); the season pass in the rating saga (quiet) writing the season `PlayerStats` row and folder levels; the nightly rollup; the flagged score reader answering seasonal bests; undo/delete replay; the backfill button; the admin season console (the calendar, roll now, backfill). | Press **Backfill seasons**: every quarter since Summer 2026 appears with its pool rebuilt and its standings rolled up, the ended ones reading *Ended, not yet sealed* until the next roll stamps them (D37); your own seasonal personal bests exist in SQL; an import you make writes a row for the running season beside the all-time one; an undo removes it. Nothing player-facing changes. | Integration tests for the writer, the replay and the seal; one week of imports accumulating in prod while nobody sees them. |
| 1c | **Season PUMBILITY** | The `SeasonPumbilityScoring` factory (D39: the official config with the continuous scale on and a Perfect Game at 1.505); `RecalculateCore` prices a season with it and all-time with the official one; the rollup prices a named sealed season when asked (D50) and the console's re-price press; the all-time pass fills `TotalPumbility` with Completion's definition (D40); and the formula sanity check (§14) as a probe in the exploration workbench that re-prices every pool under both formulas against the prod-synced database and prints rank moves and the SSS+-to-PG share. No migration. | As admin: after Backfill and one re-price, Summer 2026's rows in SQL carry the continuous numbers and every all-time row is unchanged to the cent; a second press changes nothing; the probe's table reads sane. | Lands before the 2026-10-01 roll and the retrofit covers one sealed season; after it, the same press covers two. |
| 2a | **The view, with nothing flipped** | Picker option and pill — days left in the pill, where Sheet A put it (the last-days signposting, decided here, §14) — setting + cookie through the mix redirect (parsed through `SeasonId.TryFrom`, so a malformed cookie or query string never throws), the shell seed, the intro dialog (once), the caption on excluded pages, the Account peers disclaimer. Every page still shows all-time numbers. | As admin: switch to Fall 2026, see the pill everywhere including static pages, get the intro once, see the caption on Weekly Charts; a non-admin sees none of it. The cookie survives a new tab; the anonymous default is all-time. | Plumbing proven before any number depends on it. |
| 2b | **The marker** | `DifficultyBubble` overlay slot, chevron count (▲ / ▲▲ / signed number from ±3), the mix-invariant token pair, the chart page header line; the Discord text form. Ratings are flat, so nothing shows until the admin console pins one chart's season rating by hand. | Pin 4NT S22 to 21 in the console: ▼ appears on every bubble that draws 4NT in seasonal view, at every size, and nowhere in all-time view. Unpin, it vanishes. | Sheet B's ladder holds up in the real components. |
| 2c | **PUMBILITY section flips** | Frame number and bar, Play (all-time peers, your season scores, season gains), Breakdown's fifty and titles-worth (the title-cohort comparison is decided here, §14 — recommended hidden in seasonal view), day-one state; Phoenix 1 page hidden in seasonal view. | Your Fall 2026 number and fifty; Play's gains make sense; peers identical in both views; switching views and reloading never crosses numbers. | The first real surface on the flagged reader; the caches carry the view. |
| 2d | **Chart page and Chart search flip** | Record card with two numbers, the *This season* scope on the chart board, quick record hidden; search facets, states, min/max, export on seasonal bests; markers on every bubble. | A chart you played this season shows season best over all-time; the season scope lists the right players; the SRP filters on season scores. | Static page reads the view from the request correctly. |
| 2e | **Player, Community, Rivals flip** | Player page number, tiles, folder completion (broken-as-played is decided here, D38, §14); Community Rankings on the season stats rows, By Chart and play counts; the Rivals page and head-to-head on season scores both sides, board-only rivals asterisked. | Rankings of your club for Fall 2026; head-to-head against a rival on season scores; a board-only rival still shows the all-time sweep score with the mark. | |
| 2f | **The rest of the flips** | Tier-list rows (your score and marker; lists stay), the session card line and Discord line, the "ends in N days" line on the import surfaces (§14), the Season widget, existing widgets via the page context. | An import's session card shows the Fall 2026 gain and rank move; the widget shows your standing. | |
| 3 | **The Leaderboards vertical and the section, all-time first** | The vertical skeleton (`AddLeaderboards`, `AddLeaderboardsConsumers`, the model contribution); the standings table (D44) and the stats fact (D43); the `rebuild-leaderboards` job; the section (D49) with PUMBILITY, CO-OP Completion (D41), Hardmode through the existing read, Completion (D40); the Compete entry; the World card's link and the Hardmode page's board tabs retired into it; in seasonal view the season chrome and the season's boards, the past-seasons dialog, standard rows with highlighting. The seasonal half behind the flag route-wise; the all-time half for everyone. | The all-time boards match the World Rankings and the Hardmode tabs they replaced, row for row; the Completion census (§14) — the top ten read against the prod-synced database before it ships; Fall 2026's boards with your rivals and clubmates lit; Summer 2026 by season number; arrows the morning after a move. | Sheet D redone as the section in both views (§13). The first slice a non-admin sees anything of. |
| 4 | **Balancing** | The roll computes season ratings from the sealed season's pools (D7, compounding per D8), writes the `ChartSeason` rows (D33); the What-moved section on the season page; the admin dry run per folder. | Run the dry run against Fall 2026's pools today: 22 up and 22 down in S22, the lists match Sheet D's preview; nothing moves until the real roll. | Must be merged before the first roll you want balanced (the first roll after launch, D3). |
| 5 | **List-boards: Hardmode and themes** | `LeaderboardBoard` / `LeaderboardBoardChart` (D45); the roll copies that day's Hardmode list into the season's board; the `ThemeMatcher` kinds, the console's matcher editor, dry run and per-season activation (D46); the chart-tag table and its console (D47; the Gimmick tagging loop runs on it, §14); the Stamina rule iterated through the dry run against the local catalog until it reads right or becomes a finite list (§14); list-board standings from the stats fact and the rebuild; the section's Hardmode page in seasonal view and the theme pages; the PUMBILITY page's Hardmode tab in seasonal view with the locked note (D30). | Pick the first two or three themes here (§14). Activate a Banya theme for the running season: its list freezes, its board fills after one rebuild, a Banya song added to the catalog afterwards does not appear. The season's Hardmode list does not change when the weekly census rewrites the all-time one; your season Hardmode standing on both tabs agrees. | Slice 3 merged. The Stamina rule, the tagging and the first themes run inside it (§14). |
| 6 | **Flip the flag** | `Seasons:EnableUI = true` in production config. No code. Optional the same week: a front-door line and a Discord announcement. | Everyone sees what you have been seeing. | The §10.1 checklist walked once as a bug-check session; the backfill run; slices 1b–3 live for long enough that Fall 2026's boards are real. |
| 7 | **Side boards** | The plays-journaled fact from ScoreLedger, published after the journal write (D43); the five keys (D48) from the fact and from the journal reader on rebuild; the Plays page in both views with the "since 2026-07-30" line; an undo rebuilds the player's rows. | Your play count moves on the next import; undo the import and it moves back; the all-time figures match a hand count from the journal. | Slice 3 merged; can slot before 5. |
| 8 | **CO-OP levels and CO-OP PUMBILITY** | Two halves, with the anchoring census between them (§14). First: the probe (the pass-anchored and the score-anchored level for every co-op, side by side), `CoOpDifficulty` (D42) seeded from it, the console pin, the season and official configs pricing a co-op on the Doubles curve at its level when one exists, the CO-OP PUMBILITY board (D41) in both views re-priced by the chart-difficulty event — the board can go live on seeded and hand-pinned levels, because the table is the truth. Second, once the loop has chosen the blend: the census saga. | Pin a co-op two levels up in the console: the board re-prices; the flat CO-OP Rating on the player page does not move. | The loop between the halves: whether the two anchors agree well enough to blend, or the board changes shape. Slots anywhere after 3. |

Later, in this order of value: the all-time Hardmode board folded into the list-board mechanism (D45;
its six columns then stop being written), season rewards (decide how many places pay before a season
seals), the Discord season feed (a fourth `DiscordFeedKind`), API v2 (`season=` on players/stats and
charts, a seasons list, the boards — all additive), a per-season recap.

Three calendar notes. The Summer → Fall boundary is 2026-09-30; whenever slice 1b ships, the backfill
creates Summer 2026 sealed and Fall 2026 live and flat, so no boundary has to be caught live. The first
balanced season is whichever quarter begins at the first roll after slice 4 is in production — Winter
2027 if slice 4 lands before New Year, Spring 2027 otherwise; either way the season already running at
that point stays flat (D3). And slice 1c (D39) before the 2026-10-01 roll means the retrofit (D50) is
one press for one sealed season; landing after it, the press runs for Summer and again for Fall.

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
| `EFChartRepository.ChartCacheKey(mixId)` (Catalog) | per mix | the chart dictionary incl. `Chart.Level` | **yes** — a season's `ChartSeason` rows change the level |
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
- `CacheKeys.Viewer(owner, mix, season, parts…)` — a key whose object depends on the viewer's own scores
  or on chart levels. Since slice 1a it takes the season (`SeasonId.AllTime` = 0) as a required
  parameter, so no caller can forget it; every caller passes all-time until a seasonal reader exists.

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

Built 2026-09-13 on `claude/seasons-slice-1a`, eight commits, docs first.

- **The value type.** `SeasonId` in `ScoreTracker.SharedKernel/ValueTypes` — `short` backed, `AllTime` is 0,
  `From(year, quarter)` builds the calendar number, `Year` / `Quarter` read it back, JSON converter on the
  type per the bus rule, `InvalidSeasonIdException` beside `InvalidNameException`. It lives in the kernel
  because `CacheKeys` takes it.
- **Entities and keys.** `ScoreLedger/Infrastructure/Entities/PhoenixRecordEntity.cs` gains `SeasonId`; its
  indexes move from attributes into `ScoreLedgerModelContribution`, named, the two rebuilt ones online.
  `PlayerProgress/Infrastructure/Entities/PlayerStatsEntity.cs` (PK + `TotalPumbility`) and
  `PlayerFolderLevelEntity.cs` (PK); `ChartIntelligence/Infrastructure/Entities/HardmodeChartEntity.cs` (PK).
  New `Catalog/Infrastructure/Entities/ChartSeasonEntity.cs` registered in `CatalogModelContribution`; new
  `Seasons/Infrastructure/Entities/SeasonEntity.cs` in `SeasonsModelContribution`. One migration from
  `ScoreTracker.Data` with the CompositionRoot startup project, hand-edited: the record table's new indexes
  created before the old ones drop, each primary key dropped right before it returns so no table is
  without one for more than a statement, `SeasonId` default 0 on every existing row. Entities and migration land
  in one commit, because the migrate call refuses a model with pending changes and every integration and
  E2E run migrates from scratch.
- **Global query filters** (D12), named `AllTime`, on the four discriminated entities in their
  contributions. Nothing drops them yet except the purge.
- **The audit** (§6.4): `PeerScoreStore` gains `AND pr.SeasonId = 0` in both raw statements;
  `UserDataPurge.DeleteFor` / `DeleteForNullable` add `IgnoreQueryFilters()`; `DevCatalogWriter`'s raw
  delete is already correct, but its bulk copy names the season by hand — it builds rows from the live
  schema and refuses a null before SQL sees the default, which the integration run found; the Hardmode
  census needs nothing.
- **The Seasons vertical skeleton**: `ScoreTracker.Seasons` with `Contracts/` (`ISeasonsUiGate`), `Wiring/`
  (`AddSeasons()`, `SeasonsConfiguration`, `SeasonsModelContribution` registering `scores.Season`; the
  consumer hook arrives with the roll in 1b), listed in `VerticalModelContributions.All()`, `VerticalBoundaryTests`'
  markers and `AccountPurgeCoverageTests`' vertical list; referenced from `Web`, `CompositionRoot` and the
  test projects; package allowlist row in `CLAUDE.md`.
- **The flag**: `Seasons:EnableUI` bound in `Program.cs`, `Seasons` added to the AppHost's
  `forwardedSections`, exposed through `ISeasonsUiGate` (true for admins always). Nothing reads it yet.
- **`CacheKeys.Viewer` takes the season** (§12.1): a required `SeasonId` parameter on both overloads,
  every caller passing `SeasonId.AllTime` — the page's view where a request carries one (all-time until
  the flips in slice 2), and all-time explicitly at the two import-time sites, `RecapSaga`'s top-50 sets
  and `HighlightCaptureSaga`'s cohort, background consumers having no ambient view (§4.5).
- **Tests.** `Tests`: `SeasonIdTests`, `CacheKeysTests` (the season segment). `Tests.Integration`: the
  migration applies on every run; `SeasonFilterTests` seed a season row per discriminated table and assert
  each existing reader returns only the all-time row, once more through the peer store's raw path; the
  purge decoy carries a season row per discriminated table; the peer store's warm path, the everyone
  statement, is proven the same way; `SeasonRawSqlTests` ratchets the raw-SQL rule. On the final tree, after main was merged in: 4,720 · 231 · 1,490 · 428 · 93.
- **Post-deploy.** The migration bundle runs in the gated stage; read its duration, then the smoke of §12
  row 1a. Nothing to press.

### 12.3 Slice 1b — tracking begins: where it lands

Built 2026-09-14 on `claude/seasons-slice-1b`, seventeen commits, docs first and i18n last, no migration —
1a carried every column and table, so `has-pending-model-changes` stayed clean throughout.

- **The scope parameter.** `SeasonId season = default` — `default(SeasonId)` is all-time, so every existing
  caller compiles unchanged and still reads all-time. `SeasonId` is the scope; no separate type.
- **The ports.** `IScoreReader` and the Ledger's `IPhoenixRecordRepository` take the season on every method
  that touches the record table — twenty in `EFPhoenixRecordsRepository`, not the six a first draft named,
  because the change is one line per method and a half-scoped reader is a trap for the next slice. Each drops
  the `AllTime` filter by name and applies `SeasonId == season`; `ScoreCache` carries the season. `IPlayerStatsRepository`
  and the folder-level repository take it the same way (`DeleteStats` crosses seasons for the wipe);
  `IChartRepository.GetCharts` takes it and overlays `ChartSeason` on `ChartMix.Level` (D33) — an empty overlay
  until slice 4, so the pass calls it with the season now and slice 4 changes nothing in the pass.
  New Domain port **`ISeasonReader`** + `SeasonRecord`: which season holds a play time, and whether it is
  sealed (D35). `PlayerStatsRecord` gains `TotalPumbility` (column from 1a).
- **The Seasons vertical.** `SeasonCalendar` (MoM's quarter arithmetic and offset, D36, plus `HasEnded`),
  `ISeasonRepository` → `EFSeasonRepository : ISeasonRepository, ISeasonReader`, both registered by
  `AddSeasons`. `SeasonRollSaga : IConsumer<RollSeasonCommand>, IConsumer<BackfillSeasonsCommand>` — create
  the quarter's row if missing (`IsBalanced` per D3), seal seasons whose windows have closed (`SealedAt`, D13),
  publish `SeasonOpenedEvent` / `SeasonSealedEvent`; the backfill creates every quarter since Summer 2026
  unsealed and publishes `SeasonBackfillRequestedEvent` per quarter (D37). `AddSeasonsConsumers` joins the
  `Program.cs` hooks; `GetSeasonsQuery` joins the MediatR scan for the console.
- **The writer** (ScoreLedger). One `SeasonalBestWriter` applies the counting rule for both official
  paths, so the rule cannot drift between them. `UpdatePhoenixBestAttemptCommand` gains `RaisedExistingRecord`, which
  `OfficialLeaderboardSaga.SaveBests` already knows. `SeasonCountingPolicy` is D15 as a pure function —
  official import only, and only ever the running season (D13): a play dated inside it, or a best-list card
  that raised a record, which is a raise this import watched happen. A play dated in a closed season is
  lost, and an unsealed-but-ended season is as closed as a sealed one. `UpdatePhoenixRecordHandler`
  resolves the target through `ISeasonReader` + the policy after the all-time upsert and writes the
  seasonal row through the same repository with the season; the journal is untouched. Manual, CSV and API sources never reach it.
- **The season pass** (PlayerProgress). `CaptureSessionStats.Season`; `RecalculateCore(season)` reads scoped bests
  and charts, writes the season stats row and `TotalPumbility`, and runs quiet (D18). `HighlightCaptureSaga`
  runs it as a fourth failure-isolated step for the running season — the only one an import can have
  written to (D13) — then the folder-level write a second time with the season (D38). The Hardmode
  reprice is not repeated — slice 5.
- **The rollup.** `RollupSeasonStatsCommand` → the `RecalculateMixRatingsCommand` loop with the season, nightly,
  and once more per quarter when `SeasonalBestsBackfilledEvent` arrives.
- **Undo and delete.** `UndoScoreSessionHandler` replays `SessionUndoReplay.BestOf` once more for the running
  season, over official-import survivors inside its window; `WipeUserScoresHandler`'s deletes cross seasons; the
  account purge already does (1a).
- **The backfill chain.** `BackfillSeasonsCommand` (Seasons) → `SeasonBackfillRequestedEvent` per quarter →
  `SeasonalBestBackfillConsumer` (ScoreLedger: per user, official-import journal rows in the window through
  `SeasonalBestWriter.WriteInto`, which names the season instead of inferring it — a rebuild replays a window
  that has closed, and the counting rule would refuse every play in it) → `SeasonalBestsBackfilledEvent` →
  the rollup (PlayerProgress).
  ScoreLedger references `Seasons.Contracts` for the event; Seasons references nothing back (D35).
- **The console.** `Pages/Admin/Seasons.razor`: the seasons with their windows and seals, Roll now, Backfill
  seasons; linked from `/Admin`. The rating pin and the dry run arrive with slices 2b and 4.
- **Jobs.** `roll-season` (`15 11 * * *`, after `try-schedule-mom`) and `rollup-season-stats` (nightly), each a
  one-line publisher on `RecurringJobRunner` with its row in `docs/SCHEDULED-JOBS.md`.
- **What the build found.** Three things the plan had not settled. The **observed-plays path** is where a
  run below an all-time best becomes a seasonal best, and it is a second call site the plan named only in
  passing — the import filters those runs out before the record handler, so without it a season's pool
  would have held nothing but upscores. The **seasonal folder levels** cannot reuse the all-time ones: the
  calculator has to be re-run over the seasonal pool, or a player's season completion reads as their
  lifetime completion. And the **rollup's electorate** is who holds a seasonal best, not who already has a
  season stats row — the player whose pass failed has bests and no row, and is exactly who the rollup is
  for — which needed one new read, `IScoreReader.GetUsersWithRecords`.
- **What the regression check found** (2026-09-14, after CI was green). Six defects, all now fixed and
  pinned. The shape of five of them is the same: **a rule the read side already honored that the write
  side did not**. (1) Nothing gated the write path to Phoenix 2, so Phoenix 1 — the larger importing
  population — wrote seasonal rows that the rollup and the backfill both skip, orphans nothing would
  ever recompute; the gate is now one shared `MixCapabilities.HasSeasons()` rather than an array
  repeated per vertical. (2) The writer ignored the player's *record broken scores as your best*
  setting, so an opted-out player collected broken seasonal bests that D38 then counted as folders
  played; `IncludeBroken` rides `RecordObservedPlaysCommand`. (3) The undo's replay seated manual and
  CSV scores against D4 — and worse than a missing filter, `SessionUndoReplay` treats a manual row as
  *authoritative*, so a hand-typed number overwrote a real play even when lower. (4, 5)
  `DeleteBrokenRecords` crossed seasons while `CountBrokenRecords` did not, so the button's number and
  the delete disagreed and a player whose broken bests were only seasonal faced a disabled button over
  rows that existed; both cross now, while the pricing-cache delete deliberately does not, because
  `PhoenixRecordStats` is unmirrored (D19) and taking its chart ids across seasons deleted the pricing
  row of a chart passed all-time and only failed this season. (6) The seasonal score cache lost its
  expiry on the first write — `IMemoryCache.Set(key, value)` creates an entry with no options at all —
  and a season's entry cannot be evicted by key, so a wipe left the process serving deleted scores and
  the pass priced a board row from them. One more: the undo no longer deletes a seasonal best the
  window could not have produced, since a raise-clause row carries an out-of-window date by
  construction. Finding 7 — the ambiguity in which clause of the counting rule wins when two seasons are
  open at once — was dissolved rather than answered by the owner's no-grace ruling the same day (D13):
  two seasons are never open, so there is nothing for the clauses to disagree about.
- **Tests.** `Tests`: `SeasonCountingPolicyTests`, `SeasonCalendarTests`, `SeasonRollSagaTests`, the seasonal
  branch of `UpdatePhoenixRecordHandlerTests`, the quiet pass in `PlayerRatingSagaTests`; the message taxonomy
  and JSON round trip are the existing ratchets' job. `Tests.Integration`: the writer end to end, undo removes
  the seasonal row, the wipe crosses seasons, the scoped reader returns the season's row, backfill idempotency,
  the seal resuming on the next roll. `Tests.Components`: the console. No E2E — nothing player-facing.
- **What the owner ruled mid-build.** The seven-day grace decided on 2026-09-13 was overruled on the
  14th, while the regression check's findings were being triaged (D13). It simplified more than it
  removed: exactly one season is writable, so the import-time pass, the undo replay and the counting
  rule all narrowed from "every unsealed season" to "the one the clock stands in", and finding 7 — which
  clause of the counting rule wins when two seasons are open — stopped being a question. The one place
  that had to grow instead of shrink is the backfill: it replays windows that have closed, so it can no
  longer route through the counting rule and calls `WriteInto` with the season it was handed. The split
  is honest about the difference — a live import infers its season, a rebuild is told one.
- **Post-deploy.** Trigger now on `roll-season` (the first row), then Backfill seasons once. Between the deploy
  and that, imports find no running season and write nothing seasonal; the backfill picks those plays up.

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
| **D · The boards** | slice 3 | *Redo pending since 2026-09-22 (D49):* the PIU Scores Leaderboards section in **both views** — all-time: PUMBILITY, the two CO-OP boards, Hardmode with its two tabs, Completion, Plays, and no season chrome; seasonal: the same boards on season numbers plus a theme page, the standing card, days left, previous / next, a sealed past season, the past-seasons dialog rows, What moved; the Season widget at its sizes. The Round 1 sheet stands for the row vocabulary, the standing card and the floor state. |
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

Round 3 (pending): Sheet D redone as the section in both views (D49), with the two CO-OP boards (D41),
the Plays page (D48) and a theme page (D46). Nothing on the Round 1–2 sheets that survived is redrawn
for its own sake.

## 14. Open

Standing questions, and the loops the 2026-09-22 round added. Each loop is a working session on the
prod-synced local database or a curation pass with the owner, and each ends in a ruling that lands here
as a decision. **A loop runs inside the slice that builds its tool** (owner, 2026-09-22: "loops should
be in the slices that build their tools, yes"): the slice's PR opens with the tool, the loop runs on it
against real data, and the ruling lands before the slice ships. The slice rows in §12 say where. None
ran on 2026-09-22.

- **The CO-OP anchoring census** (D42; inside slice 8, between its two halves): the first half is a
  probe that prints, for every co-op, the pass-anchored level (the co-op pass tier list per player
  count) and the score-anchored level (the scoring-difficulty saga's method, with the players' Doubles
  competitive levels as the anchor), the level table seeded from it, and the pin console; the loop
  decides the blend, checked against the [CO-OP] title holders the flat rating already places; the
  second half is the census saga with that blend. If the two anchors do not agree well enough to blend,
  the CO-OP PUMBILITY board changes shape — tier-priced, or dropped.
- **The season-formula sanity check** (D39; inside slice 1c): a probe in the exploration workbench
  re-prices everyone's current pool under season PUMBILITY against the official number — how many
  ranks move and where, and what share of a top pool the SSS+-to-PG band is worth. The "SSS+s become
  the meta" test.
- **The Completion census** (D40; inside slice 3): the all-time Completion board, rebuilt locally
  against the prod-synced database and read before it ships — who tops the whole-game sum and by how
  much the "all the 12s" acceptance actually costs; whether the sub-10 zero or the co-op exclusion
  changes the top ten.
- **The Stamina rule** (D46, D47; inside slice 5, through the matcher's dry run): run the candidate
  rule — duration at least 2:30 or a Remix or Full Song, level 18 up, the piucenter Stamina-and-Runs
  badges as a second opinion — over the Phoenix 2 catalog, read the list, refine it or reduce it to a
  finite list.
- **Gimmick tagging** (D47; inside slice 5, once the tag console exists): the first hand-tagged list,
  from the owner's knowledge and the stepfile detection findings of 2026-08-29; whether the detection
  is worth building as a seeder.
- **The first themed season** (D46; inside slice 5, its last step): which two or three themes run,
  dry-run their lists, name them.
- The 20 / 20 / 50 numbers in D7 are first guesses; re-read them after the first balanced season with
  the census in §5 re-run. Outside any slice: it needs a sealed balanced season.
- Season rewards: what the top of a sealed board earns, and how many places. Outside any slice: a
  product decision, taken before the flag flips (slice 6).

Decided at build, in the slice that touches them (recommendations recorded, none ruled on):

- **How the last days of a season are signposted**, raised by the no-grace ruling (D13): "import before
  the quarter ends or those plays are not in this season" is a rule players can only follow if they are
  told when the quarter ends. Recommended: days left in the pill, where Sheet A already places it (2a),
  and one line on the import surfaces (2f); the alternative is a countdown in the import widget.
- Whether the Play page's "players holding your title" comparison stays visible in seasonal view (2c):
  a seasonal number, under a different formula, against an all-time cohort. Recommended hidden.
- Whether folder completion on the player page in seasonal view counts a broken play as "played" the
  way the all-time folder does (D38; 2e). Recommended unchanged.

Still open, **not ruled on**: whether slice 3 builds the all-time Hardmode board as a list-board from
day one — the list refreshed by the weekly census event, the two list tables pulled forward from
slice 5 — rather than rendering it through the six columns and folding it in as a cleanup after slice 5
that retires the six columns' writer (D45). Recommended yes: the six columns would keep feeding the
Hardmode page's own standing and nothing would need retiring. Slices 3 and 5 read as written until it is.

## 15. Docs to update in the build PRs

`CLAUDE.md` (the `Seasons` vertical in the layer table and the vertical list; the global-query-filter
convention; the cache-key ratchet), `docs/ARCHITECTURE.md` (vertical list, code map, the view beside
the mix in the shell section), `docs/DATABASE-SCHEMA.md` (`Season`, `ChartSeason`, the
`SeasonId` columns), `docs/SCHEDULED-JOBS.md` (`roll-season`, `rollup-season-stats`),
`docs/UX-GUIDELINES.md` (the season marker tokens and the caption rule), `docs/DOMAIN.md` (season,
season rating, seasonal personal best, TOTAL PUMBILITY), the nine `docs/LOCALIZATION-*.md` glossaries
(season vocabulary), and `docs/API.md` when the additive endpoints land. Since 2026-09-22 also:
`docs/ARCHITECTURE.md` and `CLAUDE.md` gain the `Leaderboards` vertical (slice 3), `docs/DATABASE-SCHEMA.md`
the boards tables and `ChartTag` / `CoOpDifficulty`, `docs/SCHEDULED-JOBS.md` the `rebuild-leaderboards`
row, `docs/DOMAIN.md` season PUMBILITY, Completion, CO-OP Difficulty Level, theme and the side boards.
[hardmode-leaderboard.md](hardmode-leaderboard.md) §4 and [communities-overhaul.md](communities-overhaul.md)
carry the note that their boards move into the section (D49), and [march-of-murlocs.md](march-of-murlocs.md)
§9.6 the third configuration (D39) — those three notes rode this docs pass.
