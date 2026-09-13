# Song channels — the folder a song sits in, per mix

Status: **built** (2026-09-13). Researched, mocked in two rounds and scoped the same day; the
mock that carries the approved controls is the "Channels" artifact (Round 2). This document is
the record of what was approved; the commits follow it and do not extend it.

The ask, in the owner's words: *"I want to see if we can get Song folder added as meta data.
This is like "K-Pop", "J-Music", "World", "Original Tunes", etc. that break down the songs in
the game."* Then: *"Let's call it "Channel". I wonder if we have a SongMix table that has the
folder name on it so we can have both J-Music and the phoenix 2 break down. Chart search,
randomizer, API, chart details, yeah, basically all the stuff we touched in the version add."*

## 1. Decisions

| # | Decision | Owner's words |
|---|---|---|
| D1 | **The word is Channel.** It is what Phoenix's own song select calls the folders and what the piugame site calls them ("This is new channel structure of PIU PHOENIX"). The wiki says license category; the game's step files say `SONGCATEGORY`. Neither word reaches a player. | *"Let's call it "Channel"."* |
| D2 | **Stored per song per mix, in a `SongMix` table.** There was no such table; a song was in a mix only because its charts had ChartMix rows there. The channel is the first per-song-per-mix fact, and it is rows rather than a rule because the set of channels a mix offers changes: Phoenix 2 folded J-Music into World Music, so the same song reads J-Music on Phoenix and World Music on Phoenix 2, and a per-mix table also has a home for a song that genuinely moves. | *"I wonder if we have a SongMix table … so we can have both J-Music and the phoenix 2 break down"*, *"And we're doing SongMix table."* |
| D3 | **The surfaces are the version set**: the /Charts facet and export, the randomizer chips and presets, the v2 API (rows, one filter, one sub-resource), the chart page, the bulk-add JSON and the import skill, the dev harness. | *"basically all the stuff we touched in the version add"* |
| D4 | **The chart details dialog too**, and with it the version facts the version branch forgot there: the Chart Stats tab's meta grid gains Debuted in, Added in and Channel after Song Type. Debuted in is new to the dialog as well, because the version facts read beside it on the page. | *"Add channel to the chart details dialog too. Also can we get mix version added on chart details dialog, forgot that in the version branch."* |
| D5 | **Display names and order are the game's**: Original · K-Pop · World Music · J-Music · Xross (pumpout's `sortOrder` 0 · 10 · 20 · 30 · 40). API tokens are the enum names — `Original`, `KPop`, `WorldMusic`, `JMusic`, `Xross` — the way `mix` takes `Phoenix2`; the display name rides `/channels`. | decided unless objected, in the mock |
| D6 | **A mix's channel set is its rows.** Phoenix 2 has no J-Music chip, row or token because no Phoenix 2 row carries it; the backfill writes its J-Music carry-overs as World Music. No fold rule lives in read code. | follows D2 |
| D7 | **No row means unknown, and unknown reads as absence** everywhere: no chip, no fact, never matched by a filter. On day one no song is unknown. | the versions precedent |
| D8 | **Every path that gives a song a mix writes the row**: the bulk-add batch from the JSON's `channel`, the dev harness from the API's `channel`, and a returning song seeded by hand gets its row by hand, as it gets its version stamp. | follows D2 |
| D9 | **The dialog shows the page's two version facts, not the mock's three.** Between the mock and the build, [chart-versions.md](chart-versions.md) D13 retired "Released" from the site and the API: the debut fact now carries its patch and date, and Added in appears on a carry-over only. The dialog mirrors that. | chart-versions.md D13, same day |

## 2. The channel

Five channels have ever existed, and every source agrees on the five — the game's own step
files (`SONGCATEGORY`), pumpout's `category` table, Andamiro's per-version playlists and the
wiki's per-mix song lists. A channel is a **song** fact: no source has a song whose charts sit in
different channels, and no song moves between channels across the record. What changes by mix is
the set the cab offers:

