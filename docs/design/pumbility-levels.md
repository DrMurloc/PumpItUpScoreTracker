# PUMBILITY Levels — the gem ladder's hidden rungs

Phoenix 2 subdivides each `[P.B]` PUMBILITY gem title into five unnamed **levels** — DIAMOND LV.1
through LV.5 and so on. The game states a level nowhere in text. Its only carrier on piugame is the
badge image drawn beside a player's number on the PUMBILITY ranking and on the personal
`my_page/pumbility.php`: `/l_img/pumbility/pumbility_NN.png`, where **the file index is the rung**
and the art draws the level as a numeral. This doc records the decoded ladder, the decisions on how
piuscores surfaces it, and the slice plan that builds it.

Decoded 2026-08-13 from a 1,000-row crawl of the live board plus a walk of the badge art itself.
Reproduce with `ScoreTracker.ExplorationTests/LiveSite/Phoenix2PumbilityLevelBadgeReconTests` (three
config-gated probes; the third asserts the derived ladder row-by-row against the live board, so a
re-cut of the cutoffs fails a probe run instead of passing quietly).

## 1. The ladder

`badge = firstIndexOf(gem) + min(4, floor((total PUMBILITY − gemThreshold) / step))`

| Badge index | Tier | Starts at | Step | Evidence |
|---|---|---|---|---|
| 0 | UNRANKED | — | — | art observed — a blank grey sphere, no numeral |
| 1–5 | [P.B] BRONZE | 10,000 | 500 | art observed · thresholds derived |
| 6–10 | [P.B] SILVER | 12,500 | 500 | art observed · thresholds derived |
| 11–15 | [P.B] GOLD | 15,000 | 200 | art observed · thresholds derived |
| 16–20 | [P.B] PLATINUM | 16,000 | 200 | art observed · thresholds derived |
| 21–25 | [P.B] DIAMOND | 17,000 | 200 | wearers observed |
| 26–30 | [P.B] RED BERYL | 18,000 | 200 | wearers observed |
| 31–35 | [P.B] ALEXANDRITE | 19,000 | 200 | wearers observed |
| 36 | ABYSS ABSOLUTE | 20,000 | — | wearers observed — one rung, no numeral, draws a phoenix |

Five levels split each gem's span evenly: 200 across the 1,000-wide gems, 500 across the 2,500-wide
BRONZE and SILVER. The gem thresholds are the ones already on `Phoenix2TitleList`'s `[P.B]` titles —
the ladder introduces **no new authored numbers**, only the even split.

**Evidence, honestly split.** The *structure* is observed end to end: every rung's art exists and
numbers itself 1–5, the art family changes exactly at indices ≡ 1 (mod 5), and file weight clusters
per gem (bronze ~70 KB, silver ~29 KB) as an independent confirmation of the boundaries. The
*thresholds* are observed only from DIAMOND up: all 1,000 badged board rows agreed with the table
(zero mismatches), and 14 consecutive cutoffs each fell in an interval containing exactly one round
number — most intervals 1–10 points wide. Below the board's 17,067.44 floor no wearer is visible, so
those thresholds are arithmetic, not observation. The mirroring slice (§6) logs the only data that
can ever confirm them.

**Traps, learned the hard way:**

- ⚠ **The source URL is zero-padded below 10 and bare from 10 up** — `pumbility_01.png` answers 200
  while `pumbility_1.png` 404s byte-identically to a nonsense name. Asking only the bare spelling
  made the bottom third of the ladder look unpublished for a whole recon pass. Our mirrored copies
  pad uniformly (`pumbility_00.png`–`pumbility_36.png`) so a lookup is one `ToString("00")`.
- ⚠ Index 38 answers 200 with a blank white spacer — not a rung. The ladder is bounded at 36.
- The badge prices the **total pool only**. The Single/Double board tabs draw no badge at all
  (checked against an account reading 17,602.69 total / 17,507.33 single); the `[S]`/`[D]` title
  ladders have no levels.
- Our level and the site's badge can disagree between imports, and after a manual entry they
  disagree on purpose. Anywhere piuscores shows a level it answers "where our data puts you" — the
  same contract /Pumbility already has.

## 2. Decisions (owner, 2026-08-13)

