# SongChannels

The backfill behind a song's channel per mix ([docs/design/song-channels.md](../../docs/design/song-channels.md) §7): the folder a song sits in on the cab — Original, K-Pop, World Music, J-Music, Xross — decided for every song from three sources and written as one `scores.SongMix` row per (song, mix). Operator-run, deliberately outside `ScoreTracker.sln` like the pumpout extractor and the playlist crawler next door; its outputs never enter the repo.

## Sources, in the order they win

1. **The game's own step files**, through piucenter: every per-chart JSON in a piucenter snapshot carries the step file's `SONGCATEGORY` (and `GENRE`, which is what a `USE_GENRE` category points at). Joined to our charts through `scores.ExternalChartAlias`. Phoenix 1 content only.
2. **Pumpout**'s `category` table — one value per song, never reassigned across its 122 versions — matched to our songs by name and cut.
3. **Andamiro's per-version playlists**, whose titles carry the channel: `[PIU PHOENIX 2] v1.01.0 - STEP CHART VIDEO (K-POP)`. The crawl is `tools/YouTubePlaylists`; this join keeps the channel instead of the patch. All Phoenix 2 debuts and most Phoenix ones; the only source that misfiles.
4. **By hand**, for the name gaps between our catalog and pumpout, resolved by artist inside `merge.py`.

## Steps

Three TSVs from a prod-synced SQL Server, UTF-8 (`sqlcmd -f 65001 -o file`, strip the BOM):

- `songs.tsv` — `Id, Name, Type, DebutMix, Charts` for every song (`DebutMix` = the name of the earliest mix among its charts' origin mixes).
- `aliases.tsv` — `ExternalKey, SongId, Name` from `ExternalChartAlias` ⋈ `Chart` ⋈ `Song` where the alias resolved.
- `chartmix.tsv` — `MixName, SongId, ChartId, Type` from `ChartMix` ⋈ `Chart` ⋈ `Mix`: every (song, mix) pair with charts.

Then:

```
python read-piucenter.py <piucenter-snapshot.zip> aliases.tsv piucenter.csv
python match-pumpout.py <pumpout.db> songs.tsv pumpout.csv
python join-playlists.py <playlist crawl out/> phoenixcharts.tsv chartvideos.tsv playlists.csv   # the two TSVs are the crawler's (its README, step 3)
python merge.py songs.tsv piucenter.csv pumpout.csv playlists.csv song-channels.csv
python emit-backfill.py song-channels.csv chartmix.tsv > song-channels-backfill.sql
```

`merge.py` prints the agreement between sources and every disagreement, and `song-channels.csv` names the winning source per song. The SQL script inserts one row per (song, mix) pair that has charts and no row yet — the song's channel repeated onto every mix it is in, `JMusic` written as `WorldMusic` on Phoenix 2, which folded the channel — under a `@WhatIf` switch, and reports what it left without a row.

## What the 2026-09-13 run found

1,072 songs, every one decided: piucenter 657, pumpout 383, playlists 17, by hand 15. The step files and pumpout agree on 640 of the 643 songs both cover; the playlists' two disagreements (Magical Vacation, Yoropiku Pikuyoro!) lost to the game data. 4,926 rows on day one.
