# PIU Scores Supplemented — the second view of the Official Leaderboards

Status: **designed, not built** (2026-08-04). Owner post-deploy: press **Roll up supplemented
leaderboards** per mix on `/Admin/OfficialLeaderboards` once, immediately after the deploy — that
press is the supplemented baseline. From the following Sunday the normal sweep cadence carries it.

The Official Leaderboards section grows a second reading of the same snapshots. Official mode is
what piugame publishes, unchanged. Supplemented mode folds in the weekly rolled-up scores of public
PIU Scores accounts, so a player who is real but not board-visible appears where they actually
stand. One switch drives the section. Everything stays weekly — no live pushes when a score lands.

This is the feature that [official-leaderboards-overhaul.md](official-leaderboards-overhaul.md) §12
round 6 pulled out ("owner has a whole feature cooking that would double the scope of this branch").
The two orphaned localization keys left behind then are consumed here.

## 1. Locked decisions (owner, 2026-08-04)

| # | Decision |
|---|---|
| 1 | **Both board kinds.** Chart boards and the PUMBILITY rating board, plus the computed co-op board. **What It Takes is not supplemented** — its cutlines are a fact about the real board. |
| 2 | **Identity is the import link**, `OfficialPlayer.UserId`. `User.GameTag` counts as import-confirmed, because it is only ever written from piugame account data (§3). A contested tag resolves most-recently-active. |
| 3 | **Verified scores only.** `Source = 'officialImport'` **or NULL** — NULL is pre-capture and proven dormant (§4). `manual` and `csv` never appear on a public board. |
| 4 | **Official places renumber** in supplemented mode. |
| 5 | **Both mixes**, Phoenix and Phoenix 2. |
| 6 | **Public users only.** Private users are excluded outright — not anonymised, not counted. |
| 7 | **One section-wide switch**, labelled *PIU Scores Supplemented*, right-aligned on the section nav row. Default off. Available to anonymous visitors. |
| 8 | **This Week supplements.** Every diff-based highlight kind recomputes. World firsts and New #1s stay official (§6). |
| 9 | **Popularity is official-only**, with a disclaimer naming the reason. |
| 10 | **One table, one bool.** `IsSupplemented` on the existing placement and highlight tables, with the filter applied thoroughly (§7 makes that machine-checked). |
| 11 | **Nothing is ever pruned.** Supplemented history is kept as long as official history — the point is being able to look back at previous weeks. |
| 12 | **Account deletion purges supplemented rows.** |
| 13 | **`api/v2/official/*` stays official-only** for now. |
| 14 | **Phoenix leaderboard processing switches off in September**, when the game goes offline. Phoenix 2 is the long-term carrier. |

## 2. What this is worth, measured

Measured against the prod-synced local database on 2026-08-04; ledger data current through
2026-08-03, latest sealed snapshots Phoenix #5 and Phoenix 2 #7 (both 2026-07-26).

| | Phoenix | Phoenix 2 |
|---|---|---|
| Players the view adds | **687** | **46** |
| Supplemented rows per week | ~185,568 | 890 |
| Official placement rows per week (today) | 125,404 | 57,520 |
| Public users with ledger records in the mix | 744 | 49 |

Phoenix is where the feature lives today and it stops accruing in September (decision 14). Phoenix 2
is 46 players because only 46 public accounts have imported any Phoenix 2 scores yet — weekly active
Phoenix 2 importers ran 3 → 12 → 38 → 36 over July, pre-US-kits. The thin state is honest and
self-correcting; the UI says the number rather than looking broken (§8).

Site totals for context: 2,473 accounts, 793 public, 414 with an `officialImport` play ever.

## 3. Identity

**The link is `OfficialPlayer.UserId`**, set by `LinkPlayer` during a successful import
(`OfficialLeaderboardSaga`), last-import-wins. Supplementation is defined over linked players, so a
tag on a public board always belongs to the account that most recently proved it.

**`User.GameTag` is import-confirmed.** `UpdateUserGameProfileCommand` has exactly one sender —
`OfficialLeaderboardSaga:182`, with `accountData.AccountName` — and the only other writer is the
PIUGAME login path (`LoginController:167`, from scraped `PiuGameAccountIdentity`). There is no UI
field; the admin page renders it read-only. This supersedes decision 8 of the leaderboards
overhaul ("no string-matching backfill against `Users.GameTag`"), which assumed the column was
user-supplied. Matching `User.GameTag` to `OfficialPlayer.Username` joins two piugame-derived
strings, not a guess.

