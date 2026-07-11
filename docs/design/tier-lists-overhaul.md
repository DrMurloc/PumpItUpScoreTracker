# Tier Lists Overhaul — Design

**Status: implemented (C1–C10 landed); round-6 field-test items in flight.** Decisions below were locked in the 2026-07-10 workshop; open items are marked. Companion doc: [folder-level-progression.md](folder-level-progression.md) (deliberately decoupled, not yet workshopped). The old page at `/TierLists/Old` is removed as part of this series (round 6 pulled it into scope).

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

**Round 2 (2026-07-11):** density is chosen in the page toolbar and **persists per page** ("you use different ones based on your current task") — this amends UX-GUIDELINES rule 5: the three sanctioned modes are unchanged, but the UiSettings key becomes per-page (`Density__<Page>`), with `Universal__Density` retired before it ever shipped; the guideline reword lands in the implementation PR · QR + folder URL on share images approved · section collapse persists **globally by tier name** (a tier you never care about stays collapsed across folders — per-folder would make sections "pop around" as you walk folders) · By-Skill view **stays** in the View switch for P1, upgraded from "demote" because skill automation is now a live prospect (§8a). *(Round 3 = the piucenter integration decisions, recorded in §8a.)*

**Round 4 (2026-07-11, applied in workshop-v3):** the four ApexCharts **radar panels are retired as a concept** — the By-Skill view absorbs their information: one section per skill, sorted by the player's weakest skill first, with per-skill pass count + average score in each section header (heat-colored by pass rate). The information is the same; the placement makes it actionable (the charts to fix are directly under the stat). The details dialog gains the user's **cross-mix score journey** — a compact timeline from ScoreLedger's append-only `ScoreEventJournal`, linking to the full journey. PIU Center attribution renders in the By-Skill header and the dialog's skills row.

**Round 5 (2026-07-11):** piucenter ingestion boundary locked (§8a) — no import of their full chart rendering; chart details link out for "see more"; categorization-relevant metadata + numeric skill frequencies only, the latter banked for future cross-skill transfer prediction. Rollout fixed as **one PR** with the C1–C10 series (§12). **XX and older mixes**: the page shows a localized **"Tier lists for XX and older coming soon"** state instead of today's silent Phoenix-data fallback (`ListMix` masquerade retired for this page) — the owner has ideas for those mixes, deliberately out of scope.

