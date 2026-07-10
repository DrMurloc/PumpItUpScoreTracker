# Tier Lists Overhaul — Design

**Status: draft for owner review.** Decisions below were locked in the 2026-07-10 workshop; open items are marked. Companion doc: [folder-level-progression.md](folder-level-progression.md) (deliberately decoupled, not yet workshopped). The old page at `/TierLists/Old` is out of scope entirely.

## 1. Why

Tier lists are the site's most-used feature (~28% of traffic, ~60% of it anonymous) and its most complained-about ("tier list page is too slow"). The current page buries the answer under two-plus phone screens of controls and personal stats, names its aggregation concepts in statistician jargon, hides the Download Image tool, and recomputes population-wide statistics on every page view.

**The jobs, owner-ranked:**

1. View folder completion from different angles
2. Snapshot folder/title progress (important, but currently over-spent on space)
3. Tag charts as To-Do
4. Prioritize/explore potential To-Dos — videos today, a chart-details dialog tomorrow

**Performance is a product goal, not a nicety**: the owner's fallback plan for slowness is native apps as local caches. This design's perf targets exist to make those apps unnecessary.

## 2. Workshop decision log

| Decision | Outcome |
|---|---|
| Anonymous experience | First-class (~60% of usage). Population data renders fast with no login. |
| Lens naming | "Ranked by" picker: **Community Pass** (default), **Community Score** primary; Popularity / Chabala / PG behind "Other lists". No "(Data Backed)" suffixes. |
| Personalization | Explicit, **visibly badged** toggle ("Tuned to you"). Never silently changes a shared reference list. |
| Score recording | Lives in the chart-details dialog (manual recordings are rare). |
| Folder Level / titles | Decoupled into its own design doc. **Paragon/title progress remains for Phoenix 1 unchanged.** Phoenix 2 gets a solution later. |
| Download image | Full redesign, themed, "fancy": shared renderer also produces per-folder `og:image`. |
| Perf target | Sub-second perceived folder switch; skeleton-of-categories as the loading state. |
| Text View | Folds into **Table** density. Revisit only if users gripe. |
| Density default | **Comfortable** (matches today's cards — no jarring unopted change). |
| Routing | Path-based folder URLs with 301s from the query-param form. |
| Precompute | On the table; sized in §6 (2,333 users total, ~1,800 with scores, weekly-ish import cadence). |
| Skills / Chabala | Phoenix 1 only (manual upkeep unsustainable). Per-mix capability flag, §8. |

## 3. Mental model: three concepts the UI stops blending

Today "Difficulty Categorization", "Group By", and a hidden "Personalized Difficulty" checkbox interleave three orthogonal ideas. The overhaul separates them:

1. **Ranked by (lens)** — which data ranks the charts. Community Pass, Community Score, Scoring Level; Popularity, Chabala (P1), PG under "Other lists". One picker, plain names.
2. **Tuned to me** — an explicit toggle that bends the active lens with my skill/similar-player data. When ON, the page and any exported image carry a **"Tuned to {gametag}"** badge; OFF is the shared community reference. (Fixes the trust problem: two players comparing screens can see *why* their lists differ.)
3. **View** — *whose buckets section the folder*:
   - **Tier List** — sections are the lens's tiers, displayed with the friendly scale **"1+ Level Easier / Very Easy / Easy / Medium / Hard / Very Hard / 1+ Level Harder / Not Rated"** (raw enum names like "Overrated" never render).
   - **My Progress** — sections are *my* buckets: score percentile (rarity ramp + printed %), my score bands, score recency. This absorbs Group By's Age/Score/Score Ranking and is the home of job #1.
   - **By Skill** (Phoenix 1 only) — the current skill grouping, behind the capability flag.

"Group By" dies as a user-facing concept.

## 4. Page anatomy

```
┌─ sticky toolbar ──────────────────────────────────────────────┐
│ [◀ D18 ▶]  Ranked by: Community Pass ▾   [⚡ Tuned to me]      │
│ [Tier List | My Progress]   …   [density ▪▪▫] [Download] [Filters] │
│ (active-filter chips row, only when filters are active)        │
└───────────────────────────────────────────────────────────────┘
┌─ progress strip (logged in; collapsible, collapsed = 1 line) ─┐
│ Paragon Lv (P1) · segmented lamp bar · rarity sentence         │
└───────────────────────────────────────────────────────────────┘
   tier sections — the answer, inside the first viewport at 390×844
```

- **Folder picker**: type + level is one domain concept (*folder*), so it is one control — tap the pill, choose Single/Double/CoOp and level in a single gesture, **one load**. Prev/next arrows for sequential folder walking. This kills the S14→D18 double-load by construction; cancellation (§6) covers rapid stepping.
- **Toolbar**: sticky, compact (2 rows max). Download is a first-class button — it's the offline mechanism and the community's sharing tool, never buried again. Filters follow rule 6: collapsed row, active filters as removable chips, full panel in a drawer. Long-tail filters (BPM, note count, artists) demote gracefully since that data moves into the details dialog.
- **Progress strip** replaces today's nine stacked bars: one **segmented lamp strip** (Pass → AA … SSS+ → PG as compact chips with counts), the Paragon/title line (P1), and the rarity-styled "averaging better than X% of N similar players" sentence. Collapsible; collapsed state is a one-line summary. **Anonymous users** instead get the rule-9 empty state: "Import your scores to light this folder up" with sign-in/import actions.
- **Cards** (per density, all via `Universal__Density`):
  - *Comfortable* (default): jacket + `DifficultyBubble` + grade/plate overlay, song name, To-Do + Details actions. Pass = solid success border, unpassed = dashed (shape channel per rule 8), To-Do = filled bookmark icon (not color-only).
  - *Compact*: jacket sticker sheet with corner badges — the at-a-glance completion view and the closest on-screen analog of the share image.
  - *Table*: sortable rows (song, tier, my score, grade, plate, percentile, To-Do). Text View's replacement.
- **Chart-details dialog** (tap any card): video, chart meta (BPM, note count, step artist, song artist), placements across all lenses, To-Do toggle, score recording (reuses the existing edit grid — deliberately low-key), link to `/Chart/{id}`. Leaves a slot for future comments/UGC. One shared component — candidates elsewhere (/Charts, WeeklyCharts) adopt it in later passes.
- **Mobile (rule 10)**: at phone widths a bottom action bar carries folder pill, Filters, and Download; the toolbar collapses to essentials.

## 5. Loading, empty, and failure states (rule 9)

- **Skeletons match the layout**: section headers + jacket-grid shimmer, never a lone spinner.
- **Two-phase paint**: population content (tier sections, jackets) renders immediately from cache; the personal overlay (grades, borders, lamps, percentiles) streams in and fades up. Anonymous users simply never wait for phase two.
- **The blank-page guard dies**: today one stale cached ChartId hides the entire list (`_finalEntries.All(...)` gate). New behavior: render every chart we can resolve, drop unresolvable cached entries, and trigger a background refresh. The page never renders nothing without saying why.

## 6. Data & performance architecture

Scarce resource is **SQL DTUs**, not storage (post-incident target tier is small). Spend disk to save compute; spend compute in background batches, never per-request.

**Tier 1 — materialize population aggregates.** Variance per chart and per-tier clear-rate stats become columns/sibling rows maintained on the existing tier-list refresh cadence (Hangfire). Kills `CalculateVariance` — which today refetches *every player's scores in the folder on every recalculate* — and the per-view popularity refetch. Storage: one number per chart, negligible.

**Tier 2 — materialize per-user relative tier lists, event-driven.** When a score import lands (consume the score-batch events already on the bus), recompute *that user's* relative categories for *the folders the import touched* and store them as rows. "Similar players" then becomes one set-based query over an indexed table instead of a `GetMyRelativeTierListQuery` fan-out per neighboring player.

- Sizing: ~1,800 scoring users × ~15 meaningful folders × ~30 charts ≈ **800K skinny rows** (chartId, category, order) — tens of MB with indexes.
- Write load: most users import weekly, power users daily; an import touches a handful of folders → hundreds of small recomputes/day. Trivial.
- One-time backfill: ~27K folder computations, throttled, off-peak, with the covering index designed up front (PR #129 discipline).
- Vertical home: **ChartIntelligence** (owns tier-list math); consumes score events via published contracts, never reads another vertical's tables.

**Tier 3 — the personalized blend stays on-demand.** With tiers 1–2 materialized it's arithmetic over indexed reads; keep the in-memory cache (restore-on-restart is not needed at this cost). Do **not** precompute user × folder blends — that's the combinatorial trap.

**Interaction layer:**

- Single coalesced reload pipeline keyed by (folder, lens, tuned) with `CancellationToken` cancel-previous — rapid level stepping and folder switches never queue.
- Grouping/view/display changes are **pure in-memory re-renders** — never refetch.
- UiSettings persistence: one debounced batch write per settled state, replacing today's five sequential awaited writes at the top of every recalculate.
- Card grids virtualize (`Virtualize`) — DOM size is a real cost on phones.

**Budget**: population paint from cache < 100 ms server-side; folder switch sub-second perceived with skeleton in between; personal overlay may trail by a beat.

## 7. Routing, sitemap, share image

- **Routes**: `/TierLists/{Single|Double|CoOp}/{level}` (e.g. `/TierLists/Double/18`). Query-param form 301s to it. `/ChartSkills` and `/PersonalizedTierList` 301 to `/TierLists`.
- **Sitemap**: one entry per folder (~60 URLs) via the existing sitemap controller.
- **`og:image` per folder**: the share renderer's community version, regenerated on the tier-list refresh cadence, served static. Discord unfurls become the tier list itself — the site's real social channel. Anything deeper (SSR strategy, crawler-facing text) is deferred to a separate SEO pass.
- **Share image (Download)**: rebuilt on **SkiaSharp** (cross-platform; System.Drawing is Windows-only and retires with this). One renderer, two consumers: the Download button (user's current view, stamped **Community** or **Tuned to {gametag}**) and the og:image job (community only). Themed from the mix palette: folder bubble + lens title header, tier rows on the `--diff-*` ramp with jacket thumbnails and grade/plate badges, footer with site URL + QR code + date.

## 8. Per-mix capability flags

One flag set, resolved per `ListMix`:

| Capability | Phoenix (1) | Phoenix 2 | XX (views Phoenix data) |
|---|---|---|---|
| Skill tags on cards / By-Skill view / radar charts | ✅ | ❌ off until tagging is automated | ✅ (Phoenix data) |
| Chabala lens | ✅ (existing links) | ❌ | ✅ |
| Paragon / title progress strip | ✅ unchanged | ❌ (Folder Level doc will fill this) | per existing XX behavior |
| Personalized blend inputs | Pass Count + Skill + Similar Players | Pass Count + Similar Players | n/a |
| Provisional-fallback badge | n/a | ✅ stays | n/a |

Skill automation is explicitly out of scope; Phoenix 1 skills stay read-only ("leave something behind").

## 9. Localization (rule 7)

Every string on the page goes through `L[…]` — the current page has dozens of bypasses (menu labels, tooltips, bucket-name dictionaries, snackbars, the explainer). New keys land in all eight locales in the same pass. A **universal-terms, do-not-translate list** is added to the glossaries: "Why Don't You Get Up and Dance, Man?" (in-game meme), "Chabala", "PG", lamp names.

## 10. UX-rules compliance map

| Rule | How this design satisfies it |
|---|---|
| 1 Answer above the fold | Sticky 2-row toolbar + collapsed strip; first tier section inside first viewport at 390×844 |
| 2 Show don't tell | Jackets are the identifier at every density; grades/plates as art; bubbles everywhere |
| 3 One concept one component | Details dialog is one shared component; cards render `DifficultyBubble`/`ScoreBreakdown`/`LetterGradeIcon`; no page-local restyles |
| 4 No color literals | All new UI reads `--mix-*`/`--diff-*`/`--rarity-*`/`--plate-*`; burns down ChartSkills' 7 allowlist entries; variance icon re-encodes with shape + label |
| 5 Density | `Universal__Density` lands here: Comfortable (default) / Compact / Table; Text View retired |
| 6 Filters are furniture | Collapsed sticky row + chips + drawer; filters never push the answer down |
| 7 +40% text | Full `L[…]` coverage; no fixed-width labels; the "Pneum" hack replaced by generic truncation-with-tooltip |
| 8 Color never alone | Pass = border *style* + color; To-Do = icon + color; percentile = printed number + glow ramp; tier names printed beside tier colors |
| 9 Loading looks like layout | Section skeletons; two-phase paint; import CTA empty state; blank-page guard removed |
| 10 Thumbs first | Mobile bottom action bar (folder, Filters, Download) |

## 11. Out of scope

Folder Level progression (own doc) · skill-tagging automation · UGC comments (dialog reserves the slot) · deep SEO pass · native apps (explicit non-goal — perf work exists to avoid them) · `/TierLists/Old` removal timing.

## 12. Rollout sketch (phased commits, owner field-tests each)

1. **C1 — data layer**: population aggregates + per-user relative tier list materialization + backfill job (no UI change).
2. **C2 — routes**: path-based folder URLs + 301s + sitemap entries.
3. **C3 — shell**: sticky toolbar, folder picker, lens/view/tuned model, coalesced+cancellable reload, answer-first layout, progress strip, skeleton/empty states.
4. **C4 — density**: `Universal__Density` + the three modes; Text View retired.
5. **C5 — details dialog** (with recording); card actions simplify.
6. **C6 — share renderer**: SkiaSharp, download button swap.
7. **C7 — og:image job + sitemap wiring**.
8. **C8 — cleanup**: localization sweep, color-token allowlist burn-down, dead settings migration.

## 13. Open questions

1. Density switcher placement: in the page toolbar (writes the universal setting) vs. `/Account` only. Design assumes toolbar.
2. Does the By-Skill view (P1) stay in the View switch or demote into the details-dialog-only presentation of skills?
3. Share-image QR: link to the exact folder URL — confirm we're happy putting URLs in community-shared images.
