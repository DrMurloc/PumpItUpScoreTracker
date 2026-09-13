# Chart versions — which patch a chart came from, in each mix

Status: **built** (2026-09-12). Workshopped the same day in two rounds plus a source hunt; the
mock that carries the approved controls is the Round 2 artifact ("Chart Versions"). This document
is the record of what was approved; the commits follow it and do not extend it.

The ask, in the owner's words: *"We need to support which version (and that version's date)
charts came from in a mix. I think this is Version as a first class entity (Id, MixId, Name,
Release Date). Right now we just need a released version. There's some janky stuff when you get
into when a few charts appeared and disappeared in versions, we're not worrying about those."*
The trigger is a partner tool (FULLMODE) that wants to ask the API for *charts released after a
date*. The surfaces: data on `/Charts`, an optional filter on the randomizer, data plus filters on
the API — and, decided in round 2, the chart page.

## 1. Decisions

| # | Decision | Owner's words |
|---|---|---|
| D1 | **`releasedAfter` is the game's release date**, not the day PIU Scores added the row. A chart backfilled later with an old date never surfaces under a tool's last-sync date; catalog change detection stays the ETag and a full pull. | *"yeah, that sounds right"* |
| D2 | **Three version filters on the API**: in, by (up to and including), after (strictly after). His parameter names. | *"we need releasedInVersion=X (maybe array of versions), and releasedByVersion=X, and releasedAfterVersion=X"* |
| D3 | **Randomizer: multi-pick chips plus a bulk "through this version" select.** | *"multi pick chips are great, but we need a way to bulk select them so someone can do like 'everything released by this version' (which is common when someone plays on a cab that is behind a version or when a version is too new for a tournament)"* |
| D4 | **The chart page shows the version as two facts**, Added in and Released, beside Debuted in. | *"your read was right"* (round 2, Q5) |
| D5 | **The entity carries a `SortOrder` and its `ReleaseDate` is nullable.** 46 real legacy patches have a name and an order but no known date; a fabricated date is worse than none, and by/after are order questions. | *"yes"* |
| D6 | **A single-version mix gets one row named `Release`**, dated at its launch, and version controls hide on a mix with one version. | *"yes"* |
| D7 | **`chartCount` rides every row of the versions endpoint** — charts first released in that patch, in that mix. | *"sure"* |
| D8 | **Prime JE debuts get a `JE` pseudo-version under Prime**, undated, ordered after Prime's last patch. Around 42 charts, per the fold-JE-into-Prime call in [legacy-mixes.md](legacy-mixes.md). | *"yeah, pseudo version is good"* |
| D9 | **A new chart on an old song debuted in the patch that added it, not in the song's mix.** The 2.12.0 playlist and the wiki both list La Cinquantaine S22 and D24 as new charts on the existing song; the catalog had them as XX debuts. Fixed in the backfill script. The question reached the owner naming Conflict D18/D26 by mistake — those already read Phoenix — and his ruling applies to the pair the data actually flagged. | *"those charts came from phoenix. Conflict has a wonky history, we probably just got those two charts mislabeled. The other conflict charts were from xx. Fix it."* |
| D10 | **The bulk-upload flow carries the version** from now on: a picker on the admin tool, the patch named in the import skill's report. | *"in future, we should include version when we're bulk uploading charts from a new version"* |
| D11 | **Per-chart versions for Phoenix and Phoenix 2 come from Andamiro's own per-version playlists**, with NamuWiki's version pages filling what the playlists miss. His pointer replaced the "assume launch" shortcut. | *"https://www.youtube.com/@PUMPITUPOfficial/playlists there ya go"*, *"see what namu's got"* |
| D12 | Removals, revivals and per-version levels stay out. One version per (chart, mix): the one it first appeared in. | the ask |

## 2. The model

Three attribution kinds now describe where a chart comes from. Origin (`Chart.OriginalMixId`,
the debut mix) and membership (a `ChartMix` row per mix) came with the legacy-mix work; this adds
**release**: the patch of *that mix* the chart first appeared in.

| Field | What it is | Why |
|---|---|---|
| `MixVersion.Id` | Guid | |
| `MixVersion.MixId` | the mix this patch belongs to | unique on mix plus name |
| `MixVersion.Name` | `1.01.0`, stored bare; the UI prefixes the v | what the game prints on its update notice |
| `MixVersion.ReleaseDate` | date, nullable (D5) | the Korean notice date; one date per version even though regions ship days apart |
| `MixVersion.SortOrder` | int, never null (D5) | the ordering truth: by-version and after-version compare on it |
| `ChartMix.AddedInVersionId` | nullable link to the version of that mix the chart first appeared in | per mix, not per chart — a Prime 2 song came from its debut patch in Prime 2 and from 1.00.0 in every mix it carried into, which is what makes "released after" mean "new to this mix" |

