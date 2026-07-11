# Legacy Mixes — catalog backfill & old-mix score tracking

**Status:** design locked 2026-07-11 (all decisions below are owner-confirmed that day unless noted). PR 1 (catalog corrections script) delivered to the owner's Downloads folder, pending manual run. PR 2 and PR 3 not started — owner is sequencing rollout against other in-flight features.

People play old mixes constantly — Prime cabs, Fiesta cabs, NX cabs — and have no good way to track progress on them. This feature backfills the full historical Pump It Up catalog (The 1st Dance Floor → Prime 2, plus Infinity) into the site and opens score tracking on every mix, with CSV + manual entry as the primary upload paths. **Tier lists for XX-and-older are explicitly out of scope** — that is a separate problem from the (Phoenix 1/2-focused) tier list overhaul.

---

## Data source: the Pump Out SQLite dumps

[AnyhowStep/pump-out-sqlite3-dump](https://github.com/AnyhowStep/pump-out-sqlite3-dump) publishes SQLite dumps of the Pump Out project — a fan-maintained, *versioned* PIU catalog. Latest dump: `pumpout-2022-05-26-*.db` (7.6 MB, resolves through XX v2.08.0). The 2022 cutoff is a feature: anything absent from the dump is genuinely Phoenix-era, which our data already attributes correctly.

**Trust level (owner):** "maintained by the most scrutinous person I have ever known to exist — pumpout wins" on any conflict with our data. Most of our original catalog was seeded from pumpout; an early step-artist import misattributed many charts, which this effort corrects.

Contents: 887 songs, 6,821 charts, 28 mixes, 122 versions. Step artists on 6,820/6,821 charts, song artists on 885/887, 100% BPM/category/card-art coverage, region-exclusive + unlock labels, official numeric song IDs. Per-version chart availability (`chartVersion`: INSERT/DELETE/REVIVE/CROSS/EXISTS operations) and rating history (`chartRatingVersion`), both resolvable at any version via the `_derived_versionAncestor` ancestry table (latest row along the ancestor chain wins). Old chart slot names (EASY/NORMAL/HARD/CRAZY/FREESTYLE/NIGHTMARE/ANOTHER, PRACTICE) survive as versioned labels.

Extraction tooling (C#/Microsoft.Data.Sqlite, session scratchpad 2026-07-11; trivially rebuildable) resolves, per mix, the catalog at that mix's final version, and computes debut attribution. Outputs: `pumpout-xx-catalog.json` (everything resolved at XX v2.08.0), `pumpout-mix-catalogs.json` (per-mix song/chart/level sets — the raw material for `ChartMix` rows), `pumpout-rating-history.json` (1,007 charts with cross-mix re-rates). **Commit the extractor with PR 3** so the backfill is reproducible.

### Prod diff results (2026-07-11, via `dev/export` with the owner's API token)

Prod had 4,007 XX `ChartMix` rows vs pumpout's 4,010 XX-available charts; **3,880 matched (96.8%)** on (normalized title, SongType, ChartType+Level). Zero match-key collisions on the pumpout side; 5 title+cut collisions among songs (two songs each named Step / Adios / Further / PICK ME; Baroque Virus Full Song ×2) — artist disambiguates. The 127 unmatched are romanization/alias gaps (HANN, Nekkoya, Cross Ray…) — the `XXScoreFile.NameMappings` alias-map pattern covers them.

| Payload | Count |
|---|---|
| Step-artist fills (ours empty) | 1,590 |
| Step-artist corrections (pumpout fuller/different) | 79 |
| Song-artist fills / corrections | 42 / 35 (+ ~128 case/spacing fixes) |
| BPM fills | 110 |
| OriginalMix corrections (XX → true debut) | 2,586 |

OriginalMix correction distribution: Prime 2 ×674, Prime ×625, Fiesta EX ×324, Fiesta ×236, Fiesta 2 ×197, NXA ×85, NX2 ×84, NX ×78, … The 1st Dance Floor ×3.

---

## Scoring history (why old-mix tracking is tractable)

Verified against [NamuWiki's Basic System page](https://en.namu.wiki/w/%ED%8E%8C%ED%94%84%20%EC%9E%87%20%EC%97%85/%EA%B8%B0%EB%B3%B8%20%EC%8B%9C%EC%8A%A4%ED%85%9C) (pumpout has no scoring-system data):

- **The XX scoring formula started in Zero (2006)** — one unbroken era Zero → XX: Perfect 1000 / Great 500 base, doubled at 51+ combo, ×1.5/×2 for 3- and 4+-note simultaneous steps. Phoenix's normalized 1,000,000 system is the first break since 2006.
- Scores are note-count-dependent and **never comparable across mixes** even within the era — a score only means something per *(chart, mix)*, which is exactly how records are keyed since the `PhoenixRecordsPerMix` migration.
- **Grades are the stable currency**: Fiesta 1 → XX used one accuracy-based ladder (weights P +1.2 / G +0.9 / Gd +0.6 / B −0.45 / M −0.9 / combo +0.05), F→SSS with Gold/Silver S variants; older eras used close cousins. Phoenix switched grades to score-derived. Our existing `XXLetterGrade` (F…SSS) covers the whole legacy world.

**Consequence:** one legacy record shape for every pre-Phoenix mix = the existing XX model — optional numeric score + `XXLetterGrade` + broken flag, per mix. No per-era scoring engines, no new grade enums. Zero+ mixes get score entry we can sanity-check; pre-Zero mixes use the same shape with the score unvalidated.

---

## Locked decisions

1. **MixEnum values for every mix** — append-only after `Phoenix2` (serialized values stay stable); display order comes from the `Mix` table, never the enum.
2. **Shorthand names** (owner-approved) keep `MixEntity.Name` at `MaxLength(10)`; full names ride `[Description]` (see table below).
3. **Prime JE folds into Prime** (~42 JE-debut charts attribute to Prime; no JE enum value). Only 3–4 song differences — not worth a dedicated mix.
4. **Infinity, Pro, Pro 2 are wanted** (the American PIU line). Infinity is fully reconstructible from pumpout (2,668 charts / 357 songs at final version). **Pro/Pro 2 are NOT in the dump** — pumpout only labels the 190/303 charts that overlap mainline/Infinity; the Pro-exclusive catalog (ITG crossovers etc.) needs an external source later. Ship the enum values now, backfill catalogs when a source exists.
5. **All mixes ship at once** — no phased rollout by mix. The mix picker shows Phoenix 2 / Phoenix / XX, with a "More" expander for everything else.
6. **Both attribution kinds**: *origin* (`Chart.OriginalMixId`, one mix per chart) **and** *membership* (`ChartMix` rows per mix with era-correct levels). Membership = **ever available during that mix** (not just its final patch) so mid-mix-removed charts stay recordable.
7. **Pre-Exceed charts display their slot names** (Easy/Normal/Hard/Crazy/Freestyle/Nightmare/Another) — "Crazy 7" is *not* "S7" and the UI must never imply the scales translate. Slot is also **identity**: the same song can have Hard 5 and Crazy 5 (both Single, same number), so slot participates in matching and storage, not just display.
8. **Legacy difficulty bubbles are CSS chips, not images.** No official art exists for pre-Exceed slots; the combinatorics (~80–100 PNGs incl. Another variants, `??`, HDB) are hostile; and a rendered chip *looking different* from image bubbles is the message (different scale). Exceed → Prime 2 reuse the existing XX bubble image set (same S/D notation) via a one-line routing change.
9. **Routine collapses onto Co-Op.** Routine-mode charts span NX → Prime 2 + Infinity in pumpout and are Co-Op's ancestor (mainline renamed the concept). They carry player-count labels (84×2P, 13×3P, 4×4P, 1×5P; unlabeled ⇒ 2) *and* real difficulty levels. No `ChartType.Routine`; instead `Chart` gains an explicit `PlayerCount` and Co-Op rows may carry a genuine difficulty in `ChartMix.Level` (see Domain).
10. **HalfDouble stays a real chart type** (physically different pad layout; 86 charts, all removed by XX). Parking HDB imports entirely remains an owner option if the surface should shrink.
11. **No per-mix themes for legacy mixes** — selecting one filters content but inherits the current theme. Theme pill stays Phoenix 2 / Phoenix / XX only.
12. **Data delivery**: bulk data lands as idempotent SQL scripts in the owner's Downloads folder, run manually (same mechanism as the delivered corrections script). Schema changes ride normal EF migrations.

### Mix shorthands

| Full name | `Mix.Name` | Full name | `Mix.Name` |
|---|---|---|---|
| The 1st Dance Floor | `1st` | The Premiere 3 | `Premiere 3` |
| 2nd Ultimate Remix | `2nd` | The Prex 3 | `Prex 3` |
| 3rd O.B.G | `3rd` | Exceed | `Exceed` |
| The O.B.G / Season Evolution | `OBG SE` | Exceed 2 | `Exceed 2` |
| The Collection | `Collection` | Zero | `Zero` |
| The Perfect Collection | `Perfect` | NX / New Xenesis | `NX` |
| Extra | `Extra` | NX2 / Next Xenesis | `NX2` |
| The Premiere | `Premiere` | NX Absolute | `NXA` |
| The Prex | `Prex` | Fiesta | `Fiesta` |
| The Rebirth | `Rebirth` | Fiesta EX | `Fiesta EX` |
| The Premiere 2 | `Premiere 2` | Fiesta 2 | `Fiesta 2` |
| The Prex 2 | `Prex 2` | Prime | `Prime` |
| Infinity | `Infinity` | Prime 2 | `Prime 2` |
| Pro | `Pro` | Pro 2 | `Pro 2` |

---

## Design

### UI / components

- **`DifficultyBubble`** grows a legacy branch (one component, per the one-concept rule):
  - Modern branch unchanged; **Exceed → Prime 2 route to the XX image folder** (same trick as the existing SP/DP special case).
  - Legacy branch renders a CSS chip when the chart carries a slot: `CRAZY 7`, `NIGHTMARE 9`, `ANOTHER CRAZY 7`; also Infinity `HDB12`, levelled co-ops (`CO-OP ×2 · 15`), and unrated `??`. Colors via a new **`--slot-*` semantic token group** + `ThemeScales.SlotColor(...)` accessor (classic wheel colors — Crazy red, Freestyle green, Nightmare purple), satisfying the `UiColorTokenTests` ratchet. Tooltip: "Crazy 7 — The Premiere 2 scale; not comparable to modern levels."
- **`MixSelector`** shared component: Phoenix 2 / Phoenix / XX visible, "More" expands the rest; data-driven from `Mix.SortOrder` + `Mix.IsPrimary`. Used by /Charts, recording flows, progress pages.
- **Chart details**: "Debuted in {mix}" from `OriginalMix.GetName()`.
- **Score entry**: chart-page manual entry becomes mix-aware; `UploadXXScores` generalizes to a mix-parameterized legacy upload page reusing its CSV parsing + `NameMappings` aliases. For slot-era mixes, CSV matching keys on (song, slot, level) — mode+level alone is ambiguous (decision 7).
- Localization: all new strings through `L[…]`, every locale in the same pass; mix and slot names remain untranslated proper nouns.

### Domain / SharedKernel

| Change | Detail |
|---|---|
| `MixEnum` +28 | 23 mainline + `Infinity`, `Pro`, `Pro2`; appended after `Phoenix2`; `[Description]` full names; `GetAccentColor` falls to the default steel until per-mix brand colors are wanted. |
| `ChartType` +1 | `HalfDouble` ("HDB") only — **no Routine** (decision 9). |
| New `LegacySlot` enum | `Easy, Normal, Hard, Crazy, Freestyle, Nightmare, Practice` + `Another*` variants (the combinations observed in pumpout labels). |
| `Chart` +2 | `LegacySlot? Slot` (null for all modern charts) and explicit `PlayerCount` (replaces the `Type == CoOp ? Level : 1` pun; backfilled so existing behavior is identical). `DifficultyString` unchanged. |
| `MixIds` | 28 new deterministic Guids. |
| Scores | No new model — `BestAttempt` (the XX shape: LetterGrade + IsBroken + optional Score) generalizes per-mix. |

Unrated (`??`) levels are almost entirely co-ops (already sidestepped by player-count convention) + four Infinity routines; the rare slotted-unrated chart stores a floor level with an unknown-rating display flag rather than making `Level` nullable. Pin the exact mechanism at implementation.

### Data / tables

| Table | Change |
|---|---|
| `Mix` | +`SortOrder int`, +`IsPrimary bit`; 28 new rows. |
| `Chart` | +`PlayerCount int` (backfill: co-ops ← current Level, everything else 1, imports ← pumpout player labels). `OriginalMixId` 2,586-row backfill via Downloads script. |
| `ChartMix` | +`LegacySlot nvarchar(16) NULL`; **~17,500 new rows** (24 mainline mixes + Infinity, era-correct levels; routine imports store real difficulty in `Level` with `PlayerCount` explicit). `NoteCount` stays null (pumpout doesn't track it). |
| `BestAttempt` (ScoreLedger) | +`MixId` defaulting to the XX Guid — the `PhoenixRecordsPerMix` precedent exactly; unique index (UserId, ChartId, MixId). |
| `Song`/`Chart` | Cut-content rows in PR 3: ~350 songs / 1,464 charts that never reached XX, born with pumpout artists/step-artists/BPM. |

No new tables. `DATABASE-SCHEMA.md` gets column notes in the same PRs.

---

## Delivery plan

**One PR** (owner call 2026-07-11, superseding the earlier 3-PR split), commit series C1–C10 below, each commit building green. **Bulk data never enters the repo** — the extractor tooling is committed; the data it generates ships as idempotent SQL scripts to the owner's Downloads folder, run manually after the PR deploys (migrations applied first).

- **Already done, independent of the PR:** `Downloads\pumpout-catalog-corrections.sql` (S1) — 1,984 idempotent UPDATEs (artists, step artists, BPM; pumpout-wins), one `XACT_ABORT` transaction, prod GUIDs, schema-independent — safe to run any time. Note: a few Japanese artists lose native script (pumpout romanizes; our old values had embedded newlines/mojibake).
- **Commits:** C1 extractor tooling (`tools/PumpoutExtractor/`, outside the solution) → C2 SharedKernel enums → C3 Chart record (`Slot`, `PlayerCount`) → C4 Mix schema+seed migration → C5 ChartMix/Chart/BestAttempt schema migration → C6 `MixSelector` → C7 `DifficultyBubble` legacy branch + `--slot-*` tokens → C8 "Debuted in" → C9 mix-aware recording → C10 legacy CSV upload + docs.
- **Post-deploy scripts (generated by the C1 tool, Downloads, in order):** S2 OriginalMix backfill (2,586 rows; needs C4's Mix rows live) → S3 ChartMix backfill + cut-content Song/Chart inserts (~17.5k + ~1.8k rows; needs C5's schema live).
- **Partner API surface is deliberately untouched** — `api/*` keeps accepting only Phoenix/Phoenix2 (+ grandfathered XX on `api/charts`); legacy mixes are site-internal until an API story exists. The wire-shape approval tests should not change in this PR.

## Open questions

1. Chip text: full word (`CRAZY 7`, lean) vs abbreviated (`CZ7`)?
2. Classic slot colors vs a single neutral legacy token?
3. Cut-content song art: re-host pumpout card images on piuimages, or placeholder?
4. Park HalfDouble imports entirely? (86 charts, all dead by XX.)
5. Pre-Exceed lineage in the dump is sparse in places (Rebirth/Premiere/Prex branching) — those catalogs deserve a skeptical validation pass before the PR 3 backfill is trusted.