- **Badge art = piugame's own PNGs, mirrored into our storage** and served from
  `piuimages.arroweclip.se` ("we are downloading their pngs and putting them into our storage then
  using them"). No invented chip art, no new color token group — the image carries the identity.
- **Every level-up goes to the feed** ("if you level up it goes in the feed") — no tier floor.
- **The Discord session card is the primary highlight surface** — "community highlights" in the
  original ask meant the Discord notification. On-site feeds carry the same row.
- **The Titles drawer groups holders by level in flat lists — no expansion panels** for now;
  revisit only if the lists grow too long.
- **Holder rows print the exact pool** ("if it's cheap/free, yes" — it is free; the level read
  already fetches the stats record).
- **One highlight in the holder list**: your own entry row. The rung rows stay plain — "Where you
  stand", the rung, and your entry all marked at once "is a lot of highlighting."
- **Peers by pumbility level: not yet** — explicitly not built in this pass, though the core type
  is shaped so that pass is one query and a call site later.
- Missing badge files degrade gracefully (owner): the component hides on 404; text always carries
  the meaning.

## 3. The core type

`Domain/Models/Titles/Phoenix2/Phoenix2PumbilityLevel.cs` — a `readonly record struct` holding the
whole ladder: `From(double totalPumbility)`, `FromIndex(int)`, `LevelsOf(gem)`, with
`Index` / `Gem` / `Level` / `Threshold` / `NextThreshold` / `ToNext(pool)`. Rungs are **derived**
from the eight gem thresholds rather than listed, so a gem cannot gain a rung without gaining a
threshold. It never rounds its input — a pool is fifty fractional contributions, and a pool rounded
before the compare crosses a rung it hasn't reached (`PumbilityPrecisionTests` territory; the
drawer prints pools N0 because decimals are a PUMBILITY-section feature).

The drift ratchet (`Tests/DomainTests/Phoenix2PumbilityLevelTests`) zips the ladder against
`Phoenix2TitleList`'s `[P.B]` titles: every gem must start where its title says, indices run 0–36
unbroken, every threshold round-trips through `From`, and threshold−ε lands one rung lower.

## 4. The Titles drawer (Slice 2)

`GetTitleHoldersQuery` already splits a gem rung's holders into "standing here" vs "climbed past".
On the eight `[P.B]` rungs, `TitleCommunityHandler` additionally batch-reads the standing set's
stats (one query, drawer-open only) and each `TitleHolder` carries `TotalPumbility`. The drawer
renders five flat groups, highest first — badge, `LV.n`, threshold, count — with every holder named
and their pool printed N0, sorted descending within a group. Empty rungs stay visible and dimmed:
the shape of the distribution is itself the answer to "how far up am I". A holder whose pool has
outrun the standing set (stats say RED BERYL, titles not yet recomputed) clamps into the gem's five
— the standing set is the authority for who appears at all.

**"Where you stand"** renders above the list: your badge, rung, and exact distance to the next —
computed from `TitleRung.Progress.CompletionCount`, which for a pumbility title *is* the viewer's
pool, so it costs no query. Hidden for logged-out viewers and non-gem rungs. Every other rung's
drawer renders exactly as before.

## 5. The level-up highlight (Slice 3)

One crossing rule, written once in PlayerProgress contracts
(`PumbilityLevelChange.TryFrom(milestones)`): derive old → new rung from the existing
`MilestoneKind.PumbilityGain` milestone's OldValue → NewValue, and return nothing when the same
batch also completed a `[P.B]` gem title — crossing into RED BERYL LV.1 *is* that title, and saying
both is saying it twice. This is the owner's "didn't change titles but changed levels" stated from
the other side, and the same shape as the folder-lamp suppression already in
`PlayerHighlightPolicy`.

Three render surfaces consume the one rule:

1. **The Discord session card** (`CommunitySaga.Consume(ScoreHighlightsCapturedEvent)`) — one line
   in the reserved stats block, inside the existing char budget. Discord changes verify in the
   owner's lab channel and the card canary runs before shipping.
2. **The session page** — `SessionHero` holds the batch's milestones, so it derives the level form
   and `MilestoneStrip` renders it.