- **Null means unknown, and every surface reads it as absence**: a version filter never matches
  a chart with no version, the facet hides it, the chart page shows no fact.
- **The `Chart` record (SharedKernel) carries an optional `Release`** — name, date, order — built
  once in the per-mix catalog cache. The SRP, the randomizer, the API DTO and the chart page all
  read it with no second query, and the Randomizer vertical, which references Domain but not
  Catalog, reaches it through the `IChartRepository` port like every other chart fact.
- **The table belongs to Catalog.** `MixVersionEntity` is Catalog-internal and registered by its
  model contribution, which also declares the foreign key from Data's `ChartMixEntity` — the
  same shape as ChartVideo's key onto Chart. The column itself is a plain nullable Guid on the
  shared entity.
- **Hotfix builds are not seeded.** Pumpout shows XX 1.03.1 and 2.03.1 added nothing, and the
  Phoenix x.y.1 builds are fixes. A hotfix that ever adds charts gets a row then. The launch
  build Andamiro's playlists call `v1.00.1` is the day-one patch and maps to the seed's `1.00.0`.
- **Ordering.** `SortOrder` steps by ten in release order; a new version created from the admin
  tool takes the next step. Names are never parsed for order — `Pre-v1.10` and `Release` are
  real names.

## 3. API v2

| Route | What |
|---|---|
| `GET /api/v2/versions?mix=` | The mix's patches oldest first: `name`, `releaseDate` (null on an undated legacy patch), `sortOrder`, `chartCount` (D7). A sub-resource rather than an array on `/mixes`, the 2026-08-02 precedent. |
| `GET /api/v2/charts?mix=&releasedInVersion=1.01.0` | What a patch added. Several: a comma list, or repeat the parameter. |
| `…&releasedByVersion=2.09.0` | Everything a cab on 2.09.0 has. Up to and including. |
| `…&releasedAfterVersion=2.09.0` | What that cab is missing. Strictly after. |
| `…&releasedAfter=2026-08-01` | The date form, exclusive like `recordedAfter`. Skips undated versions. |

- Every chart row gains `version` and `releaseDate`, both null when unknown. A Phoenix carry-over
  reads `1.00.0` in Phoenix 2: the version of *this* mix it arrived in.
- The same four parameters ride `charts/skills` and `charts/random`, which take the chart filters
  by contract.
- The three version filters combine with AND, so by 2.09.0 plus after 2.05.0 is a range. A
  version filter never matches a chart whose version is unknown.
- An unknown version name is a `400` (`invalid-version`) whose detail points at the versions
  endpoint. Every filter rides the cursor fingerprint. ETags are body hashes, so they change once
  when the fields land and then hold.
- v1 `api/charts` stays frozen. The three v2 chart goldens change additively, on purpose.

## 4. The site

**/Charts.** A Version facet under Mix scope beside Debut mix: chips per patch of the mix in view
with counts, grouped by major version when the mix has more than one, with a *Through a version*
select that turns on every chip up to the chosen one, plus All and Clear. Picking through a
version reads as one query chip; individual picks read one chip each. The URL carries the exact
set (`Version=1.00.0,1.01.0,…`), so a shared link still means the same versions after a new patch
ships. The export gains `Version` and `ReleaseDate` columns. *Newest content* sorts by release
order within the mix in view, then debut era, and the per-card sort line reads `v2.09.0 · May 27,
2025`; the card head is untouched. The facet hides on a mix with one version (D6).

**Randomizer.** A Released row beside Song Types in the settings panel: the same chips, the same
through select, All and Clear. Nothing picked draws from every version. The picks save into the
preset as the exact set of versions, so "through 2.09.0" keeps excluding 2.10.0 and later after
they ship — right for a cab that has not updated and for a tournament that froze its pool.
Tournament presets carry it the same way; `charts/random` takes the four API parameters. The row
hides on a mix with no version data.

**Chart page.** Two facts beside Debuted in: `v1.01.0` labelled *Added in Phoenix 2*, and the
date labelled *Released*. The per-mix nuance is the point: a carry-over shows it arrived at the
launch, a debut shows the patch. Neither fact shows when the version is unknown.

**Admin BulkAddCharts.** A Version picker, newest by default, with a new-version entry of name and
date. The picked version stamps every chart the batch creates. The JSON blob is unchanged.

## 5. Sources and the backfill

- **Pumpout** (`pumpout-2022-05-26-20-56-1653612171279.db`, the newest published) carries **no
  dates in any table**. It gives 122 version names with their order across 28 mixes, and its
  per-version chart operations resolve, per chart and mix, the first version the chart was
  present in. The extractor (`tools/PumpoutExtractor`) gains an S6 pass that emits those
  assignments; Prime JE debuts land on the `JE` row (D8), single-version mixes on `Release` (D6).
