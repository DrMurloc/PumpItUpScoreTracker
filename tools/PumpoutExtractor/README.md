# PumpoutExtractor

Generates the legacy-mix backfill SQL scripts from a [Pump Out](https://pumpout2020.anyhowstep.com/) SQLite dump ([AnyhowStep/pump-out-sqlite3-dump](https://github.com/AnyhowStep/pump-out-sqlite3-dump)). Design and decision log: [docs/design/legacy-mixes.md](../../docs/design/legacy-mixes.md).

**Deliberately not part of `ScoreTracker.sln`** — it's an operator tool, not product code. The data it produces never enters the repo either (owner decision): scripts land wherever you point `outDir` (typically Downloads) and are reviewed + run by hand.

## Usage

```
# 1. Grab the latest dump
curl -L -o pumpout.db https://raw.githubusercontent.com/AnyhowStep/pump-out-sqlite3-dump/master/dump/<latest>.db

# 2. Export the prod catalog (ApiToken auth) into a folder:
#    dev/export/songs, dev/export/charts, dev/export/chartmixes -> songs.json, charts.json, chartmixes.json

# 3. Run
dotnet run -- pumpout.db <prodExportDir> <outDir>
```

Outputs three idempotent, single-transaction scripts plus `reports/`:

| Script | Needs live schema | What it does |
|---|---|---|
| `s1-pumpout-catalog-corrections.sql` | none | Song artists + BPM + step artists, pumpout-wins |
| `s2-originalmix-backfill.sql` | Mix-seeding migration | `Chart.OriginalMixId` → true debut mix |
| `s3-membership-backfill.sql` | LegacySlot/PlayerCount/BestAttempt.MixId migration | `ChartMix` rows for every legacy mix + cut-content Song/Chart inserts |

**Always read `reports/suspects.txt` and `reports/s3-report.txt` before running S3** — suspects are probable prod misattributions (the 2020-era import misfiled some charts); they are quarantined out of S3 and need manual fixes. `reports/art-needed.txt` lists card art to upload for new songs (Song rows are inserted pointing at the piuimages URL convention).

## How resolution works

The dump is *versioned*: `chartVersion` (INSERT/DELETE/REVIVE/CROSS/EXISTS), `chartRatingVersion`, and `chartLabelVersion` record changes per game version, and `_derived_versionAncestor` gives each version its ancestry — state at a version = newest row along the chain. From that:

- **Membership** ("was in mix M") = present at *any* version of M (mid-mix removals stay recordable); level/slot taken at the last version where present.
- **Debut** = earliest non-DELETE appearance, mainline-preferred (Infinity never wins attribution; Prime JE folds into Prime).
- **Slots** (pre-Exceed Easy/Normal/Hard/Crazy/Freestyle/Nightmare + Another) come from versioned labels; player counts from `TWO/THREE/FOUR/FIVE PLAYERS` labels (Routine/Co-Op default 2). Routine mode collapses onto Co-Op with a real difficulty (owner decision).

Matching against prod is four passes (XX-anchored + aliases + overlap scoring; any-era residual pairing; name-only for Phoenix revivals; suspect quarantine) — see `Matcher.cs`. `aliases.json` carries the curated romanization gaps ("Bad ∞ End ∞ Night" ↔ "Bad 8 End 8 Night" and friends); a trailing `*` makes an alias a normalized prefix match.

The Mix Guids in `MixMap.cs` must stay in lockstep with `ScoreTracker.Data`'s `MixIds` and the Mix-seeding migration — minted once on 2026-07-11, hardcoded in both places on purpose.