**Why a backfill is needed at all.** `LinkPlayer` shipped 2026-07-17, so the link column's earliest
row is that date — it currently records three weeks of importers, not the import population.
Backfilling lifts public linked players from 153 → 294 on Phoenix and 49 → 104 on Phoenix 2.

**The backfill is ensure-player *and* link.** 394 Phoenix players (and 3 on Phoenix 2) have no
`OfficialPlayer` row at all — never board-visible, last imported before the link shipped. Creating
them changes what the table means: `OfficialPlayer` becomes *tags we know about*, not *tags seen on
a board*. Two consequences:

- `UserIdSource` gains a third value so backfilled provenance stays distinguishable from
  import-observed.
- **`GetPlayerNames` must gain a placement filter in the same PR.** It returns every `OfficialPlayer`
  row with no filter today — already 282 blank Phoenix 2 profiles — and 394 new dim rows would make
  that worse *in official mode*, where the feature isn't even switched on.

**Collisions.** 11 Phoenix and 6 Phoenix 2 board tags are claimed by more than one public account.
Most-recently-active resolves them, defined as `MAX(PhoenixRecord.RecordedDate)` for the user in that
mix. Identity resolves first, the public filter applies second — so if the winning claimant is
private, the tag simply produces no supplemented row rather than showing the loser's scores under it.

## 4. What counts as a verified score

`PhoenixRecord.Source` is authoritative: *verified ⇔ `officialImport`*. NULL means the row predates
capture and counts as verified, which three measurements support:

- The newest NULL-Source row has `RecordedDate` **2026-07-06**. Nothing NULL has been written in a month.
- Every record written since 2026-07-30 — 1,668 of them — carries a Source. Zero NULLs.
- Of NULL rows the journal can classify, **3,242** have a matching `officialImport` play against
  **149** with only manual/csv plays.

Distribution: 917,566 NULL · 115,176 `officialImport` · 2,992 `csv` · 928 `manual` · 20 `backfill`.

`manual` and `csv` are excluded because both may *lower* a best and both are whatever the player
typed ([score-truth-model.md](score-truth-model.md) D9). The residual is pre-July manual entries,
bounded by the 46 accounts that have ever journaled a manual play. If it ever matters, excluding
NULL rows for that set is a one-line predicate.

## 5. The merge

Per board, per snapshot: official rows, plus the ledger bests of every linked public player, deduped
to **one row per human** — a player present on both sides keeps the higher score, since scores only
improve. Ranked with the same Olympic tie rule, with a deterministic tiebreak (official first, then
tag) so a paginated board doesn't reshuffle between renders.

**On chart boards, supplemented rows can only append below the official tail.** If a player is not on
the official top 300, their score is by definition below the 300th score, so they rank ≥301 and
places 1–300 never move. `SupplementMerge` asserts this rather than assuming it. The exceptions are
narrow and all visible: a board the sweep skipped (`BoardsSkipped` is already tracked), a chart with
no mirrored board, or a play landing between the scrape and the rollup.

**Real interleaving happens only on the PUMBILITY board**, because it is two rulers: piugame's
official value for players on the board, our computed `PlayerStats.SkillRating` for players who
aren't. Official wins wherever it exists, ours fills the gaps, and the row does not say which — an
explanation there confuses more than it clarifies (owner call).

## 6. Highlights

The supplemented pass calls `HighlightsCalculator` a second time over merged placements. `HighlightsInput`
already carries everything it needs, so the calculator gains one flag: `IncludeRecordKinds`.

| Kind | Supplemented? | Why |
|---|---|---|
| Movers, BoardsClimbed, WeeklyPulse, PumbilityGainer, FloorMark, Debut | **yes** | Needs only this snapshot against the previous one. |
| NewNumberOne, ChartGradeFirst, FolderGradeFirst | **no** | Reads `OfficialBoardRecord`/`OfficialFolderRecord` and cross-mix highs. Keeping them official means **no second set of record books** — the expensive, stateful, cross-mix half never doubles. |

Gating inside the calculator beats filtering its output, because the record kinds participate in a
collapse rule ("a grade first absorbs its new-#1") that post-hoc filtering would leave holed.

Two behaviours to expect:

