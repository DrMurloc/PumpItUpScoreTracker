# RiseCatalog

Generates the Pump It Up RISE catalog — the **Rise** and **Rise Arcade** mixes' patches, songs, charts and
membership rows — as idempotent SQL from the community song sheet, the Arcade Station song list and the
site's own Phoenix exports. Design and decision log: [docs/design/rise.md](../../docs/design/rise.md) (§4 for
the catalog rules, §11.3 for what each script holds).

**Deliberately not product code** — an operator tool like `PumpoutExtractor`, standard-library Python only,
Sonar-excluded. The data it produces never enters the repo (owner decision): scripts land wherever you point
`out-dir` (typically Downloads) and are reviewed and run by hand, in order, after the `RiseMixes` migration has
seeded the two `scores.Mix` rows.

## Usage

```
python build.py <inputs-dir> <out-dir>
```

Inputs, all CSV/JSON (the research bundle's `2026-09-22-catalog-inputs/` is a complete set):

| File | What it is |
|---|---|
| `rise-song-list.csv` | The community sheet (The_N_1), one row per song: `Title, Composer, Category, S1..S6, HD1..HD6, …, Note, Version` |
| `arcade-station-songs.csv` | The Arcade Station list (Dave), `Title` — the 352 songs the game's arcade mode carries |
| `export-Phoenix.csv`, `export-Phoenix2.csv` | The site's `/Charts/Export.csv` for each Phoenix mix — the arcade charts the singles align onto |
| `patches.csv` | `Mix, Version, ReleaseDate` — every Rise patch the sheet names, dated from the Steam notices |
| `jackets-manifest.csv` | The jackets already on the CDN under `songs/` (`title, gameId, file, …`) |
| `durations.csv` (optional) | `Title, Seconds` for the new songs where measured; `00:00:00` otherwise |

Outputs, four single-transaction scripts (`SET XACT_ABORT ON`, a `RAISERROR` on any count that comes up short
rolls the whole script back) plus `reports/`:

| Script | Rows | Needs |
|---|---|---|
| `s1-rise-versions.sql` | `scores.MixVersion` for every Rise patch and the Arcade Station's | the RiseMixes migration |
| `s2-rise-songs-charts.sql` | `scores.Song` for the songs new to the tracker; `scores.Chart` for every Rise-only single and every half-double, `OriginalMixId = Rise` | s1 |
| `s3-rise-membership.sql` | `scores.ChartMix` for Rise: ported singles onto their arcade charts at Rise's level, the new charts, every half-double; `AddedInVersionId` = the song's arrival patch | s2 |
| `s4-rise-arcade-membership.sql` | `scores.ChartMix` for Rise Arcade: every Phoenix 2 Single and Double of the Arcade Station songs, Phoenix 2 level and note count copied | s1 |

**Read `reports/` before running anything.** `alignment.csv` is every single's reading (which arcade chart it
maps onto, or why it is a new chart) with a flag on the ambiguous ones; `fuzzy-matches.txt` is every sheet
title that only matched a tracker name approximately; `unmatched-arcade-songs.txt` should be empty;
`art-needed.txt` lists new songs whose jacket is not in the manifest; `counts.txt` is what the scripts assert.

## How resolution works

- **Identity.** Existing songs and charts are resolved *inside the script* by name, type and Phoenix level
  (`s.Name = N'…' AND c.Type = 'Single' AND cm.Level = 16` on the Phoenix — or, for a chart Phoenix 2 added,
  the Phoenix 2 — membership row), so the exports need no ids and a renamed song fails loudly rather than
  silently. New rows get v5 Guids of their natural key, so a re-run emits the same ids and every `INSERT` is
  guarded by `NOT EXISTS`.
- **Singles** align onto a song's arcade charts by order (docs/design/rise.md §4.3): charts keep their order
  across mixes, a re-rate moves one level (rarely two, never three), Phoenix 1 is Rise's origin catalog and a
  Phoenix 2 re-rate counts as a match. A Rise single that matches nothing within two levels is a new chart.
  The five songs the owner reviewed in-game read exactly as the alignment does, so no overrides exist.
- **Half-doubles** are always new charts (D7); the arcade's doubles are never their parent.
- **Rise Arcade** is a membership list: the Phoenix 2 charts of the 352 songs, as-is (§4.2).
- **Versions** come from the sheet's `Version` column (`Base` for the early-access launch) mapped onto
  `patches.csv`; a version the sheet uses that the file lacks stops the run.
- `TITLE_FIXES` in `build.py` carries the sheet spellings the game disagrees with (`Pop & Pump & DIVE!!`).

The Mix Guids in `build.py` must stay in lockstep with `ScoreTracker.Data`'s `MixIds` and the RiseMixes
migration — minted once on 2026-09-22, hardcoded in all three on purpose.
