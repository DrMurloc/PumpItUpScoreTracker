---
name: check-new-charts
description: Check @PUMPITUPOfficial on YouTube for new Pump It Up chart videos since the last watermark and build the /Admin/BulkAddCharts JSON batch (canonical names/images from piugame.com). Use when the owner asks for a chart/video import, a new-songs batch, to "check for new charts", or to refresh the new-song oracle.
---

# Check for new charts

Automates the owner's most-hated maintenance task: finding newly released songs/charts and
producing the upload blob for `/Admin/BulkAddCharts` (schema + tool behavior:
[docs/design/new-charts-json.md](../../../docs/design/new-charts-json.md)). Three PowerShell 5.1
scripts in `scripts/`, glued by durable state. **The watermark IS the diff** — no catalog
sweep is needed to find new videos, only to refresh the oracle.

## State (durable, outside the repo)

`%USERPROFILE%\.piu-score-tracker\check-new-charts\`

| File | What |
|---|---|
| `state.json` | `watermark` (newest processed video id), `channelHandle`, `channelId`, `updatedUtc` |
| `oracle.json` | Site-truth for not-yet-imported songs: canonical EN `name`, `imageUrl`, song `type`, over-20 chart list. Same schema as the upload blob; `koreanName`/`artist`/`stepArtist`/`youtubeHash` are `TODO` until the YouTube pass fills them |
| `shipped.json` | Songs emitted in previous batches (name + chart keys + batch file). Excluded from new batches |
| `catalog.csv` (+`catalog-prev.csv`) | Last full site sweep (Name/Type/Level/Id); the diff baseline for oracle candidates |
| `videos\<id>.json` | Watch-page cache (title/description/length). Accumulates forever — this is what lets a song complete across multiple runs |

If the state dir is missing, this is a first-run bootstrap: ask the owner for the newest
already-processed video id, write `state.json` by hand (`channelId` is
`UC1zVbfSZSKz9r2AzF50l9sA`), and run `rebuild-oracle.ps1` twice conceptually — the first sweep
is only a baseline, so seed `oracle.json` from whatever new-song list the owner confirms.

## Normal run

```powershell
& scripts\walk-and-fetch.ps1      # YouTube: walk newest -> watermark, cache watch pages
& scripts\build-batch.ps1         # emit Downloads\phoenix2-batch-<date>.json + -report.txt
```

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
- **Read the report top to bottom.** `FLAG` lines are decisions the script made for you
  (title-vs-description disagreements — title wins, Andamiro typos descriptions; consensus
  picks; no-BGA duration fallbacks — slight overshoot, display-only). Confirm they're sane.
- **`DEFERRED` songs are normal** — Andamiro uploads a song's charts across days. The cache
  means they complete automatically on a later run. Never force a partial song through.
- **Chart-set drift on a shipped song** means a new chart video for an already-imported song.
  The admin tool skips already-in-P2 songs on Confirm, so this needs manual chart addition —
  tell the owner.
- **Handoff**: give the owner the batch + report paths. Preview in `/Admin/BulkAddCharts` is
  the human checkpoint (its already-in-catalog warnings are the dedup net for stale
  watermarks). With real blob creds the images mirror to the production CDN for real.
- **If the owner decides not to upload a batch**, remove its songs from `shipped.json` —
  otherwise they never re-emit.

## Hard-won gotchas (do not relearn these)

- The channel grid's id↔title pairing is **off-by-one** in the innertube payload — watch pages
  are the only truth. The continuation POST needs the page's own `INNERTUBE_CONTEXT_CLIENT_VERSION`.
- Title format drift is real: subtitled songs get the Korean annotation **mid-title**
  (`INFiNiTE ENERZY (인피니트 에너지) -Overdoze-`); the matcher's fallback strips the Hangul
  paren group and compares paren-insensitively, taking the Korean name from the group.
- `koreanName` is **import-critical, not cosmetic** (a missing ko-KR row silently drops the
  song from Korean users' imports). YouTube-title Korean names are the accepted source; the
  site-truth reconcile (session language via POST `/ajax/language_update.php`, `lang=kr`,
  **always restore `en`**) is a separate owner-planned pass.
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