- **The supplemented series has its own week one.** `isSupplementedBaseline = !AnySupplemented(mix)`,
  mirroring the existing `isBaseline = !AnySealed(mix)`. Without it the first run emits 687
  simultaneous debuts and a board-wide flood of new entries. The owner's post-deploy button press
  *is* that baseline; the first Sunday after it produces the first real supplemented This Week.
- **Entry credits inflate.** Entering a board credits N−P+1 places and N is larger on a merged board,
  so official players' climb numbers read higher in supplemented mode. Correct by the formula,
  surprising on the page.

## 7. Keeping supplemented rows out of official reads

Decision 10 puts both row kinds in one table, so the filter is a discipline problem. It is made
machine-checked instead:

- The repository exposes `GetOfficialPlacements` and `GetAllPlacements`. No handler writes its own
  predicate.
- An architecture test fails the build if `Set<OfficialLeaderboardPlacementEntity>()` appears outside
  `EFOfficialSnapshotRepository`.
- An integration test asserts every hub read in official mode returns zero supplemented rows against
  a real database seeded with both.

The read paths that must stay official: highlights assembly, both record books and `GetCrossMixHighs`,
the `TierListProcessor` "Official Scores" feed, `CutlineCalculator`, rankings archetypes and computed
ratings, placement estimates, the five `api/v2/official` endpoints, the Discord weekly digest, and
`DeleteRatingPlacements`. The tier-list feed is the one where a miss is silent and expensive — it
feeds the site's most-used page.

## 8. Presentation

**The switch** lives in `OfficialSectionFrame`, right-aligned on the nav row so it reads as scoped to
the section. State is one UiSettings key, `OfficialLeaderboards__Supplemented`, which covers anonymous
visitors through `ProtectedLocalStorage` ([UiSettingsAccessor.cs:56](../../ScoreTracker/ScoreTracker/Services/UiSettingsAccessor.cs)).
No page in the section is authenticated — every `IsLoggedIn` reference there is decoration (the
you-glow, and Players auto-selecting your linked tag).

**The row marker is a dashed left rail plus a chip, never a background tint.** A row can be
supplemented *and* your row *and* a community row at once, and the background is already spoken for
by the `--daily-you` / `--daily-community` glow vocabulary. `--mix-secondary` carries it — already in
the palette, unused on these boards.

| Surface | Supplemented mode |
|---|---|
| Rankings | Merged board, renumbered, markers. Only board where rows interleave. |
| Players | Placement sheet gains the player's full ledger; profile tiles recompute. |
| This Week | Pulse, movers, climbers, gainers, floors, debuts recompute. World firsts unchanged. |
| Chart board dialog | Official 1–300, then the supplemented tail. |
| Popularity | Unchanged board, plus the disclaimer. |
| What It Takes | **Chip hidden from the nav.** Landing there by direct URL renders it normally — not worth a redirect. |

**Popularity disclaimer**, owner-approved wording: *"Popularity always comes from the official
leaderboards. It's ranked on full play data, not on a truncated board, so there's nothing for PIU
Scores to add."*

**Thin-mix count line**, while the number is still surprising: *"46 players added on Phoenix 2 · 890
scores, rolled up Jul 26."*

Mock: https://claude.ai/code/artifact/12a2702d-71f3-4035-bb6b-b219aaf34722

## 9. Technical scope by layer

**Verticals touched:** OfficialMirror (nearly all), ScoreLedger (one published read), Web. Nothing in
SharedKernel, Identity, PlayerProgress or Catalog. **No new project references** — `OfficialMirror`
already references `ScoreLedger`.

**Domain** (`OfficialMirror/Domain/`) — new `SupplementMerge` (pure: dedupe, rank, tail-append
invariant); `HighlightsCalculator` gains `IncludeRecordKinds`; `CoOpBoardCalculator` accepts merged
rows; `IOfficialSnapshotRepository` splits its placement read and gains supplemented write/delete
plus `AnySupplemented(mix)`; `PlacementRow`/`HighlightRow` carry the flag; `IAccountPurgeRepository`
deletes supplemented rows.

**Application** (`OfficialMirror/Application/`) — new `SupplementRollupSaga`, consuming
`OfficialSnapshotSealedEvent` (Sunday) and `RollUpSupplementedLeaderboardsCommand` (the admin
button) so both triggers share one path. Deliberately not a stage inside `LeaderboardSweepSaga`: that
saga is already 417 lines over 7 stages, and a rollup failure must never block the official seal.
`OfficialCacheKeys` gains the mode and the rollup evicts by hand — writing rows onto a sealed
snapshot is the one case snapshot-keyed caching cannot survive ([official-leaderboards-overhaul.md](official-leaderboards-overhaul.md)
§12 J4). `LeaderboardHubSaga`'s 12 handlers take the flag through. `OfficialDigestFeedSaga` is
explicitly official-only. Six hub queries and three contract records gain the flag.