- **NamuWiki's per-version pages** date Phoenix (22 content patches, 2023-07-04 → 2.12.0 on
  2025-12-23), XX (15 to 2.08.0, then hotfixes through 2023-12-07), Prime 2 (17) and Phoenix 2
  completely, and list the songs each patch added. Prime, the three Fiestas, Infinity and NX have
  no version page: their patches keep pumpout's names and order, and only the launch is dated.
  Wikipedia dates every mix launch. The piugame notice boards now sit behind the am-pass sign-in.
- **Andamiro's channel keeps one playlist per version per category** (`[PIU PHOENIX] v.2.12.0 -
  STEP CHART VIDEO (Original|World Music|K-POP|XROSS)`, `[PIU PHOENIX 2] v1.00.1 / v1.01.0 …`):
  63 step-chart playlists covering every Phoenix content patch from launch through Phoenix 2
  1.01.0. Joined to the tracker through the stored video id from 2.00.0 on (our ids are the
  playlist videos) and by title before that (our early-Phoenix videos are different uploads).
  The playlists miss about five percent of Phoenix content — charts added to existing songs and
  some low charts and cuts — which NamuWiki's "Add existing songs" lines settle. Together they
  are complete: **Phoenix 1,340 of 1,340 natives assigned, Phoenix 2 308 of 308** (249 at launch,
  59 at 1.01.0). Seven non-native charts sit in version playlists — Prime and Zero revivals
  reading as "returned in that version", and the two La Cinquantaine charts of D9.
- **Delivery.** The version rows are seeded by the migration, so fresh databases carry them;
  new patches are added from the admin tool, never by migration. The per-chart assignment is one
  idempotent script the owner runs (`Downloads\chart-versions-backfill-<date>.sql`, with a
  what-if switch): the Phoenix and Phoenix 2 assignments by chart id, the D9 origin fix, every
  remaining Phoenix and Phoenix 2 row to 1.00.0, then the extractor's legacy assignments. Pro and
  Pro 2 have no catalog and stay untouched. The dev harness gets both through the API.
- The playlist crawler and join live in `tools/YouTubePlaylists/` beside the extractor; their
  outputs stay out of the repo like the extractor's.

## 6. The ongoing flow

Chart videos do not name their version; the teaser title (`The 1st Content Update Teaser
(V1.01.0)`) and the BGA descriptions do, and the new version's playlist is the cleanest signal of
all. The check-new-charts skill names the patch in its report; the admin picker defaults to the
newest version and takes a new one by name and date; `CreateChart` stamps it. Returning songs
seeded through the ChartMix path need the same stamp by hand.

## 7. Not in scope

Per-version levels, removals and revivals, a catalog-changed timestamp, anything on v1, a
cross-mix scope, a per-player "my cab is on this version" setting (a separate feature, if ever).

## 8. Appendix — the seed

One row per version, as the migration seeds them. Dates are Korean notice dates; *charts first
seen* is pumpout's count of charts whose first appearance in the mix is that patch (so a mix's
first row is its launch catalog), and the Phoenix-era counts are the playlist-plus-wiki
assignment.

### Phoenix 2

| Version | Released | Charts first seen |
|---|---|---|
| 1.00.0 | 2026-07-09 | 249 |
| 1.01.0 | 2026-09-03 | 59 |

### Phoenix

| Version | Released | Charts first seen |
|---|---|---|
| 1.00.0 | 2023-07-04 | 180 |
| 1.01.0 | 2023-07-27 | 53 |
| 1.02.0 | 2023-09-05 | 57 |
| 1.03.0 | 2023-10-31 | 45 |
| 1.04.0 | 2023-11-21 | 43 |
| 1.05.0 | 2023-12-21 | 47 |
| 1.06.0 | 2024-01-30 | 42 |
| 1.07.0 | 2024-03-07 | 47 |
| 1.08.0 | 2024-04-18 | 38 |
| 2.00.0 | 2024-05-27 | 107 |
| 2.01.0 | 2024-07-11 | 90 |
| 2.02.0 | 2024-08-22 | 43 |
| 2.03.0 | 2024-09-26 | 45 |
| 2.04.0 | 2024-10-31 | 54 |
| 2.05.0 | 2024-11-28 | 35 |
| 2.06.0 | 2024-12-26 | 70 |
| 2.07.0 | 2025-02-13 | 71 |
| 2.08.0 | 2025-04-03 | 76 |
| 2.09.0 | 2025-05-27 | 63 |
| 2.10.0 | 2025-07-24 | 69 |
| 2.11.0 | 2025-09-30 | 31 |
| 2.12.0 | 2025-12-23 | 41 |

### XX

| Version | Released | Charts first seen |
|---|---|---|
| 1.00.0 | 2019-01-07 | 2867 |
| 1.01.0 | 2019-02-28 | 87 |
| 1.02.0 | 2019-04-25 | 51 |
| 1.03.0 | 2019-06-27 | 75 |
| 1.04.0 | 2019-08-29 | 119 |
| 1.05.0 | 2019-10-31 | 67 |
| 2.00.0 | 2019-12-26 | 123 |
| 2.01.0 | 2020-02-27 | 73 |
| 2.02.0 | 2020-04-23 | 95 |
| 2.03.0 | 2020-06-25 | 67 |
| 2.04.0 | 2020-08-27 | 89 |
| 2.05.0 | 2021-01-07 | 68 |
| 2.06.0 | 2021-04-08 | 73 |
| 2.07.0 | 2021-07-08 | 80 |
| 2.08.0 | 2022-04-21 | 76 |

### Prime 2

| Version | Released | Charts first seen |
|---|---|---|
| 1.00.0 | 2016-11-13 | 2609 |
| 1.01.0 | 2017-01-20 | 15 |
| 1.02.0 | 2017-03-27 | 28 |
| 1.03.0 | 2017-04-17 | 27 |
| 1.04.0 | 2017-05-29 | 39 |
| 1.05.0 | 2017-06-19 | 30 |
| 1.06.0 | 2017-07-24 | 50 |
| 1.07.0 | 2017-07-31 | 28 |
| 1.08.0 | 2017-09-18 | 46 |
| 1.09.0 | 2017-09-25 | 18 |
| 1.10.0 | 2017-11-27 | 35 |
| 2.00.0 | 2018-01-01 | 83 |
| 2.01.0 | 2018-02-26 | 49 |
| 2.02.0 | 2018-03-02 | 23 |
| 2.03.0 | 2018-04-30 | 42 |
| 2.04.0 | 2018-06-25 | 48 |
| 2.05.0 | 2018-08-27 | 45 |

### Older mixes

Names and order from pumpout; only the launch is dated. The count in parentheses is pumpout's charts first seen in that patch.

| Mix | Launch | Versions |
|---|---|---|
| Prime | 2014-12-13 | 1.00.0 (1862), 1.01.0 (24), 1.02.0 (19), 1.03.0 (14), 1.04.0 (15), 1.05.0 (13), 1.06.0 (25), 1.07.0 (25), 1.08.0 (16), 1.09.0 (49), 1.10.0 (31), 1.11.0 (52), 1.12.0 (64), 1.13.0 (26), 1.14.0 (43), 1.15.0 (82), 1.16.0 (34), 1.17.0 (24), 1.18.0 (17), 1.19.0 (37), 1.20.0 (14), 1.21.0 (15), JE |
| Infinity | 2013-01-30 | Pre-v1.10 (2192), 1.10 (476) |
| Fiesta 2 | 2012-11-24 | 1.00 (2008), 1.01 (8), 1.10 (12), 1.20 (46), 1.30 (11), 1.40 (22), 1.50 (35), 1.51 (2), 1.60 (38), 1.61 (0) |
| Fiesta EX | 2011-01-22 | 1.00 (1448), 1.10 (17), 1.20 (36), 1.30 (70), 1.40 (17), 1.50 (202), 1.51 (0) |
| Fiesta | 2010-03-06 | 1.01 (1059), 1.02 (0), 1.03 (0), 1.04 (0), 1.05 (9), 1.06 (13), 1.07 (0), 1.10 (120), 1.20 (0) |
| NX Absolute | 2008-11-25 | Release (697) |
| NX2 / Next Xenesis | 2007-12-14 | Release (560) |
| NX / New Xenesis | 2006-12-15 | 1.05 (469), 1.08 (1) |
| Zero | 2006-01-28 | Release (346) |
| Exceed 2 | 2004-11-30 | Release (303) |
| Exceed | 2004-04-02 | Release (228) |
| The Prex 3 | 2003-10-04 | Release (189) |
| The Premiere 3 | 2003-05-11 | Release (120) |
| The Prex 2 | 2002-11-23 | Release (167) |
| The Premiere 2 | 2002-03-09 | Release (198) |
| The Rebirth | 2002-01-10 | Release (86) |
| The Prex | 2001-11-01 | Release (110) |
| The Premiere | 2001-06-01 | Release (98) |
| Extra | 2001-01-20 | Release (71) |
| The Perfect Collection | 2000-12-07 | Release (105) |
| The Collection | 2000-11-14 | Release (82) |
| The O.B.G / Season Evolution | 2000-09-03 | Release (62) |
| 3rd O.B.G | 2000-05-07 | Release (37) |
| 2nd Ultimate Remix | 1999-12-27 | Release (18) |
| The 1st Dance Floor | 1999-09-20 | Release (11) |