| Channel | Since | Until |
|---|---|---|
| Original | The 1st Dance Floor (1999) | — |
| K-Pop | The 1st Dance Floor (1999) | — |
| World Music | NX (2006); the two export Premieres carried world licenses earlier, all cut today | — |
| J-Music | Prime (1.09.0 in Korea, 1.12.0 abroad — the Japanese licenses left World Music) | Phoenix. **Phoenix 2 folded it into World Music**: *"The J-MUSIC channel has been integrated into the WORLD MUSIC channel."* |
| Xross | Prime 2 (the collaboration channel; one Infinity oddity) | — |

Every J-Music song debuted in Prime or Prime 2, so no older mix ever needs the channel, and the
Phoenix 2 fold is the only place the stored value differs between a song's mixes.

## 3. The model

```
scores.SongMix                                    -- Catalog-owned, registered like MixVersion
  SongId   uniqueidentifier NOT NULL              -- FK scores.Song
  MixId    uniqueidentifier NOT NULL              -- FK scores.Mix
  Channel  nvarchar(16)     NOT NULL              -- Original · KPop · WorldMusic · JMusic · Xross
  PRIMARY KEY (SongId, MixId)
  INDEX (MixId, Channel)
```

- **Composite key, no surrogate id**, the shape of ChartSkillMetric and ChartFolderBaseline in
  the same contribution. `Channel` is required: the row exists to say the channel, and no row
  means unknown (D7). Membership stays ChartMix's.
- **SharedKernel** gains the `Channel` enum beside `SongType`, with a display helper, and the
  `Song` record gains a trailing optional `Channel` — the song's channel on the mix the record
  was built for, so the 78 positional `new Song(` sites are untouched, the way `VersionStamp`
  landed on `Chart`.
- **Reads ride the per-mix chart dictionary.** `EFChartRepository.GetAllCharts(mix)` left-joins
  the mix's SongMix rows the way it left-joins MixVersion, so every surface reads
  `Chart.Song.Channel` from the fourteen-day cache with no second query, and the channels
  handler counts songs and charts per channel off the same dictionary — no repository read, the
  versions handler's shape. The mix's channel set is whatever its rows contain (D6).
- **One write**: `SetSongChannelCommand(mix, songId, channel)` in Catalog's contracts, an upsert
  on the internal `ISongMixRepository` that evicts the mix's chart dictionary. The bulk-add
  batch sends it after `CreateSong`; the dev harness writes the table directly, the way it
  writes MixVersion.
- **The migration creates the table with no seed.** The 4,926 day-one rows — one per song per
  mix it has charts in — are a what-if script the owner runs after the deploy (§5). Fresh
  databases start empty; the harness fills its rows from the API.
- **Drift is guarded in tests, not by convention**: the integration suite checks that every
  write path leaves a song with a ChartMix row in a mix carrying a SongMix row there, and the
  backfill script reports what it left unresolved.

## 4. API v2

| Route | What |
|---|---|
| `GET /api/v2/channels?mix=` | The channels the mix offers, in the game's order: `name` (the token `channel` takes), `displayName`, `sortOrder`, `songCount`, `chartCount`. Phoenix lists five, Phoenix 2 four. A sub-resource like `/versions`. |
| `GET /api/v2/charts?mix=&channel=KPop` | One channel. Several: a comma list, or repeat the parameter; any-of. |
| `GET /api/v2/songs?mix=&channel=WorldMusic` | The song catalog takes it too; a channel is a song fact. |
| `…/charts/skills` and `…/charts/random` | The same parameter, by contract. |

- Every chart row and every song row gains `channel`, the enum name, in *this* mix's vocabulary:
  the same chart says `JMusic` under `?mix=Phoenix` and `WorldMusic` under `?mix=Phoenix2`. Null
  when unknown, and a channel filter never matches an unknown.
- A name the mix does not offer is a `400` (`invalid-channel`) whose detail points at the
  channels endpoint — `JMusic` on Phoenix 2 included. The filter rides the cursor fingerprint;
  ETags are body hashes, so they change once when the field lands and then hold.
- v1 stays frozen. The chart and song goldens change additively, on purpose.

## 5. The site

**/Charts.** A Channel facet under Mix scope beside Song Types: chips in the game's order with
chart counts, any-of, one query chip per pick. The URL carries the exact set of enum names
(`Channel=KPop,Xross`). The export gains a `Channel` column. The facet hides while the mix has no
channel rows — the window between the deploy and the backfill. Song Types and Channel combine
with AND like every other facet pair.