**Infrastructure** (`OfficialMirror/Infrastructure/`) — `IsSupplemented` on the placement and
highlight entities; the flag joins the placement indexes; the clustered PK is unchanged, since
supplemented rows carry their merged place and `PlayerId` keeps it unique. Two migrations:
`SupplementedPlacements` (two bool columns, default 0 — no data movement) and
`BackfillOfficialPlayerLinks` (ensure-player + link, raw SQL, collision resolution).
`EFOfficialSnapshotRepository` becomes the sole toucher of the placement set.

⚠ **The purge gap.** Supplemented rows key on `PlayerId`, not `UserId`, so the four-way account-purge
ratchet cannot see them — no `*UserId` column means nothing forces them into a manifest. The delete
needs a hand-written integration test, and this paragraph is the reason it exists.

**ScoreLedger** — one published read on `IScoreReader`: verified non-broken current bests for a mix,
streamed rather than materialised (587k rows on Phoenix). Implementation in
`EFPhoenixRecordsRepository`.

**Presentation** — the switch and What-It-Takes hiding in `OfficialSectionFrame`; the flag threaded
through `HubRankings`, `HubPlayers`, `HubThisWeek`, `OfficialChartBoardDialog`; the disclaimer and
count line on `HubPopularity`; `.olb-row-supp` in `site.css` (rail only, so it composes with both
glows); a fourth button on `/Admin/OfficialLeaderboards` beside Run import / Rebuild highlights /
Refresh popularity, same `_xQueued` pattern. ~9 localization keys ×9 locales, two of which are
already present from round 6.

**Not touched:** `HubWhatItTakes`, `CutlineCalculator`, `TierListProcessor`, the `api/v2/official`
controller and its contract goldens, the chart details page.

## 10. Growth

Phoenix roughly triples its weekly placement growth (~186k supplemented rows on ~125k official, plus
a second highlights set) and then stops in September (decision 14). Phoenix 2 adds 890 rows a week
today and grows with adoption. Nothing is pruned, so the Phoenix supplemented history is a bounded,
permanent artifact of the mix's final months — which is most of the point.

## 11. Verification

| Suite | Coverage |
|---|---|
| `DomainTests` | `SupplementMerge` — dedupe, higher-score-wins, tie ordering, the chart-board tail-append invariant; `HighlightsCalculator` with `IncludeRecordKinds=false`; supplemented-baseline silence. |
| `ArchitectureTests` | The placement-set chokepoint ratchet. |
| `Tests.Components` | The switch; the marker composing with both glow classes; What It Takes hidden; the disclaimer; the count line. |
| `Tests.Integration` | The leak test (official mode returns zero supplemented rows against real SQL); account purge; public→private flip at read time; rollup idempotency; both migrations. |
| `Tests.Api` | Assert-only — no golden changes. |

## 12. Commit plan

**S0** this doc · **S1** migration + entities + model contribution · **S2** repository split +
chokepoint ratchet · **S3** `SupplementMerge` + the ledger read · **S4** `SupplementRollupSaga` +
command + cache keys · **S5** highlights second pass · **S6** hub contracts + handlers · **S7** the
switch, row marker, disclaimer, count line · **S8** admin button · **S9** link backfill migration ·
**S10** integration + component tests · **S11** localization ×9 · **S12** docs pass
(DATABASE-SCHEMA, ARCHITECTURE code map, this doc flipped to shipped).

## 13. Open items

- **A supplemented board with no official rows.** When the sweep skips a board, supplemented mode
  shows our players alone on it, ranked from #1. Undecided whether that renders normally, carries a
  note, or is suppressed.
- **`?supplemented=1` querystring override** — offered and not taken; the switch is preference-only,
  so a supplemented view is not shareable by link. Additive later.
- **Discord digest** — deliberately official-only in v1. A supplemented weekly section is an obvious
  follow-up and the highlight rows are already the right shape.
- **`api/v2/official`** — decision 13 is "not for now", not "never". If it ever changes, it publishes
  a piugame-tag-to-site-account map in bulk, which is what that controller's class doc currently
  refuses.