3. **The feeds** — a new `WinKind.PumbilityLevelUp` classified by `PlayerHighlightPolicy`
   (`SignificantWin.Rank` carries the badge index, a new `PoolValue` the pool), rendered by
   `CommunityHighlightsWidget` and `RivalHighlightsFeed`. `PlayerHighlightSchema` bumps to v3 so
   pre-level summaries read as stale and regenerate on their next import (v2 did the same for
   folder standings). Feed rows shortened 2026-08-14 (the feeds' short-form pass): the row is the
   badge + rung name; "Reached …" and the pool value ride the tooltip.

## 6. Storage and mirroring (Slice 4)

The 37 badges live at `$web/pumbility/p2/pumbility_00.png`–`_36.png` on the piuscores storage
account — one flat lowercase folder per family, like `songs/` and `avatars/`; the `/p2` segment
copies `avatars/p2`, which exists because piugame reuses filenames across mixes for different
images.

Every P2 import already fetches `my_page/pumbility.php`. The parser additionally captures the badge
index (`PiuGameGetPumbilityResult.BadgeIndex`, tolerant — absent is null, never a throw), and
`OfficialSiteClient` mirrors an unknown index into `pumbility/p2/` via the existing
`IFileUploadClient` — `DoesFileExist` / `CopyFromSource`, the exact avatar-mirroring pattern. A
badge Andamiro adds next patch lands in storage the first time any importer wears it; the
component's 404 fallback covers the gap. The same parse logs the importer's (pool, badge) pair —
the only observations that can ever confirm the sub-17,000 thresholds.

## 7. Slices and commit order

Docs first, internationalization last (owner):

1. docs — this file + the DOMAIN.md glossary line
2. exploration probes (the recon instruments)
3. Slice 1a: `Phoenix2PumbilityLevel` + drift-ratchet tests
4. Slice 1b: `PumbilityLevelBadge.razor` (img, padded name, 404 self-hide)
5. Slice 2a: `TitleHolder.TotalPumbility` + handler batch read
6. Slice 2b: the drawer's level groups + "Where you stand"
7. Slice 4: the badge mirror on import + fixture + approval pin
8. Slice 3a: `PumbilityLevelChange` + `WinKind.PumbilityLevelUp` + schema v3 + policy arm
9. Slice 3b: session strip + feed widgets
10. Slice 3c: the Discord card line (then the canary)
11. i18n: every new key ×9 locales in one pass

No migrations, no new ports, no DI wiring, no public API change — `Tests.Api`, `Tests.Integration`
and E2E are untouched by design.

## 8. Level markers on the one-gem bars

Four surfaces draw a bar fed by a PUMBILITY pool: the title rails on `/Pumbility/Pool`
(`PumbilityTitleRails`, one per pool, held title → next title), the Titles drawer's "Your
progress" bar, the session page's "Titles you're working on" bars (`SessionTitleBars`), and the
title track on `/TierLists`. The level rungs render as tick marks on the bars whose geometry
carries them — which turns out to be every **total-pool** bar, because of a fact worth stating
precisely:

**No pumbility bar runs 0 → threshold.** `Phoenix2TitleList.BuildList` finishes with
`TitleHelpers.LinkLadder` over the pumbility titles, flooring each rung at the rung below it, and
the session bars' milestone percents are floor-aware (`TitleSaga`). So a gem title's bar spans
exactly one gem — the drawer's DIAMOND bar runs 16,000 → 17,000, which is PLATINUM's five levels,
the rungs being climbed through on the way to the title. The `/Pumbility/Pool` rail (held → next)
spans the *current* gem the same way.

**One formula covers every bar**: tick each rung whose threshold falls strictly inside the bar's
span, positioned proportionally; brighten the tick of the rung the pool stands on. The formula
yields four evenly spaced ticks on every gem bar and naturally yields zero where ticks would be
wrong — BRONZE's first rung (0 → 10,000 contains no levels), non-pumbility titles, Phoenix.

Excluded on pool rather than geometry: the `/TierLists` track and the Singles/Doubles rails run on
the per-type pools, and the per-type ladders have no levels (the site draws no badge on those board
tabs). The hero's "Your bar" card is not a growth bar at all — it is the 50th chart's value.

Implementation is presentation-only: a pure `PumbilityLevelMarkers` service and a shared
`PumbilityLevelTicks` component in Web, consumed by the three total-pool bars. The session bars
need no model change — the component resolves floor and threshold from the title name against the
floor-linked `BuildList` output. No contract, no query, no storage, no localizer keys (the labels
are the game's own `LV.n` notation).

**The markers are big, and the labels are visible** (owner, field-testing the first cut: "I
expected big ol' markers with visible labels"). The first version shrank the ticks to fit inside
each bar's `overflow: hidden` clip and dropped the labels — wrong trade. `PumbilityLevelTicks`
now *wraps* the bar it decorates: the frame reserves label headroom above, the `LV.n` labels
render into it, and the ticks overshoot the bar's edges — while the bar itself keeps its own
rounded clip untouched. A gem bar whose span holds no rungs (BRONZE's first) renders its child
bare, so the frame never adds layout where there is nothing to show.

## 9. Explicitly out of scope

- **Peers by pumbility level** (owner: "not yet") — needs a `GetPlayersByPumbilityLevel` beside
  `GetPlayersByCompetitiveRange` and moves the cohort behind every projected number on /Pumbility.
  Competitive level and pumbility level are different axes; that swap wants its own validation run.
- The XX/Phoenix-1 mixes — no gem ladder exists there; every surface here is Phoenix 2-gated.

## 10. Artifacts

- Ladder reference (all 37 badges, cutoff intervals): the "pumbility-levels" artifact page.
- Scope + mocks (drawer, strip, card, feeds): the "pumbility-level-scope" artifact page.
- Badge art + board CSV + recon reports: `~/Downloads/pumbility-level-badges/`, staged upload copies
  in `upload/`.