**Randomizer.** A Channels row beside Song Types in the settings panel: the mix's own chips, All
and Clear. Nothing picked draws from every channel, the Song Types rule. The picks save into the
preset as the exact set of channel names, so a "K-Pop night" preset stays K-Pop after the next
patch; tournament presets carry it the same way, and `charts/random` takes the API parameter.
The row hides on a mix with no channel rows.

**Chart page.** A Channel fact beside Debuted in, in the vocabulary of the mix in view; nothing
when unknown. No history note: the channel is a present-tense fact like the level.

**Chart details dialog.** The Chart Stats tab's meta grid gains rows after Song Type, the page's
facts in the page's order: *Debuted in {mix}* with the debut patch and date when the origin
mix's row knows them, *Added in {mix}* with the patch on a carry-over only, and *Channel*. Each
row hides the way it does on the page (D4, D9).

**Admin BulkAddCharts.** The JSON names each song's `channel`; the preview shows it on the song's
card; a missing or unrecognised value is a warning, never an error — the song imports with no
channel and the row can be set later. On Confirm the batch writes the song's Phoenix 2 row after
`CreateSong`.

## 6. The ongoing flow

Andamiro's per-version playlists carry the channel in the title's parenthesis —
`[PIU PHOENIX 2] v1.01.0 - STEP CHART VIDEO (K-POP)` — the same playlists that name the patch.
The check-new-charts skill reads it there and emits `channel` per song; since Phoenix 2 has no
J-Music playlist, the four names it can produce are the four the mix has. A song in no playlist
gets no channel and a flag line. Returning songs seeded through the ChartMix path by hand get
their SongMix row by hand.

## 7. Sources and the backfill

Three sources, ranked by how close each sits to the game, decided every one of the 1,072 songs:

| Source | What it is | Songs it decided |
|---|---|---|
| piucenter step files | The game's own step-file header per chart (`SONGCATEGORY`, with `GENRE` as the fallback its `USE_GENRE` value points at), read from the piucenter snapshot and joined through `ExternalChartAlias` | 657 |
| pumpout | Its `category` table — one value per song, no reassignment in 122 versions — which the extractor already loaded and never emitted | 383 (it names all 887 songs it knows; the step files took precedence where both spoke, agreeing on 640 of 643) |
| Andamiro's playlists | The channel in the per-version playlist title, from the crawl the versions work made | 17 (all 35 Phoenix 2 debuts and 159 Phoenix ones; the only source that misfiles — Magical Vacation and Yoropiku Pikuyoro, both settled by the two game-data sources) |
| by hand | Fifteen name gaps resolved against pumpout by artist; every candidate pair shared a channel | 15 |

Totals on the song: Original 472 · World Music 293 · K-Pop 233 · Xross 47 · J-Music 27 (Prime 26,
Prime 2 1). Per mix, as the facet counts them: Phoenix 2 songs Original 441 · K-Pop 24 · World
Music 142 (its twelve J-Music carry-overs inside) · Xross 47; Phoenix Original 422 · K-Pop 37 ·
World Music 123 · J-Music 12 · Xross 45; XX Original 345 · K-Pop 49 · World Music 75 · J-Music
24 · Xross 43. K-Pop shrinks mix over mix because licenses expire.

The scripts that produced the table live in `tools/SongChannels/` beside the playlist crawler;
their outputs stay out of the repo like the extractor's. Delivery is the versions pattern: the
table lands with the migration, and one idempotent script in Downloads
(`song-channels-backfill-<date>.sql`, `@WhatIf` switch) inserts a row for every (song, mix) pair
that has charts and no row yet — the song's channel repeated onto every mix it is in, J-Music
written as World Music on Phoenix 2 — then reports what is still missing. One Clear Cache after
it, because the per-mix chart dictionaries cache for fourteen days.

## 8. Not in scope

A channel per chart, channel history within a mix, a per-mix channel other than the Phoenix 2
fold (the record contains none; the one candidate, Black Cat, which the wiki says was re-recorded
as an original in XX 1.04.0 while pumpout still files it K-Pop, is a one-row edit once verified),
a channels page, anything on v1.
