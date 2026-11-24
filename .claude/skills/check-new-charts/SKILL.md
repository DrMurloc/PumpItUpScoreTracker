---
name: check-new-charts
description: Check @PUMPITUPOfficial on YouTube for new Pump It Up chart videos since the last watermark and build the /Admin/BulkAddCharts JSON batch (canonical names/images from piugame.com). Use when the owner asks for a chart/video import, a new-songs batch, to "check for new charts", or to refresh the new-song oracle.
---

# Check for new charts

Automates the owner's most-hated maintenance task: finding newly released songs/charts and
producing the upload blob for `/Admin/BulkAddCharts` (schema + tool behavior:
[docs/design/new-charts-json.md](../../../docs/design/new-charts-json.md)). PowerShell 5.1
scripts in `scripts/` (plus two standard-library Python emitters for the SQL paths), glued by
durable state. **The watermark IS the diff** — no catalog sweep is needed to find new videos,
only to refresh the oracle.

## State (durable, outside the repo)

`%USERPROFILE%\.piu-score-tracker\check-new-charts\`

| File | What |
|---|---|
| `state.json` | `watermark` (newest processed video id), `channelHandle`, `channelId`, `updatedUtc` |
| `oracle.json` | Site-truth for not-yet-imported songs: canonical EN `name`, `imageUrl`, song `type`, over-20 chart list. Same schema as the upload blob; `koreanName`/`artist`/`stepArtist`/`youtubeHash` are `TODO` until the YouTube pass fills them |
| `shipped.json` | Songs emitted in previous batches (name + chart keys + batch file). Excluded from new batches |
| `catalog.csv` (+`catalog-prev.csv`) | Last full site sweep (Name/Type/Level/Id); the diff baseline for oracle candidates |
| `videos\<id>.json` | Watch-page cache (title/description/length). Accumulates forever — this is what lets a song complete across multiple runs |
| `channels.json` | Video id → channel token (`KPop`, `WorldMusic`, …) from the per-version playlists (`fetch-channels.ps1`). Accumulates like the video cache; `build-batch.ps1` names each song's `channel` from it |
| `rise-songs-<version>.json` | A patch's RISE-path spec for `rise-songs-sql.py`: version, release date, the RISE sheet, and each RISE song's Korean title as the site served it — the record of what that SQL was built from |

If the state dir is missing, this is a first-run bootstrap: ask the owner for the newest
already-processed video id, write `state.json` by hand (`channelId` is
`UC1zVbfSZSKz9r2AzF50l9sA`), and run `rebuild-oracle.ps1` twice conceptually — the first sweep
is only a baseline, so seed `oracle.json` from whatever new-song list the owner confirms.

## Normal run

```powershell
& scripts\walk-and-fetch.ps1      # YouTube: walk newest -> watermark, cache watch pages
& scripts\fetch-channels.ps1 -Version v1.03.0   # the patch's playlists -> channels.json (each video's channel)
& scripts\check-korean-names.ps1  # every unshipped oracle song's Korean title, off piugame's Korean session
python -I scripts\rise-songs-sql.py <spec.json> Downloads\phoenix2-<ver>-rise-songs-<date>.sql   # only if RISE songs arrived (below)
& scripts\build-batch.ps1         # emit Downloads\phoenix2-batch-<date>.json + -report.txt
& scripts\check-korean-names.ps1 -BatchJson "$env:USERPROFILE\Downloads\phoenix2-batch-<date>.json"   # koreanName vs the site; fix the blob to the site's
python -I scripts\channels-sql.py <earlier batch date> <its version> Downloads\phoenix2-<ver>-channels-<date>.sql   # once an earlier batch's playlists exist
```

`rise-songs-sql.py` records its songs in `shipped.json`, so it runs **before** `build-batch.ps1`
(otherwise the builder emits them into the JSON too). Its spec names the version, release date,
the RISE sheet and each song's Korean title as the site served it; v1.03.0's is
`rise-songs-1.03.0.json` in the state dir.

Run `rebuild-oracle.ps1` **first** when: the report shows `NO BLOB MATCH` entries (a song not
in the oracle), the oracle is known-exhausted (everything shipped), or the owner asks to check
for new site songs. It sweeps piugame.com's over-20 boards (login-gated; uses the
owner-authorized PiuTest account from the AppHost user-secrets store — **never print the
secret values**), rotates `catalog.csv`, and appends new-to-the-site songs to the oracle.

## Agent review duties (the scripts flag, you judge)

- **Oracle candidates are site-new, not necessarily tracker-new.** A returning song (cut in
  Phoenix, back in P2 — e.g. KUGUTSU) already exists in the tracker and must go the
  ChartMix-seed path, not BulkAddCharts. Verify each candidate against the tracker (site
  `/Charts` search across mixes, or ask the owner) and remove returners from `oracle.json`.
- **RISE songs reach the arcade too, and the tracker already has them.** v1.02.0 brought 17:
  15 whose chart videos say `Steam Link Exclusive Track` plus two teaser songs that were RISE
  songs first; v1.03.0 brought 5 (three Steam-link, two teaser). BulkAddCharts checks only Phoenix
  and Phoenix 2 names, so it would create a second copy of each. Their arcade charts go in by SQL
  on the existing songs, the RISE catalog way (docs/design/rise.md D7/D8): an arcade single joins
  the RISE single it was ported from, matched in order; a double, co-op or single RISE lacks is a
  new chart debuting in Phoenix 2. `rise-songs-sql.py` emits that script (it stops on a pairing it
  cannot make by the one-level rule). Keep these songs in `oracle.json`; the emitter records them
  in `shipped.json` with the SQL as their batch, so the drift check keeps watching them. **Which
  songs are RISE songs the tracker has** = the RISE catalog sheet the catalog was built from
  (`Downloads\rise-research-2026-09-14\2026-09-22-catalog-inputs\rise-song-list.csv`, RISE through
  1.5.0). A RISE song from a later RISE patch (v1.03.0's ELEVATE and untitled, RISE 1.6.0) is
  only in the tracker if somebody added that patch since — ask the owner before it goes in the blob.
- **Rehearse the SQL before handing it over.** LocalDB cannot replay the migration chain (one
  migration builds an index `WITH (ONLINE = ON)`, Enterprise-only), so build the current model's
  schema instead: `dotnet ef dbcontext script` from `ScoreTracker.Data` (startup project
  CompositionRoot), strip ` WITH (ONLINE = ON)`, add the four `scores.Mix` rows, and seed the
  songs exactly as the RISE catalog wrote them (their lines from `s1`–`s3` in
  `2026-09-22-catalog-sql\`). Then: the WhatIf run writes nothing, a commit writes what the
  closing checks expect, a second run changes nothing, and a sabotaged run (a second copy, a moved
  RISE level, a taken Korean title) stops with nothing written.
- **A site chart Andamiro never filmed** (Until Defined CO-OP x2, v1.02.0) takes a roster channel's
  video (docs/design/chart-video-sourcing.md) with that channel's canonical `channelName`. Add the
  song to the blob by hand and to `shipped.json` with every chart; the builder then lists it as
  shipped instead of deferring it forever.
- **Read the report top to bottom.** `FLAG` lines are decisions the script made for you
  (title-vs-description disagreements — title wins, Andamiro typos descriptions; consensus
  picks; no-BGA duration fallbacks — slight overshoot, display-only). Confirm they're sane.
- **`DEFERRED` songs are normal** — Andamiro uploads a song's charts across days. The cache
  means they complete automatically on a later run. Never force a partial song through.
- **Chart-set drift on a shipped song** means a new chart video for an already-imported song.
  The admin tool skips already-in-P2 songs on Confirm, so this needs manual chart addition —
  tell the owner.
- **Name the patch in the handoff.** Chart videos never say their version; the teaser title
  (`The 1st Content Update Teaser (V1.01.0)`), the BGA descriptions (`ver.1.01.0 Released Song`)
  and the per-version playlist (`[PIU PHOENIX 2] v1.01.0 - STEP CHART VIDEO (…)`) do. Tell the
  owner which version the batch belongs to and its Korean notice date, so the BulkAddCharts
  Version picker gets the right row (docs/design/chart-versions.md §6).
- **Name each song's channel.** The same per-version playlists carry it in the title's
  parenthesis — `(Original)`, `(K-POP)`, `(World Music)`, `(XROSS)` — and the blob's song
  `channel` field takes the enum name (`Original`, `KPop`, `WorldMusic`, `Xross`; Phoenix 2 has
  no J-Music). A song in no playlist gets no channel and a FLAG line; the admin tool imports it
  with none and warns (docs/design/song-channels.md §6).
- **Handoff**: give the owner the batch + report paths. Preview in `/Admin/BulkAddCharts` is
  the human checkpoint (its already-in-catalog warnings are the dedup net for stale
  watermarks). With real blob creds the images mirror to the production CDN for real.
- **If the owner decides not to upload a batch**, remove its songs from `shipped.json` —
  otherwise they never re-emit.

## Hard-won gotchas (do not relearn these)

- The channel grid's id↔title pairing is **off-by-one** in the innertube payload — watch pages
  are the only truth. The continuation POST needs the page's own `INNERTUBE_CONTEXT_CLIENT_VERSION`.
- **The playlists page has the same trap.** A playlist card's video-count badge and title come
  *before* its `contentId`, so "the first title after the id" is the next card's: the first
  `fetch-channels.ps1` filed every playlist under its neighbour's channel (v1.01.0's Original
  videos read as K-POP, its K-POP as World Music; v1.02.0's World Music playlist was skipped as
  "PV"). It now reads each `lockupViewModel` card as a unit and prints the card's own count beside
  the fetched one — a count that belongs to another playlist means the pairing slipped again.
  `tools/YouTubePlaylists/inventory-2026-09-12.tsv` (collected in a browser) is the cross-check.
- Title format drift is real: subtitled songs get the Korean annotation **mid-title**
  (`INFiNiTE ENERZY (인피니트 에너지) -Overdoze-`); the matcher's fallback strips the Hangul
  paren group and compares paren-insensitively. The site's Korean title keeps the English
  tail, spelled as the canonical name spells it (`인피니트 에너지 -Overdoze-`,
  `풀 미 업 (Feat. Monya)`, `버닝 슈퍼노바 feat. neur6sia`), and so does the builder. v1.02.0
  added **Korean-first** titles (`더 라스트 스탠드 (The Last Stand)`): English in the trailing
  paren group, Korean before it.
- `koreanName` is **import-critical, not cosmetic** (a ko-KR row that differs from what a
  Korean piugame session serves silently drops the song from those players' imports, and both
  name lookups are dictionaries, so a duplicate on either side breaks every import). Check every
  new song against the site before shipping with `check-korean-names.ps1`: it switches the
  session with POST `/ajax/language_update.php` (`lang=kr`), reads each song's
  `over_ranking_view.php?no=` title (the `no=` ids in `catalog.csv` are stable across
  languages), and **always restores `en`**. Without `-BatchJson` it reads every unshipped oracle
  song. v1.02.0: 21 of 21 matched once the tail rule was in.
- The per-version playlists can lag the release: v1.02.0 and v1.03.0 had none on release day,
  so their songs shipped without a channel. Once the playlists appear, `fetch-channels.ps1` maps
  the videos, but a shipped song never re-enters a batch: `channels-sql.py` emits its Phoenix 2
  `scores.SongMix` row (SongId, MixId, Channel) for the owner to run (v1.02.0's on 2026-11-24).
- RISE BGA videos (`[PUMP IT UP - RISE] BGA - …`) write `* Title :` with a bullet, so the builder
  lists them as `NO TITLE FIELD`. They carry no arcade chart; ignore them. A teaser song can also
  have only a RISE BGA, in which case its duration falls back to the shortest chart video.
- Andamiro's chart-video titles and its own site can disagree on a Korean title
  (`::POINT_NEMO:: (포인트_니모)` on every chart video, `::포인트_니모::` on piugame and the BGA):
  the builder takes the video consensus, `check-korean-names.ps1` reports the mismatch, and the
  site's spelling goes in the blob.
- The sweep's stepball regex `([sdc])_bg\.png` would read a half-double's `hd_bg.png` as a
  double. No half-double has reached the boards yet (the RISE songs' arcade doubles draw
  `d_bg.png`); if one does, it needs its own type, not `d`.
- Site boards: `lv=` filters are `20`..`26`, `27over`, `coop` (empty lv = capped top-80 "All"
  view); out-of-range pages serve **repeated** content (stop on no-new-rows); page-1-empty
  means bounced login, not an empty filter. P2 boards are login-gated for anonymous traffic.
- Level art that renders `??` (e.g. 1948 D29 = functionally 29) parses to an empty level —
  the sweep flags those rows; resolve by hand.
- PS 5.1: no Hangul literals in BOM-less `.ps1` (build from `[char]0xAC00` codes), no
  `Write-Output` inside value-returning functions (use `Write-Host`), wrap pipeline results in
  `@()`, never `return ,$arr` into `@(...)` callers.

## Update this skill

When Andamiro changes a format (video titles, board markup, login flow) and you fix a script,
commit the fix and note the drift here — the next agent should not rediscover it.
