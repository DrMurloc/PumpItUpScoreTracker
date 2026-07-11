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
| Lens naming | "Ranked by" picker: **Pass Difficulty** (default), **Score Difficulty** primary; Popularity / Chabala / PG behind "Other lists". No "(Data Backed)" suffixes. |
| Personalization | Explicit, **visibly badged** toggle ("Personalized"). Never silently changes a shared reference list. |
| Score recording | Lives in the chart-details dialog (manual recordings are rare). |
| Folder Level / titles | Decoupled into its own design doc. **Paragon/title progress remains for Phoenix 1 unchanged.** Phoenix 2 gets a solution later. |
| Download image | Full redesign, themed, "fancy": shared renderer also produces per-folder `og:image`. |
| Perf target | Sub-second perceived folder switch; skeleton-of-categories as the loading state. |
| Text View | Folds into **Table** density. Revisit only if users gripe. |
| Density default | **Comfortable** (matches today's cards — no jarring unopted change). |
| Routing | Path-based folder URLs with 301s from the query-param form. |
| Precompute | On the table; sized in §6 (2,333 users total, ~1,800 with scores, weekly-ish import cadence). |
| Skills / Chabala | Phoenix 1 only (manual upkeep unsustainable). Per-mix capability flag, §8. |

**Mock feedback round 1 (2026-07-10, applied in workshop-v2):** "Personalized" replaces "Tuned to me" · lens names are **Pass Difficulty** / **Score Difficulty** · Ranked-by hides while My Progress is active (the lens has no effect there) · tier sections collapse, collapsed set persisted per user in UiSettings · border language locked (§4) · filters drawer gains song type / letter grade / plate as first-class · details dialog leads with the video, as today's video dialog does.

**Round 2 (2026-07-11):** density is chosen in the page toolbar and **persists per page** ("you use different ones based on your current task") — this amends UX-GUIDELINES rule 5: the three sanctioned modes are unchanged, but the UiSettings key becomes per-page (`Density__<Page>`), with `Universal__Density` retired before it ever shipped; the guideline reword lands in the implementation PR · QR + folder URL on share images approved · section collapse persists **globally by tier name** (a tier you never care about stays collapsed across folders — per-folder would make sections "pop around" as you walk folders) · By-Skill view **stays** in the View switch for P1, upgraded from "demote" because skill automation is now a live prospect (§8a).

## 3. Mental model: three concepts the UI stops blending

Today "Difficulty Categorization", "Group By", and a hidden "Personalized Difficulty" checkbox interleave three orthogonal ideas. The overhaul separates them:

1. **Ranked by (lens)** — which data ranks the charts. Pass Difficulty, Score Difficulty, Scoring Level; Popularity, Chabala (P1), PG under "Other lists". One picker, plain names. The picker **hides while My Progress is active** — the lens has no effect on personal buckets, and a control that does nothing is worse than no control.
2. **Personalized** — an explicit toggle that bends the active lens with my skill/similar-player data. When ON, the page and any exported image carry a **"Personalized for {gametag}"** badge; OFF is the shared community reference. (Fixes the trust problem: two players comparing screens can see *why* their lists differ.)
3. **View** — *whose buckets section the folder*:
   - **Tier List** — sections are the lens's tiers, displayed with the friendly scale **"1+ Level Easier / Very Easy / Easy / Medium / Hard / Very Hard / 1+ Level Harder / Not Rated"** (raw enum names like "Overrated" never render).
   - **My Progress** — sections are *my* buckets: score percentile (rarity ramp + printed %), my score bands, score recency. This absorbs Group By's Age/Score/Score Ranking and is the home of job #1.
   - **By Skill** (Phoenix 1 only) — the current skill grouping, behind the capability flag.

"Group By" dies as a user-facing concept.

## 4. Page anatomy

```
┌─ sticky toolbar ──────────────────────────────────────────────┐
│ [◀ D18 ▶]  Ranked by: Pass Difficulty ▾   [⚡ Personalized]    │
│ [Tier List | My Progress]   …   [density ▪▪▫] [Download] [Filters] │
│ (active-filter chips row, only when filters are active)        │
└───────────────────────────────────────────────────────────────┘
┌─ progress strip (logged in; collapsible, collapsed = 1 line) ─┐
│ Paragon Lv (P1) · segmented lamp bar · rarity sentence         │
└───────────────────────────────────────────────────────────────┘
   tier sections — the answer, inside the first viewport at 390×844
```

- **Folder picker**: type + level is one domain concept (*folder*), so it is one control — tap the pill, choose Single/Double/CoOp and level in a single gesture, **one load**. Prev/next arrows for sequential folder walking. This kills the S14→D18 double-load by construction; cancellation (§6) covers rapid stepping.
- **Toolbar**: sticky, compact (2 rows max). Download is a first-class button — it's the offline mechanism and the community's sharing tool, never buried again. Filters follow rule 6: collapsed row, active filters as removable chips, full panel in a drawer. Drawer contents: To-Do / passed / unplayed, **song type (Arcade, Remix, Shortcut, Full Song — each its own filter)**, **letter grade**, and **plate**. Long-tail data filters (BPM, note count, artists) retire — the details dialog carries that data.
- **Progress strip** replaces today's nine stacked bars: one **segmented lamp strip** (Pass → AA … SSS+ → PG as compact chips with counts), the Paragon/title line (P1), and the rarity-styled "averaging better than X% of N similar players" sentence. Collapsible; collapsed state is a one-line summary. **Anonymous users** instead get the rule-9 empty state: "Import your scores to light this folder up" with sign-in/import actions.
- **Sections**: diff-ramp stripe + friendly localized names ("1+ Level Easier" … "1+ Level Harder"); raw enum names never render. Sections **collapse from the header**; the collapsed set persists per user (UiSettings, part of the page settings blob).
- **Border language** (owner-locked): **solid green = passed**, **dashed blue = To-Do**, **dashed green = passed in another mix** (new state — one extra indexed cross-mix pass-set read in the personal overlay), neutral otherwise. Precedence passed > To-Do > other-mix; a passed To-Do just renders as passed (rare case, deliberately unhandled). The two dashed states differ by hue only — run the rule-8 colorblind-simulator check before ship (accepted follow-up). A small legend renders above the sections for signed-in users.
- **Cards** (per density; the mode is picked in the toolbar and persisted **per page** — `Density__TierLists`):
  - *Comfortable* (default): jacket + `DifficultyBubble` + grade/plate overlay, song name, To-Do + Details actions.
  - *Compact*: jacket sticker sheet with corner badges — the at-a-glance completion view and the closest on-screen analog of the share image.
  - *Table*: sortable rows (song, tier, my score, grade, plate, percentile, To-Do). Text View's replacement.
- **Chart-details dialog** (tap any card): **leads with the video** (as today's video dialog does), then chart meta (BPM, note count, step artist, song artist), placements across all lenses, To-Do toggle, score recording (reuses the existing edit grid — deliberately low-key), link to `/Chart/{id}`. Ingested PIU Center step-data metadata lands here too, with the attribution footer (§8a). Leaves a slot for future comments/UGC. One shared component — candidates elsewhere (/Charts, WeeklyCharts) adopt it in later passes.
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
- **Share image (Download)**: rebuilt on **SkiaSharp** (cross-platform; System.Drawing is Windows-only and retires with this). One renderer, two consumers: the Download button (user's current view, stamped **Community** or **Personalized — {gametag}**) and the og:image job (community only). Themed from the mix palette: folder bubble + lens title header, tier rows on the `--diff-*` ramp with jacket thumbnails and grade/plate badges, footer with site URL + QR code + date.

## 8. Per-mix capability flags

One flag set, resolved per `ListMix`:

| Capability | Phoenix (1) | Phoenix 2 | XX (views Phoenix data) |
|---|---|---|---|
| Skill tags on cards / By-Skill view / radar charts | ✅ | ❌ off until tagging is automated | ✅ (Phoenix data) |
| Chabala lens | ✅ (existing links) | ❌ | ✅ |
| Paragon / title progress strip | ✅ unchanged | ❌ (Folder Level doc will fill this) | per existing XX behavior |
| Personalized blend inputs | Pass Count + Skill + Similar Players | Pass Count + Similar Players | n/a |
| Provisional-fallback badge | n/a | ✅ stays | n/a |

Skill automation is out of scope for the overhaul itself; Phoenix 1 skills stay read-only ("leave something behind"). Research findings for the follow-on project:

### 8a. Skill automation: piucenter.com integration (2026-07-11)

piucenter's `/skill` pages are generated from a **per-chart feature matrix** (~35 numeric columns: Run/Drill/Jack/Footswitch/Bracket frequencies, five twist-angle grades, travel distance, irregular rhythm, hands, etc.), computed by a pipeline ([maxwshen/piu-analysis](https://github.com/maxwshen/piu-analysis)) that parses stepcharts and annotates limb placement (author-estimated 80–90% accurate). The raw matrix is strictly richer than our boolean tags — frequencies with tunable thresholds, and the raw material for "you're weak at brackets" analysis.

**Status (owner knowledge, 2026-07-11 — the public GitHub lags the live project):** piucenter is **active** — data covers through the latest Phoenix 1 patch, the community Discord was active as of May 2026, and **aesthete** currently maintains it. The owner knows the maintainers and has discussed integration before; no export negotiation gates this work.

- **Plan (owner-locked): HTML-crawl it now.** A **weekly** Hangfire job, **gap-driven** — it only fetches for charts we're missing skill data on, so steady-state runs are near no-ops. Crawl the **per-chart pages** (`/chart/<key>`), not the `/skill` listings: the listings only expose top-20-per-level names, while chart pages carry the full per-chart analysis — which also feeds the metadata ingestion below. HtmlAgilityPack client in the OfficialMirror ACL mold. "Charts that changed" is deferred — charts only really change between mixes.
- **Generic external-name map**: mismatched names land in a shared alias table keyed with a **Source column** — `(Source, ExternalKey) → ChartId` — because a second community-tool integration is planned later. piucenter's key format `"<Song> - <Artist> <S|D><level> <variant>"` doubles as the crawl-URL builder, so the alias table is also the fetch plan. Most songs should auto-match on normalization; owner + Claude seed the long tail.
- **Metadata beyond skills**: piucenter is effectively our **primary chart step-data source** going forward. Anything meaningful on their chart pages (data-driven difficulty prediction, stepchart-derived stats, similar charts) is fair game to ingest into the chart-details surface, with a **"PIU Center" attribution footer on the chart detail page**.
- **Attribution swap (owner-locked)**: PIU Center credit replaces **all** existing skills attribution — the Chabala skills credit goes away. Chabala's ongoing role is his **difficulty attribution** only: the Chabala lens becomes an import of his posted tier lists (later; Phoenix 1 only).
- **Phoenix 2**: expectation is that active downstream usage encourages upstream P2 coverage; the per-mix skills capability flag simply flips on when their data exists for a mix.
- **Fallback only**: forking piu-analysis and managing simfiles/runs ourselves is explicitly *not* the plan while piucenter is maintained — it's the contingency if the project goes dormant.

Sequence: (1) crawler + generic alias table; (2) weekly gap-driven job; (3) P1 skills flip to ingested data, attribution swapped to PIU Center; (4) chart-detail metadata ingestion + footer credit; (5) P2 when upstream covers it; (6) Chabala difficulty-attribution import (later, P1 only).

## 9. Localization (rule 7)

Every string on the page goes through `L[…]` — the current page has dozens of bypasses (menu labels, tooltips, bucket-name dictionaries, snackbars, the explainer). New keys land in all eight locales in the same pass. A **universal-terms, do-not-translate list** is added to the glossaries: "Why Don't You Get Up and Dance, Man?" (in-game meme), "Chabala", "PG", lamp names.

## 10. UX-rules compliance map

| Rule | How this design satisfies it |
|---|---|
| 1 Answer above the fold | Sticky 2-row toolbar + collapsed strip; first tier section inside first viewport at 390×844 |
| 2 Show don't tell | Jackets are the identifier at every density; grades/plates as art; bubbles everywhere |
| 3 One concept one component | Details dialog is one shared component; cards render `DifficultyBubble`/`ScoreBreakdown`/`LetterGradeIcon`; no page-local restyles |
| 4 No color literals | All new UI reads `--mix-*`/`--diff-*`/`--rarity-*`/`--plate-*`; burns down ChartSkills' 7 allowlist entries; variance icon re-encodes with shape + label |
| 5 Density | The three sanctioned modes land here: Comfortable (default) / Compact / Table; Text View retired. Persistence is per page (`Density__<Page>`) per the round-2 amendment — rule 5's wording updates in the implementation PR |
| 6 Filters are furniture | Collapsed sticky row + chips + drawer; filters never push the answer down |
| 7 +40% text | Full `L[…]` coverage; no fixed-width labels; the "Pneum" hack replaced by generic truncation-with-tooltip |
| 8 Color never alone | Passed vs To-Do borders pair hue with dash pattern; the dashed pair (blue To-Do vs green other-mix) is hue-differentiated only — colorblind-simulator check is an accepted pre-ship follow-up; percentile = printed number + glow ramp; tier names printed beside tier colors |
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

All three original questions were resolved 2026-07-11 (toolbar + per-page density; By-Skill view stays for P1; QR approved — see the round-2 feedback note in §2). Remaining follow-ups, none blocking implementation:

1. Colorblind-simulator pass on the dashed-blue / dashed-green border pair before ship (rule 8).
2. Build the piucenter crawler + generic alias table (§8a) — its own project, independent of the overhaul.
3. Folder Level workshop ([folder-level-progression.md](folder-level-progression.md)).