**Round 6 (2026-07-11, owner field test of C1–C10, desktop):** the toolbar splits on a new principle — **"changes the data you see" stays sticky (folder → view → Personalized); "changes how it presents" moves to a content bar directly above the list** (Ranked-by, Grouped-by, density, Download, Filters-as-icon-with-count-badge) — the single big toolbar read as overwhelming. This amends UX-GUIDELINES rule 6 (the filter row is no longer sticky; the drawer + chips move with the content, the mobile bottom bar keeps thumb reach). **Ranked-by shows in every view** (reversing round 1's hide-under-My-Progress): when the grouping isn't the lens, each card/row carries its lens tier — a named column in Table, the tier word on the card in Comfortable, a top-right diff-colored dot + tooltip word in Compact. **Progress strip**: general folder progress (pass rate + grade lamps + rarity line) always shows; the title bars collapse under **"Title Levels"** (same label on every mix), default collapsed. **"Applied Filters:"** label precedes the chips. Table density regains the song jacket as column 1 (rule 2). Bug fixes from the same session: jacket `url(&quot;)` corruption, legend swatch CSS-order override, MudMenu-based folder picker closing on type switch (rebuilt on an owned popover; component genericized to `FolderPicker`). Shared component styles moved to `site.css` — page style blocks can't back reusable components. Scope additions from the same session: the piucenter crawler, `/TierLists/Old` removal, and the Playwright rewrite are now in scope (§12 addendum).

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
- **Chart-details dialog** (tap any card): **leads with the video** (as today's video dialog does), then chart meta (BPM, note count, step artist, song artist), the user's **cross-mix score journey** (compact timeline from ScoreLedger's `ScoreEventJournal`, linking to the full journey), placements across all lenses, To-Do toggle, score recording (reuses the existing edit grid — deliberately low-key), link to `/Chart/{id}`, and a **"View full chart on PIU Center"** link-out (§8a — their scrollable chart rendering is deliberately not imported). Ingested PIU Center step-data metadata lands here too, with the attribution footer. Leaves a slot for future comments/UGC. One shared component — candidates elsewhere (/Charts, WeeklyCharts) adopt it in later passes.
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
| Skill tags on cards / By-Skill view (radar charts retired — round 4) | ✅ | ❌ off until piucenter data covers P2 | n/a — page shows "coming soon" |
| Chabala lens | ✅ (existing links; becomes imported difficulty attribution later) | ❌ | n/a — page shows "coming soon" |
| Paragon / title progress strip | ✅ unchanged | ❌ (Folder Level doc will fill this) | n/a — page shows "coming soon" |
| Personalized blend inputs | Pass Count + Skill + Similar Players | Pass Count + Similar Players | n/a |
| Provisional-fallback badge | n/a | ✅ stays | n/a |

XX and older mixes get the whole-page **"Tier lists for XX and older coming soon"** state (round 5) — the per-capability XX column is moot for this page.

Skill automation is out of scope for the overhaul itself; Phoenix 1 skills stay read-only ("leave something behind"). Research findings for the follow-on project:

### 8a. Skill automation: piucenter.com integration (2026-07-11)

piucenter's `/skill` pages are generated from a **per-chart feature matrix** (~35 numeric columns: Run/Drill/Jack/Footswitch/Bracket frequencies, five twist-angle grades, travel distance, irregular rhythm, hands, etc.), computed by a pipeline ([maxwshen/piu-analysis](https://github.com/maxwshen/piu-analysis)) that parses stepcharts and annotates limb placement (author-estimated 80–90% accurate). The raw matrix is strictly richer than our boolean tags — frequencies with tunable thresholds, and the raw material for "you're weak at brackets" analysis.

**Status (owner knowledge, 2026-07-11 — the public GitHub lags the live project):** piucenter is **active** — data covers through the latest Phoenix 1 patch, the community Discord was active as of May 2026, and **aesthete** currently maintains it. The owner knows the maintainers and has discussed integration before; no export negotiation gates this work.

- **Plan (owner-locked): HTML-crawl it now.** A **weekly** Hangfire job, **gap-driven** — it only fetches for charts we're missing skill data on, so steady-state runs are near no-ops. Crawl the **per-chart pages** (`/chart/<key>`), not the `/skill` listings: the listings only expose top-20-per-level names, while chart pages carry the full per-chart analysis — which also feeds the metadata ingestion below. HtmlAgilityPack client in the OfficialMirror ACL mold. "Charts that changed" is deferred — charts only really change between mixes.
- **Generic external-name map**: mismatched names land in a shared alias table keyed with a **Source column** — `(Source, ExternalKey) → ChartId` — because a second community-tool integration is planned later. piucenter's key format `"<Song> - <Artist> <S|D><level> <variant>"` doubles as the crawl-URL builder, so the alias table is also the fetch plan. Most songs should auto-match on normalization; owner + Claude seed the long tail.
- **Ingestion boundary (owner-locked, round 5)**: we do **not** import their full scrollable chart rendering — that's piucenter's own value, and our chart details **link out** to it ("View full chart on PIU Center") for "see more". We ingest only what directly answers *how charts are categorized*, plus the **numeric skill frequencies** (`ChartSkillMetric`) banked for future personalized algorithms — cross-skill transfer prediction ("you were good at X, you'll be good at Y") when the personalization revisit happens. piucenter remains our primary step-data source, with the **"PIU Center" attribution footer on the chart detail page**. The link-out ships with the overhaul as a best-effort URL from their key format; the alias table (crawler project) hardens it.
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

Folder Level progression (own doc) · UGC comments (dialog reserves the slot) · deep SEO pass · native apps (explicit non-goal — perf work exists to avoid them). *(Round 6 pulled `/TierLists/Old` removal and the Playwright rewrite into this PR, and greenlit the piucenter crawler (§8a) as its own follow-up PR.)*

## 12. Rollout: one PR, a fixed commit series (owner field-tests each checkpoint)

The design-doc commits already on this branch are the prelude (C0); implementation lands as follow-on commits on the same branch. **One PR total** — the PR #134 pattern. Suites stay green at every commit; the Playwright tier-list workflow updates land with the commit that moves its cheese (C3/C4), never deferred.

1. **C1 — data layer** (no UI change): `ChartScoreStats` + `UserTierListEntry` entities, repositories, and migrations with covering indexes designed up front; `UserTierListMaterializer` consuming `PlayerScoresUpdatedEvent`; tier-list sagas persist stats during rebuilds; admin-triggered throttled backfill command. DATABASE-SCHEMA.md rows.
2. **C2 — contracts** (no UI change): `GetBlendedTierListQuery` (ChartIntelligence — the blend math leaves the page, gains ApplicationTests per the handler pattern); ScoreLedger's cross-mix pass read + `GetChartScoreJourneyQuery`.
3. **C3 — routes**: `/TierLists/{Single|Double|CoOp}/{level}`, 301s from `/ChartSkills` / `/PersonalizedTierList` / query-param forms, sitemap folder entries.
4. **C4 — shell**: sticky toolbar, folder picker, lens/view/Personalized model, coalesced + cancellable reload on the blend query, answer-first layout, progress strip + lamp strip, skeletons and empty states, border language + legend.
5. **C5 — density**: `Density__TierLists` + Comfortable/Compact/Table via `ChartCard`/`TierSection`; Text View retired; legacy `TierLists__*` settings read-migrated once.
6. **C6 — details dialog**: video-first `ChartDetailsDialog` — meta, cross-mix journey, placements, To-Do, quiet recording, PIU Center link-out, comments slot.
7. **C7 — By-Skill view**: weakest-first skill sections with per-skill stats; the four ApexCharts radar panels removed.
8. **C8 — share renderer**: `IShareCardRenderer` port + SkiaSharp client in Data; Download swap with the Community/Personalized stamp.
9. **C9 — og:image**: `refresh-folder-share-cards` Hangfire job + blob wiring + per-folder meta tags; SCHEDULED-JOBS.md row.
10. **C10 — cleanup**: localization sweep (all eight locales, universal-terms glossary note), `UiColorTokenTests` ChartSkills 7 → 0, UX-GUIDELINES rule-5 reword + CLAUDE.md density line, final E2E pass.

**Round-6 addendum** — the series continues on the same PR: **C11** field-test bug fixes (jacket URLs, legend CSS order, folder-picker popover) · **C12** field-test UX (toolbar data/presentation split, lens in every view, Title Levels collapse, table jacket column, Applied Filters label) · **C13** `/TierLists/Old` + `TierListSection` removal (26 color-literal allowlist entries burn down; the URL survives as a 301) · **C14** Playwright rewrite against the new markup. Outside this PR: the piucenter crawler + alias table (§8a — greenlit, its own PR), Folder Level (own doc).

**Round 9 (2026-07-11, compact-mode redesign — C18):** round 8's bottom action bar stacked awkwardly on the shell's bottom nav — **deleted**. New compact model (<960): the toolbar is **one clean row — Folder → view buttons → "Advanced"**; the **Advanced drawer** holds Personalized, Ranked-by, Grouped-by, Download, and the entire filter panel (one drawer, not a drawer-opens-a-drawer); **Folder Stats moves to the Ranked-by slot in the content bar** (the lens controls only render there on desktop). Below **600px** the Advanced button reorders before the view group and the toolbar centers, so the wrap lands deliberately as folder + Advanced / views (the view buttons were wrapping raggedly ~450, language-dependent). Shared RenderFragments (`lensControls`, `filterPanel`) keep the desktop content bar / Filters drawer and the compact Advanced drawer from drifting. Razor gotcha recorded: inline `@<text>` template assignments parse only in code mode — after markup siblings they need an explicit `@{ }` block.

**Round 8 (2026-07-11, responsive pass — C17):** one breakpoint story: a **1680px max-width clamp** for 4K, and a single **960px compact breakpoint** matching the shell's bottom nav. Below 960: the toolbar renders as **two explicit rows** (folder + views / Personalized + actions — `display:contents` flattens them on desktop, killing the awkward free-wrap), the page's bottom action bar (folder · Download · Filters) extends up from phones-only to the whole compact tier, and the **progress strip + border legend move behind a "Folder Stats" drawer** (trigger button in toolbar row 2) that also contains the title progress — the owner's directive: get everything out of mobile's above-the-fold, the tier sections are the answer (rule 1). The stats markup is one shared RenderFragment so the desktop strip and the drawer can't drift. Bar labels shortened to **"Score in Folder"** / **"Passes in Folder"** (the similar-players context lives in the note text). Section headers stop double-printing count + stat — the stat carries the count; the bare count is the anonymous fallback. Also from this session: the admin **"Rebuild Tier Lists"** button (locally `PreventRecurringJobs` empties the Hangfire Recurring Jobs tab, so the dashboard can't trigger the rebuilds — the button publishes the same messages).

**Round 7 (2026-07-11, second desktop field-test pass):** **C15** polish — top-of-page breathing room; Download returns to the sticky toolbar right-aligned with a spinner + "Generating" state (and the real fix: the share renderer's sequential image fetches parallelized, ~10s → ~1s cold); density selector sits left of the Filters icon, whose tooltip becomes **"Filters and Columns"**; the drawer's Display switches actually render again — Step Artist / Skills / Age on Comfortable cards and as Table columns, disabled in Compact with a "Not available in Compact view" note, always shown in the details dialog (plus the lens tier and score age); the dead **Show Difficulty switch retired** (superseded by the round-6 always-on lens annotation); **Ranked-by orders cards within every section in every view** (hardest first, scoring level tiebreaker, most-popular-first under Popularity); "Title Levels" renamed **"Folder Levels"**. **C16** cohort bars — the "averaging better than" text becomes a **"Competitive Ranking in Folder"** bar (rarity-ramp fill + printed % + cohort size), joined by **"Folder Passes vs Similar Players"**: new `scores.FolderCohortStats` table (per-folder pass-count histograms per half-level competitive bucket, written during the daily scores rebuild — owner rejected on-demand cohort queries after the PR #129 incident), merged at read time across the ±0.5 window into player count / average passes / pass percentile. Personalized stays visible in every view (post-round-6, the lens output shows everywhere). Passes-vs-peers deliberately lives HERE and not in Folder Levels — the owner expects Folder Levels to be self-progress, not peer comparison.

## 13. Open questions

All three original questions were resolved 2026-07-11 (toolbar + per-page density; By-Skill view stays for P1; QR approved — see the round-2 feedback note in §2). Remaining follow-ups, none blocking implementation:

1. Colorblind-simulator pass on the dashed-blue / dashed-green border pair before ship (rule 8).
2. Build the piucenter crawler + generic alias table (§8a) — its own project, independent of the overhaul.
3. Folder Level workshop ([folder-level-progression.md](folder-level-progression.md)).

## 14. Technical scoping (2026-07-11)

### UI surface area

- **Pages**: `Pages/TierLists/ChartSkills.razor` rebuilt (blend math leaves the page — see below); gains `/TierLists/{Single|Double|CoOp}/{level}` routes with 301s from `/ChartSkills`, `/PersonalizedTierList`, and query-param forms. Sitemap controller +~60 folder URLs. `/Chart/{id}` untouched until the crawler project (attribution footer).
- **New shared components** (`Components/`): `ChartDetailsDialog` (video-first; wraps `EditChartGrid` + `ChartVideoDisplayer`), `FolderPicker`, `ChartCard` (all three densities), `TierSection` (collapsible), `LampStrip`, `FilterDrawer` + `FilterChips`, `DensityToggle`, `TierListSkeleton`, `BorderLegend`. `TitleProgressBar` gains a compact variant; `DifficultyBubble`/`ScoreBreakdown` consumed as-is.
- **Retired**: Text View, per-element Show-X toggles, `TierListSection` (old page only), the four ApexCharts radar panels (By-Skill view absorbs them — round 4). **Ratchets**: `UiColorTokenTests` ChartSkills 7 → 0; `L[…]` sweep, all eight locales.

### Architecture

- **Verticals**: ChartIntelligence (center of gravity), ScoreLedger (one published read added), Catalog (crawler project only), PlayerProgress/Identity consumed as-is, plus Web and CompositionRoot.
- **New contract**: `GetBlendedTierListQuery` (ChartIntelligence) — lens/personalized blend moves from the .razor into a handler: cacheable, cancellable, testable in ApplicationTests; internalizes the page's direct `ITierListRepository.GetUsersOnLevel` port injection.
- **New internal entities** (ChartIntelligence, via its `IDbModelContribution`): `UserTierListEntry` + repository, `ChartScoreStats`.
- **New consumer**: `UserTierListMaterializer : IConsumer<PlayerScoresUpdatedEvent>` (event already fires per score batch).
- **ScoreLedger**: cross-mix passed-chart-ids read (`IScoreReader` method or small contract query) for the dashed-green border; plus a `GetChartScoreJourneyQuery(userId, chartId)` contract reading the append-only `ScoreEventJournal` across mixes for the dialog's journey timeline.
- **SharedKernel**: `MixCapabilities` static policy (skills/Chabala/radar/titles per mix) — gates display and blend inputs; SharedKernel references nothing so both Web and ChartIntelligence read it.
- **Share renderer**: `IShareCardRenderer` port in `Domain.SecondaryPorts`, SkiaSharp implementation in `Data.Clients` (Data allowlist +SkiaSharp), output via existing `IFileUploadClient`; shared by the Download endpoint and the og:image job.
- **Crawler project (later)**: piucenter client behind a Domain port, HtmlAgilityPack impl in `Data.Apis` (`PiuGameApi` precedent); ingestion consumers in Catalog.

### SQL

| Table | Owner | Shape |
|---|---|---|
| `scores.UserTierListEntry` | ChartIntelligence | PK (Mix, UserId, ChartId); covering index (Mix, ChartId) for similar-players aggregation; ~800K rows post-backfill |
| `scores.ChartScoreStats` | ChartIntelligence | (Mix, ChartId) → variance band + per-grade clear stats; kills per-view `CalculateVariance` |
| `scores.ExternalChartAlias` | Catalog *(crawler)* | unique (Source, ExternalKey) → ChartId |
| `scores.ChartSkillMetric` | Catalog *(crawler)* | (ChartId, Skill, Frequency, Source) — frequencies alongside boolean `scores.ChartSkill` |

No changed tables: `scores.TierListEntry` stays untouched (stats are a sibling); `scores.UserSettings` is key/value (new keys `Density__TierLists`, collapsed-tiers JSON; legacy `TierLists__*` keys read-migrated once). 2–3 EF migrations, indexes designed up front (PR #129 discipline), DATABASE-SCHEMA.md rows per table. Backfill = admin-triggered throttled bus command (~27K folder computations), not a migration.

### Jobs & background services

- **Event-driven, no schedule**: `UserTierListMaterializer` (idempotent per in-memory-transport rules).
- **Extended existing**: `TierListSaga` + `ScoringDifficultySaga` also persist `ChartScoreStats` during their daily rebuilds.
- **New Hangfire rows** (each = `RecurringJobRunner` method + `Program.cs` line + SCHEDULED-JOBS.md row): `refresh-folder-share-cards` (daily ~10:30 UTC, after tier-list rebuilds — regenerates per-folder og:images to blob); `crawl-piucenter` (weekly, gap-driven; crawler project).
- **One-time**: `UserTierListEntry` backfill, admin-triggered.
- **Not happening**: no new hosted services/timers, no second scheduler, no second DbContext.
