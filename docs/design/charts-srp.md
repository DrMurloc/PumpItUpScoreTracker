# Charts SRP — the `/Charts` reinvention

The generic chart browser, rebuilt as a search-results page (SRP) over the canonical chart
pages (the VDP). Round-1 workshop 2026-07-18; mocks live as a private artifact (workshop-notes
toggle). This doc is the durable home for the locked decisions, the technical scope by layer,
and the commit order.

## 1. Framing

The current `Pages/Charts.razor` is the site's oldest surface — the web-hosted spreadsheet:
it loads four full tables into every circuit (all charts, all videos, all ratings, all score
aggregates), filters in memory per keystroke, exposes 13 column toggles and 7 filter toggles
as UI, edits scores through inline cells, and links to **zero** chart pages. The redesign
flips the model:

- **`/Charts` = SRP** — full-inventory faceted search. Every chart findable through any
  filter combination; every result is one link to its VDP (`/Charts/{mix}/{song}/{difficulty}`).
- **Other pages are the curated shelves** — tier lists (opinionated per-level), popularity,
  suggested charts, randomizer. The SRP answers the queries no shelf does
  (*level-21 doubles with staggered brackets under 140 BPM that I haven't passed*).
- **Dialogs die. Inline editing dies.** A card does three things: navigate, save
  (favorite/to-do), quick-record. Everything deeper lives on the VDP.

## 2. Locked decisions (workshop, owner-confirmed)

| Decision | Ruling |
|---|---|
| Scope | **The selected mix, and only that mix.** One card per chart. A cross-mix view was built and then deliberately removed (see §7) — it needs its own design pass, not a scope flag bolted onto this page. |
| VDP link target | The chart's page in the mix being searched. My-score overlay shows that mix's best. |
| Skills facet | **Granular piucenter badge vocabulary** (`top3:` metrics: `staggered_bracket`, `twist_over90`, `anchor_run`, `yog_walk`, `hands`, …). NOT the rollup `Skill` enum (the high-level generic tags — slated to retire), NOT `SkillCategory` buckets. **Highlighted-only matching** = badge present in the chart's top-3 dominance summary; contains-level badges neither render nor match. |
| NPS | The banked piucenter `nps` metric ONLY — never `NoteCount / Duration` (holds inflate perfects). Unmatched charts silently drop from NPS filter/sort. |
| Tier facet source | Splits by scoring family: **Phoenix / Phoenix 2 = Pass Difficulty / Score Difficulty** (score-derived); **XX and older = Community Vote** (the voted list — always called exactly that, everywhere). There is deliberately no single "legacy" flag — each feature keeps its own boundary (scoring family here, Exceed for rerates, slot era for chips). Community Vote also ships as an export column, populated for XX-and-older rows. Known divergence: today's `/TierLists` serves XX through the score-derived lens set; aligning it is out of scope here. |
| Debut mix | A chart's origin (`OriginalMix`) rides every appearance, so **it filters and sorts inside one mix** — this is the "new content" query and it survives the cross-mix removal. |
| My Score | **Phoenix Score and Legacy Score are separate facet groups**, never compared or blended. XX letter grades behave as clear-quality (plate-like). Only the family matching the searched mix is offered. |
| Score state | Unplayed / Played / Passed / Failed as **multi-select chips** ("Unplayed or Failed" = everything you haven't beaten), plus per-family grade ≥ / plate ≥ / score range / recorded date. |
| Popularity | Site score counts for the searched mix drive the sort; official mirror rank renders as a badge when present (display-only, never a filter). |
| Default sort | **Level descending**; within-level tiebreak = **Scoring Level** (score-derived) in a Phoenix-family mix, Community Vote average in XX and older. Vote data never orders modern-mix results, tiebreaks included. |
| Sorts | Level · scoring level · popularity · **pass difficulty** · newest content (debut era) · name · BPM · NPS · duration · my grade · my recent (Community Vote replaces scoring level as the difficulty sort in XX and older). The difficulty sort is the community *lens*, never the raw rate — the ramp already encodes "hard for its level", where a rate needs a sample floor to mean anything. (This row said "pass rate" through round 5; the code never did.) |
| Card display | Fixed core card + a small curated **Display switch set** in Comfortable (the tier-lists idiom: step artist, duration, note count). **The active sort key always auto-surfaces on the card/table and cannot be hidden** — sorting by an invisible value is impossible by construction; the sort menu never greys out. Table shows the full fact set with the sort column highlighted. No column pickers anywhere on the page. |
| Export | Toolbar **⤓ Export** button → dialog: column picker over the full inventory (this is where column freedom lives), downloading a CSV of the **entire filtered set** via an endpoint reusing the page's query-string contract. Column picks persist as a UiSetting; My columns signed-in only *and mix-conditional* (§8); stable English headers in registry order (convenience surface, outside the versioned `api/*` contract), except the `pc:` passthrough which is deliberately unstable (§8); Excel formula-injection hygiene on values. |
| Dropped | Has-video, recently-added (needs version/date backfill the owner defers), letter-grade percentiles, single-select level (→ range), the `/{userId}/Charts` share view, UCS (separate rethink). |
| Nulls | Facets with gappy coverage (NPS, badges, BPM on legacy) silently exclude unmatched charts. |
| Rendering | Interactive circuit page. Load state from the query string, filter live without reloads, write state back via the history interop (the PR #164 pattern — no programmatic `NavigateTo` for filter state). SSR/SEO facets are explicitly not v1. |
| Density | Comfortable / Compact / Table via `Density__Charts`. **Compact = the jacket sticker sheet** (tier-list idiom — mini art tiles, intentionally low information, never rows). Table = view-only; the column toggles do not return. |
| Quick record | Reuse the Quick Record widget's record body (chart pre-filled, family fork preserved). Entry point = the card's ✎ only; unships cleanly if it reads as bloat. |
| Nav | Desktop top-nav promotion. Mobile stays in More — tier lists own mobile discovery. |
| Default landing | Unfiltered = the catalog, Level ↓. |

"Legacy Difficulty" is the user-facing name for the pre-Exceed slot filter (matches the
`--slot-*` chip vocabulary in UX-GUIDELINES). It appears only when the mix in view actually
has slotted charts — asked of the facet counts rather than guessed from the mix, so a
backfilled catalogue answers for itself.

### Vocabulary (pinned — UI copy, l10n keys, and this doc use these names exactly)

| Name | What it is | Internal source |
|---|---|---|
| **Community Vote** | The voted difficulty list — the only difficulty signal for XX and older. Never abbreviated, never "Community" alone, never "Difficulty". | Tier-list rows named `Difficulty` (storage name stays; display name never uses it) |
| **Pass Difficulty** | Tier bucket (Overrated → Underrated): how hard the chart is to *pass* relative to its level, from weighted recorded passes. Modern mixes. | `Pass Count` tier list |
| **Score Difficulty** | Tier bucket: how hard it is to *score well* on, relative to its level. Modern mixes. | `Scores` tier list |
| ~~**Pass Rate**~~ | **Removed from this page entirely (§8).** A raw percentage relative to nothing, on a self-selected population — it looked authoritative and meant nothing, which is how a page loses a player's trust. Card fact, table column, drawer filter, export column and `SearchChartsQuery.PassRateMin` all gone; `ChartSearchResult.PassCount` went with them. The `Pass rate` l10n key **stays** — `StaminaTournament.razor` still uses it. | — |
| **Scoring Level** | Score-derived decimal difficulty estimate; the Phoenix-family difficulty sort and the default-sort tiebreak. | `IChartScoringLevelRepository` |

### The page stack (locked, toolbar rounds 3–5)

Top to bottom, nothing else between the stack and the results:

1. **Page header** — the page title plus the mix being searched. The mix is page furniture,
   never a filter and never a chip; it follows the global mix selection.
2. **The query (chip row)** — one chip language: every applied filter renders as a small
   clearable chip, and a **non-default sort joins them** as an accent-bordered `⇅` chip.
   Clearing the sort chip returns to Level ↓; the default sort shows no chip. Clear-all
   appears from two chips.
3. **The answer (count line)** — result count on the left; controls on the right in fixed
   order **Export → Comfortable/Compact/Table → Sort → Filters** (funnel icon whose badge
   counts applied filters, sort excluded).
4. Results.

**There is no search input on the page.** "Search charts" was only ever a song-name
string match, so it becomes the **Song name contains** filter in the drawer (contains, not
the old exact match) and appears as an ordinary chip; the app-bar chart search remains the
jump-to-a-specific-chart path. Consequently the drawer is the sole home of every filter —
there are no promoted always-visible facet controls. In Table density the sort chip
retires: the highlighted sorted column header carries the sort and clicking headers
re-sorts; the `⇅` menu stays available.

## 3. Layer scope

### Presentation (`ScoreTracker.Web`)

- **Rebuild `Pages/Charts.razor` in place** — same `/Charts` route, `@rendermode
  RenderModes.Interactive`. The `/{userId}/Charts` route, share dialog, `ChartOverview` +
  `ChartDetailsDialog` usage on this page, inline edit cells, the column/filter toggle
  UiSettings, and the dead vote plumbing (`GetChartRatingsQuery` full-table load,
  uncalled `UpdateDifficultyRating`) are deleted with it.
- **Components** (one concept, one component): the page header (title + mix);
  the **query chip** (one shared component rendering filter chips and the sort chip — the
  page-stack rules above); the count line (count + the four icon controls, funnel badge);
  the filter drawer (the sole home of every filter: grouped per the inventory,
  contextually activated — Community Vote in legacy mixes, Legacy Difficulty when the mix has slotted charts, co-op
  player count with CoOp type — plus the Display switches); the sort menu; a search card
  (Comfortable), the compact **jacket sticker tile** (the tier-list idiom verbatim: art
  tile + bubble + tier dot + grade overlay, chart identity in the tooltip, the owner-locked
  state-border language — solid green passed, dashed blue To-Do), and a table renderer
  (fixed columns, sorted-column highlight, header-click sorting) over the same result
  model. Reuses `SongImage`, `DifficultyBubble` (modern image bubbles; legacy CSS chips),
  `LetterGradeIcon`.
- **URL contract**: every facet and the sort are query-string keys; init
  parses, changes push via history interop. The old page's param names (`Difficulty`,
  `ChartType`, `SongName`, `SongType`, `SongArtist`, `ScoreState`, `SavedCharts`) are
  honored as read-time aliases so shared links keep working (`SongName`'s exact match maps
  onto the contains filter); the page emits only the new names.
- **Export**: the dialog (grouped column picker + filename preview) and a
  UI-support controller endpoint (house pattern: culture/sitemap — not under `api/*`)
  that accepts the page's query-string filters plus `columns`, dispatches the
  unpaged search, and streams the CSV. Anonymous callers get content/community columns;
  My columns require the signed-in user.
- **Quick record**: extract the record body of
  `Components/HomeWidgets/QuickRecordWidget.razor` into a shared `RecordScoreForm`
  component consumed by both the widget (behavior unchanged) and the SRP's ✎ dialog.
  Save updates the card overlay in place.
- **Nav**: `ShellModelFactory` / top-nav markup gains the desktop entry; mobile nav untouched.
- **Localization**: every string through `L[…]`; all nine locales in the same pass, per the
  locale glossaries. The granular badge display names are new key volume (~30 keys).
- **Tests**: bUnit (`Tests.Components`) for filter-bar dispatch, card states (families,
  passed/To-Do borders, unplayed), drawer, densities, `RecordScoreForm` under both
  consumers. One Playwright E2E fact for the URL round-trip + card → VDP navigation —
  history-interop territory bUnit can't observe.

### Application (`ScoreTracker.Catalog`)

Catalog owns the search — it is the content-reads vertical, and the reference math only
works from here: ChartIntelligence already references Catalog (verdict handler sends
Catalog queries), so Catalog must never reference ChartIntelligence; everything the search
needs from other verticals is reachable through Domain ports. **Zero new project
references.**

- **`Contracts/Queries/SearchChartsQuery.cs`** (+ result records): the mix, plus filters
  (text, level range, types, co-op player count, song type, artist, step artist,
  BPM/NPS/duration/note ranges, badges, debut mixes, legacy slots, tier categories,
  pass-rate floor, score states + per-family score facets, recorded-date range,
  `RestrictToChartIds`), sort + direction, page + size. Returns each chart with its
  community and personal overlays, and a total count (+ enum facet counts).
- **Handler pipeline** (`Application/`):
  1. Content facets against the mix's cached chart dictionary plus the banked
     `top3:`/`nps` metrics.
  2. Community facets via Domain ports: `ITierListRepository` — `Pass Count`/`Scores`
     entries (Pass/Score Difficulty) for Phoenix-family mixes, `Difficulty` entries
     (**Community Vote**) for XX-and-older;
     `IChartScoringLevelRepository`; `IScoreReader.GetChartScoreAggregates` (popularity,
     PG — the pass half of the aggregate is no longer projected, §8).
  3. User facets when signed in: `IScoreReader` best-scores per family; saved-lists arrive
     from the page as `RestrictToChartIds` (Catalog stays agnostic of list storage).
  4. Sort, tiebreak, page slice. An unpaged path serves the CSV export — bounded by
     catalog size (~4–5k rows per mix), so no streaming gymnastics needed.
- **Caching**: community-wide dictionaries (per mix: aggregates, tier entries, scoring
  levels, badge index) in `IMemoryCache`, expiring after the nightly analytics chain like
  `ChartVerdictHandler` (13:00 UTC). User reads are per-request, never cached cross-user.
  Scale check: ~4–5k charts per mix — in-memory composition over cached dictionaries is
  comfortably within budget; the structural win is that the *circuit* stops holding four
  full tables.
- **Official ranks** are page-side enrichment: the page sends the OfficialMirror contract
  query for the returned page of ids. Keeps Catalog free of OfficialMirror types; ranks are
  display-only.

### Domain

- **`ScoreTracker.Domain` / SharedKernel: no changes.** `IScoreReader.GetChartScoreAggregates`
  already exists; query params ride existing value types.
- **Catalog `Domain/` (internal)**: the badge display-name catalog (piucenter key → English
  display, Title-cased fallback for unknown keys so new vocabulary degrades gracefully; UI
  layer localizes) and family classification helpers (delegating to `UsesLegacyScoring`).
  A span/rerate calculator over a chart's appearances lived here until the cross-mix
  removal took it (§7); rebuild it there when All Mixes gets its design pass.
- **Unit tests** (`DomainTests/`): badge naming fallback.

### Infrastructure (Catalog `Infrastructure/`)

- Extended internal repositories: badge reads (`top3:` names per chart; distinct-name
  enumeration for the facet cloud) and `nps` reads. Charts themselves come from the
  existing per-mix cached read.
- **No new tables; no expected migrations.** `ChartMix` is already indexed on
  MixId/Level/ChartId. If profiling shows the metrics table needs a `MetricName`-prefix
  index, it lands as a standard scaffolded migration.
- Integration tests (`Tests.Integration`): metric reads against real
  SQL.

## 4. Commit order

Each commit leaves all fast suites green; integration/E2E green at their touchpoints.

| # | Commit | Layer |
|---|---|---|
| C1 | This design doc | docs |
| C2 | Catalog contracts + domain: `SearchChartsQuery`/results, badge display catalog + unit tests (the span+rerate calculators landed here and left with §7) | Application/Domain |
| C3 | Catalog infrastructure: badge/nps reads + integration tests | Infrastructure |
| C4 | Search handler v1: content facets, paging, sorts, cached community dictionaries (tier source fork incl. votes-for-legacy) + component tests | Application |
| C5 | User facets: per-family score facets, `RestrictToChartIds` + component tests | Application |
| C6 | Page skeleton: rebuilt `/Charts` on the locked stack (header, query chips, count line + icon controls), Comfortable cards, paging, sort menu, minimal drawer (level range, type, score state, song-name-contains), URL contract with old-name aliases; old page internals deleted + bUnit | Presentation |
| C7 | (built then removed — see §7) |
| C8 | Full drawer: complete facet inventory, contextual activation, enum facet counts, Display switches + bUnit | Presentation |
| C9 | Export: CSV endpoint reusing the query-string contract + dialog (column picker, UiSetting persistence, hygiene) + bUnit | Presentation |
| C10 | Densities: Compact sticker sheet + Table (header-click sorting, sorted-column highlight, sort-chip suppression) + `Density__Charts` persistence + bUnit | Presentation |
| C11 | Quick record: `RecordScoreForm` extraction (widget pinned unchanged), ✎ dialog, in-place overlay update + bUnit both consumers | Presentation |
| C12 | Official-rank badges (page-side), desktop nav promotion | Presentation |
| C13 | Localization ×9 for all new keys incl. badge display names | Presentation |
| C14 | E2E fact (URL round-trip + card → VDP) + docs sweep (ARCHITECTURE page table; UX-GUIDELINES if reviewers judge the card a new shared pattern) | tests/docs |

No new scheduled jobs, no migrations expected, no post-deploy owner presses.

## 5. Risks and open items

- **Badge coverage**: piucenter matched ~4,337 modern charts; legacy content has no badges
  or NPS. Facet UX must read as absence, not zero (nulls silently excluded — locked).
- **Facet counts**: enum facets only, computed from the cached dictionaries; free-text
  facets get no counts (that's where count cost hides).
- **Old-URL aliases**: read-time only; if a legacy param combination has no new-model
  equivalent it is dropped silently.
- **Export headers**: stable English by design (community tools will parse them), but the
  endpoint is a convenience surface — explicitly outside the `Tests.Api` wire contract.
  Values are formula-injection escaped (`=`, `+`, `-`, `@` starts). The `pc:` group added in
  §8 is the one deliberate exception and says so in the dialog.
- **`/TierLists` XX divergence**: the SRP will show XX tiers vote-sourced while the tier
  page still runs XX through score-derived lenses. Owner decides separately whether to
  align the page.
- **Funnel-only filtering** (no promoted facet controls) trades a click of discoverability
  for the clean stack — an accepted owner call. If analytics later show filter usage
  cratering, the first lever is default-open drawer on first visit, not new chrome.
- **`GetChartsQuery` consumers elsewhere** (randomizer, upload pages, admin) are untouched —
  the SRP query is additive; the old page's four full-table loads die with the page.

## 6. Field-test fixes

### Round 1

- **Compact rendered as slivers** — the tier lists' compact tiles take their width from
  `.tier-card-grid.tier-card-grid-compact`; the sheet had wrapped them in a plain flex row.
  The sheet reuses that grid outright now, and a bUnit fact pins the container.
- **Legacy chart pages 404'd** — `ChartDetails` resolved the URL, then re-fetched the chart
  in the *viewer's* mix and `NotFound()` when it wasn't there, so every chart the current
  mix had dropped 404'd. It now renders the appearance the URL names. Proven by running the
  new E2E fact against the pre-fix commit (404) and the fix (renders).
- **Canonical for a dropped chart is its debut mix** (owner ruling): the default mix's copy
  when it still lives there, else the copy in its `OriginalMix`. The sitemap lists every
  legacy-only chart's canonical once, at that debut appearance, so the whole back catalogue
  is crawlable. This reverses the rejected-alternatives note in
  [chart-details-overhaul.md](chart-details-overhaul.md), which reasoned about charts that
  *do* exist in the current mix — a case this rule doesn't touch.

### Round 2 — the drawer (owner-locked)

- **Basics first, long tail behind "More filters"** (persisted; auto-opened when a URL
  arrives carrying an advanced filter) so chips never flood the panel. Drawer widened to
  420px to suit them. *Round 3 replaced the expand/collapse with a pick list — same intent,
  see below.*
- **Chips wherever a facet is a set of toggles**: chart type — spelled out, *Single /
  Double / Single Performance*, never `S`/`D` — song type, score state, legacy difficulty,
  saved lists, and a co-op player-count row that appears only when Co-Op is picked. Score
  state going multi is a capability gain: "Unplayed or Failed" is everything you haven't
  beaten, which one select could not say.
- **Sliders replace paired numeric fields** *(shipped inert — see round 3)*: level, BPM, NPS, note count, duration, pass
  rate, scoring level, Phoenix score, and the tier lists — which work because
  `TierListCategory` is an ordered scale, so Overrated-to-Underrated is a real range. Grade
  and plate become ordinal sliders too, turning "at least" into a range. Extents come from
  the mix's own catalogue via `GetSearchRangesQuery` rather than guessed spans; an untouched
  slider means no filter at all, so nulls are never quietly cut.
- **Facet counts ride every chip facet, computed with that facet's own filter lifted.**
  Counted against the filtered set they would all read 0 the moment you picked a value,
  which reads as a broken page rather than a live facet.
- **Debut mix groups into eras** (Pro, Pro 2 and Infinity are their own line).
- **Multi-selects are all any-of for now**; the AND/OR toggle is deferred (owner: "do 'Any'
  for now, which I THINK is what's expected").
- **Display switches stay switches** — they change what a card shows, not which charts match.
  *(Round 4 deleted them: step artist just shows, and note-count display went with them.)*

### Round 3 — dead sliders, a pick list, and touch targets

- **Every range facet was inert.** They were written as `<MudRangeSlider>`, which MudBlazor
  8.15 does not have — it ships `MudSlider` and no range variant. An unknown component name
  is Razor warning **RZ10012**, not an error: the tag renders as literal HTML, so all ten
  (level, BPM, NPS, note count, duration, the three tier ramps, scoring level, Phoenix
  score) looked like controls and did nothing, and their attributes were never even
  type-checked. Three defences now: they use the site's real `RangeSlider`; **RZ10012 is a
  build error** in `ScoreTracker.Web.csproj`; and the bUnit fact asserts real
  `input[type=range]` elements that move the query, which markup-text assertions could not.
- **`LevelRangeSlider` → `RangeSlider`.** It was never level-specific — already used for
  co-op player counts and similarity dimensions, and its CSS was always `range-slider`. It
  gains `Step` (a 3,000-note span should not be a pixel hunt) and `ValueText` (a filter
  spanning its whole extent reads better as "Any" than as both ends recited).
- **More filters is a pick list, not a disclosure.** A multi-select names the long-tail
  filters; the ones you check are the ones your drawer keeps, persisted as
  `Charts__ShownFilters`. **Unchecking a filter clears it** — otherwise results stay
  narrowed by a control that is no longer on screen and Clear all is the only escape. A URL
  carrying a filter shows that filter whether or not it was picked, for the same reason.
  Display switches are not filters, so they stayed out of the list — and round 4 removed them
  entirely.
- **Drawer vocabulary loads whenever the drawer opens**, not when the long tail is revealed:
  the pick list needs the extents to know which range filters this mix can offer at all. The
  loaded flag is set *after* the reads succeed, so a failure part-way retries instead of
  leaving the drawer permanently half-built.
- **Legacy grades wear letter art.** An XX grade printed as bare text with `⨯` for broken
  read as "D x" and meant nothing. `LetterGradeIcon` takes a `LegacyGrade` and draws the
  Phoenix art for the same letter — every letter XX uses (F…SSS) exists in that set, so the
  borrow is exact. One component, one parameter: when the XX letter art is drawn it is the
  only place that has to learn about it. Card, sticker and table row all route through it.
  This does not cut against [legacy-mixes.md](legacy-mixes.md), which bars legacy
  *difficulty* from borrowing the modern ramp: that doc calls grades "the stable currency"
  precisely because the F→SSS ladder does span eras, which is what makes the letters shareable
  when the levels are not.
- **The official badge names its measure.** "#12 official" never said what 12 ranked. It is
  piugame's play ranking, and this page *also* sorts by Popularity — which counts scores
  recorded here — so the two were indistinguishable. It reads "#12 most played" with the
  source in its title.
- **Chips are touch targets.** Filter chips and skill tags were ~22px, under half a thumb;
  they sit at 40px now, applied-filter tokens at 36px with a 28px ✕. The skill tag borrows
  the tier list's card chip, so only the SRP's interactive copy is resized — the display tag
  is untouched.

### Round 4 — release-to-commit sliders, and what the drawer is for

- **A range slider publishes on release, not per step.** Level is nineteen steps and felt
  fine; BPM is two hundred, and each one re-ran the search, so the thumb fought the results.
  The fill and readout still follow the drag — and the readout stops deferring to the
  caller's `ValueText` mid-drag, which would otherwise sit frozen on "Any" while the thumb
  moved. `RangeSliderTests` pins both halves: input moves the readout and publishes nothing,
  change publishes once.
- **The Display switches are gone.** Step artist is part of what a chart is, so it just
  shows; the note-count toggle went with it, and with it the whole Display section.
- **Scoring level is a filter wherever it is a sort.** It was always in `SortOrder` but the
  filter was gated on banked extents, so a mix whose analytics had not run could be ordered
  by it and not filtered by it. The track falls back to the level scale, which scoring level
  is expressed on anyway.
- **Both artist facets read as multi-value.** The type-ahead stays the finder — a mix has
  hundreds of artists and a plain dropdown is a scroll-hunt with no search — and the picks
  sit beside it as chips you drop one at a time.
- **The pick list locks the page while open** (`LockScroll`, 360px max height): the list is
  long enough to scroll itself, and without the lock a wheel over it drove the page behind.
  A Playwright fact measures the gap between list and input across a page wheel and a
  drawer-content scroll; neither drifts, and the page behind cannot move at all while the
  drawer is open — MudDrawer already locks it.

### Round 4 — the card opens a dialog

- **Clicking a result opens `ChartDetailsDialog`, not the chart page.** A searcher usually
  wants a look, not a page load, and losing the result list to a navigation is the expensive
  part. The card and the compact sticker stay real `<a href>` elements with the plain left
  click intercepted, so the status bar previews the destination and right-click → open in new
  tab still reaches the chart page. The dialog is only rendered once something has been
  opened — it pulls its own dependencies, and a search that never opens one should not pay.
- **Popularity moved into the dialog as two facts**: place on piugame's play ranking for the
  whole mix, and place within the chart's own folder — a chart four hundredth overall can
  still be the most played thing in its folder, which one number could not say. The card's
  `#12 most played` badge is gone. Folder membership comes from one unpaged folder-scoped
  search, memoized per folder, because the official board knows chart ids and places but
  nothing about type or level, and OfficialMirror cannot see Catalog.
- **The dialog gained a "More info" link** to the canonical chart page — it is a summary, and
  the page is the whole record. Every dialog consumer gets it.
- **The favourite icon is gone from the card** (owner: the feature is not built out). The
  drawer's Favorites filter stays — it still filters lists saved elsewhere.
- **Skills**: the SRP's chips were already the granular piucenter badges; they lose their
  category tint, and the coverage bars on the chart page and dialog move to granular badges
  too. See [nuke-old-skill-categories.md](nuke-old-skill-categories.md).
- **The page stops side-scrolling on a phone**, from three independent causes — measured on
  the live page at 390px, where the document was 498px wide:
  - **Comfortable, 124px of it**: a grid item's automatic minimum size is its *min-content*
    width, and `grid-template-columns: 1fr` is `minmax(auto, 1fr)` — so the one-column phone
    track was floored at the widest card in the result set. One long song title ("Extreme
    Music School 2nd period feat. Nanahira", 482px min-content, `nowrap` on
    `.srp-card-song`) stretched every other card to match and pushed the page sideways,
    which is why it read as intermittent: it depends on a long title landing on the page.
    `.srp-card` gets `min-width: 0` and the mobile track becomes `minmax(0, 1fr)` — the
    first also covers desktop, where an over-wide card would spill out of its own
    `minmax(330px, 1fr)` track. Letting the card shrink is what lets the title's ellipsis
    work at all. Compact/sticker density was never exposed: its tracks carry an explicit
    96px floor rather than `auto`.
  - **12px more**: the answer line's six controls plus the count are wider than the
    viewport, so they travel as one right-aligned block (`.srp-answer-controls`) on a row
    that wraps.
  - **Table**: it borrowed the tier lists' `.tier-table` classes, which lived inside
    `ChartSkills.razor`'s `<style>` block and therefore did not exist on this page at all —
    the table rendered unstyled and took the document sideways with it. Those base rules
    moved to `site.css`, where both pages read them, and the wrap is what scrolls.
- **The filter drawer closes from its foot**: the drawer is taller than a phone screen, so
  the header's ✕ has scrolled out of reach by the time the picking is done.

## 7. Cross-mix: built, removed, deferred

A cross-mix ("All Mixes") view shipped in C7 and was **removed before the branch landed**
(owner call, 2026-07-21). Recording it here because the reasons constrain the redesign:

- **It was slow in the way that matters.** Every request re-grouped ~25k `ChartMix` rows
  into ~15k identities before filtering — so pagination and each filter change paid the
  regroup, not just the first load. The fix (a cached identity index) is real work and was
  being designed as an emergency perf patch rather than as a model.
- **It needed UI decisions nobody had made**: which mix a quick record writes to when a
  chart spans several; whether "Unplayed" means *this mix* or *never played at all*;
  whether a my-score overlay may ever show a best from another mix (it may not — scoring
  families are incomparable); how a legacy grade and a 1M Phoenix score sit side by side.
- **The model wants naming first.** Today's `Chart` is a denormalized join of the `Chart`
  (identity) and `ChartMix` (appearance) tables. Cross-mix work needs the identity named —
  the working name is **`ChartLineage`**, holding a `Chart` template plus its appearances
  and *materializing plain `Chart`s* (`In(mix)`, `Canonical`) so existing leaf components
  keep taking `Chart` and only page shells learn the new type. `Chart` stays the appearance:
  it is the dominant, correct view (you record a score on a chart-in-a-mix) and ubiquitously
  what players call a chart.

What survived the removal, because it is single-mix meaningful: the **debut-mix** facet and
the newest-content sort (`OriginalMix` is a per-chart fact), and **Legacy Difficulty**
(slots are per-appearance, and a legacy mix can be the selected mix). What went: the scope
toggle, available-in / not-available-in, rerate filters and Level Change, the re-clear gap,
the identity span line, the cross-mix pass marker, and the export's per-mix row shape.

The twelve l10n keys the removal orphaned (`Mixes`, `Available in`, `Not available in`,
`Rerated up`/`down`, `Level change`, `In`, `Not in`, `Shape`, `Group by chart`, `One row per
chart and mix`, and the shape-toggle helper paragraph) stay translated in all nine locales —
they are the exact vocabulary the redesign will want back, and re-translating them ×9 costs
more than an unreferenced key does.

When cross-mix returns it gets a full mock from the start, not a flag on this page.

## 8. Export column set — second pass (workshop, owner-confirmed)

The C9 export shipped seven default columns over a 28-column inventory. This pass adds what
the data already holds, deletes what never should have been there, and opens a passthrough
for piucenter's raw metrics. **No migration, no new table, no new entity, no new Domain
port** — every column here reads something that already exists.

### What lands

| Column | Group | Scope | Source |
|---|---|---|---|
| `Pumbility` | My | Phoenix + Phoenix 2 | `ScoringConfiguration.PumbilityScoring(mix, includeCoOp).GetScore(...)` on the row — pure, no read. `N2` in the export, which is presentation and therefore the one layer allowed to round (`PumbilityPrecisionTests` scans every project except Web) |
| `MyPerfects/Greats/Goods/Bads/Misses` | My | Phoenix family | already hydrated by `GetBestScores`; the search handler was dropping them in `MyRecord` |
| `MyMaxCombo` | My | Phoenix family | derived — see the gate below |
| `MyPlayCount` | My | **Phoenix 2 only** | new ScoreLedger query over the journal |
| `ChartId` | Chart | all | `Chart.Id`. The export exists so tools can parse it and had no join key; song+type+level is not stable across renames |
| `ChartUrl` | Chart | all | `ChartSlugs.CanonicalPath` + request base |
| `PlayerCount` | Chart | all | `Chart.PlayerCount` — a drawer facet that was never exportable |
| six `pc:` bundles | piucenter | all | banked `ChartSkillMetric` rows, already loaded and cached by the handler |

**CO-OP in PUMBILITY**: `includeCoOp: true` on Phoenix 1, where CO-OP charts genuinely
score. Phoenix 2 needs no argument — `Phoenix2PumbilityScoring` zeroes CO-OP itself, and
nothing on the official site prices a CO-OP chart there.

### `MyMaxCombo` — the gate

Combo is the only unknown in the Phoenix formula once the five judgement counts and the
score are known, so it inverts:

```
MaxCombo = 200 × (score × T / 1e6 − .995 × (P + .6G + .2Go + .1B))     where T = P+G+Go+B+M
```

**It derives only when `T` equals the chart's stored note count. Otherwise the column is
null.** The inversion needs the denominator the game scored against; a judgement set that
does not cover the whole chart is not that denominator, and a stage break is the case where
it most obviously isn't. Measured against production (2026-08-10): all 6,066 non-broken
Phoenix 2 records carrying judgements satisfy the gate, and on full-combo rows — where the
answer is known in advance, since `Goods=Bads=Misses=0` implies combo equals note count —
the inversion lands (3,333 → 3333.00; worst of twenty sampled, a 3,500-note chart, 3499.60).
Note the two known-short Phoenix note counts (`Simon Says, EURODANCE!!` S20, `Over the
Horizon` S20 — stored counts that predate a re-step) fail the gate and correctly return null
rather than a wrong number.

The solver lives in `Domain/Services/`, **not** beside `ScoreScreen` in `Domain/Records/`,
because Records is a coverage-excluded folder and a formula inversion is exactly what
coverage is for. Same assembly as the forward formula so a round-trip test pins the pair —
the reasoning `ScoringConfiguration.Decompose` already uses.

The site never reports max combo (the recently-played card carries five `data-th` cells:
PERFECT/GREAT/GOOD/BAD/MISS, and the best-score page carries none), so capturing it at
ingestion instead is a separate, later change. This solver is what that change would reuse.

### `MyPlayCount` — why Phoenix 2 only

The journal shipped 2026-06-12; Phoenix 2's first import was 2026-07-11. **Phoenix 2 has no
backfill rows** — its sources are `officialImport` / `manual` / `csv` only, so the journal
is a genuine gap-free personal play log. Phoenix 1 is the opposite: 939,395 of its 1.09M
rows are `backfill`, one per record dated at the record's last update, so a count there
reads 1 for a chart played two hundred times. That number would be worse than no column.

Honest even where it ships: this counts plays *we observed* — best-list changes plus
whatever the recently-played window caught. 91% of Phoenix 2 user-chart pairs sit at exactly
1 today. That is a month-old mix, not a defect, and the column gets more interesting monthly.

### Judgement density (why five columns earn their width)

| Mix | Records | With judgements |
|---|---|---|
| Phoenix 2 | 11,923 | 6,678 (**56%**; 71% of officially-imported records; 80–98% per active importer) |
| Phoenix 1 | 1,051,226 | 10,867 (1.0%) |

Structural, not accidental: capture landed 2026-07-17 and judgements are written only when
the record *changes* (`UpdatePhoenixRecordHandler` — the previous play's counts describe the
old result and would be a lie on the new one). Phoenix 2 records were nearly all created
through the capturing path; Phoenix 1's mostly predate it and fill in as people upscore.
Manual and CSV entries carry none by construction. Columns ship on both mixes and are null
where absent.

### The piucenter passthrough

136 metric names over 4,411 charts. `data_version` is bookkeeping and never ships; `nps`
already has its own column. The remaining 134 group into six checkboxes:

| Checkbox | Columns | Family |
|---|---|---|
| Chart analysis | 4 | `difficulty_prediction`, `sustain_time`, `time_under_tension`, `last_segment_is_peak` |
| Skill emphasis | 29 | `badge_fraction:*` |
| Top-3 skills | 29 | `top3:*` — the same data the `Badges` column renders, as a matrix |
| Practice ranks | 32 | `practice_rank:*` |
| Chart ending | 29 | `last_segment_badge:*` |
| Rare patterns | 11 | `rare:*` |

Rules: headers carry a `pc:` prefix; ticking a family emits **every name in that family**
whether or not the current filter contains a chart holding it, so the header set does not
shift between two exports of different filters; and the group is labelled unstable **in the
dialog**, because it is a third-party passthrough and the promise the rest of the file makes
does not extend to it.

**`difficulty_prediction` is not a difficulty projection and is not Scoring Level.** It is
piucenter's own `page-content/tierlists.json` — folder → NPS-cluster → predicted value —
and it behaves like a within-folder refinement of the *printed* level, never leaving it by
more than +0.5 when averaged per folder (S1 → 1.28, S17 → 17.28, S25 → 25.27). Our Scoring
Level is score-derived and roams: across the 3,616 Phoenix charts holding both, mean absolute
gap 1.40 levels, 31% more than two levels apart (*Pump me Amadeus* S13 — piucenter 12.26,
ours 16.84). They measure different things. It stays inside the passthrough, keeps its
`pc:` prefix, and **nothing calls it a projection**. (It already ships publicly as
`DifficultyPrediction` on the api/v2 catalog DTO; that name is spent.)

### Dialog

37 single columns plus 6 bundles does not fit the flat chip rows C9 shipped. Changes:

- **An "All *n*" per group** — with 17 chart columns and 13 personal ones, ticking one at a
  time is the common case going wrong.
- **Bundle chips print a multiplier** — `×29` — because one tap is not one column: ticking
  Practice ranks moves the footer from 6 columns to 38. They wear the **ordinary chip**
  otherwise. A dashed edge was tried and cut (owner, field test): the multiplier already
  says it, and a second visual language for one group cost more than it explained.
- **My columns is mix-conditional** — 13 on Phoenix 2, 12 on Phoenix 1, 4 on XX and older.
  This also fixes an existing wart: the group currently offers *My legacy grade* on Phoenix
  and *My Phoenix score* on XX and returns them blank.
- The unstable note lives in the piucenter group, not in this doc alone.

**Chips stay loosely organized** — one wrapping row per group, each chip sized by its own
label. A two-column grid was built and cut (owner, field test): it lined the labels up, and
lost the scannability of a ragged row whose widths tell you how many options are short ones.
The group headers are what made the long inventory navigable, not the alignment.

(`charts.scss` line 1 sets `.mud-dialog-width-sm { max-width: none !important; }`, so this
dialog has never actually been `MaxWidth.Small` — the room is there if a future pass wants it.)

### Layer scope

| Layer | Change |
|---|---|
| SharedKernel | none — `PumbilityScoring` already exists and Web already calls it |
| Domain | one file: the combo solver in `Domain/Services/` |
| Application, Data | none |
| Catalog | `ChartSearchMyState` +5 judgement ints (free — the record read already hydrates them); `ChartSearchResult` −`PassCount`; `SearchChartsQuery` −`PassRateMin`; handler loses the pass-rate filter. Plus `GetChartMetricsQuery` / `GetChartMetricNamesQuery` + one handler for the passthrough |
| ScoreLedger | `GetPlayerChartPlayCountsQuery` + handler + one method on the internal `IScoreJournalRepository` + its EF implementation. Covered by the existing `(UserId, MixId, ChartId, OccurredAt)` index |
| Web | the registry, the dialog, the controller, and the pass-rate deletions in `ChartSearchCard` / `Charts.razor` / `ChartSearchUrlParser` |

`ChartExport.Column.Value` grows a context record so play count and the metric map can ride
the export without the page paying for reads it never renders.

**The passthrough deliberately does NOT ride `ChartSearchResult`.** Two Catalog queries carry
it instead — names for the picker's chip sizes, values for the endpoint — because the page
renders that projection on every load and the CSV is the only thing that wants metrics. It
also keeps `ChartSkillMetric` internal, which a contract field would not.

### Commit order

| # | Commit | Layer |
|---|---|---|
| E1 | This doc section | docs |
| E2 | Purge pass rate: card fact, table column, chip, URL param, `PassRateMin`, `MinScoresForPassRate`, `PassCount`, the export column and its `DefaultColumns` entry; delete `PassRateNeedsTheMinimumSample` | Presentation/Catalog |
| E3 | `ChartId`, `ChartUrl`, `PlayerCount` + the `ExportContext` the URL forces | Presentation |
| E4 | Dialog restructure: group headers, All-*n*, mix-conditional My columns (incl. the legacy-blank fix) + bUnit | Presentation |
| E5 | `Pumbility` column + l10n | Presentation |
| E6 | Judgements through `ChartSearchMyState` + five columns + l10n | Catalog/Presentation |
| E7 | Combo solver + round-trip unit test + `MyMaxCombo` column | Domain/Presentation |
| E8 | Play count: ScoreLedger query, handler, repo method, integration test, the column | ScoreLedger/Presentation |
| E9 | piucenter passthrough: two Catalog queries, bundle expansion in `Write`, six chips, the unstable note + l10n | Catalog/Presentation |

The drawer needed no change at E2 — the pass-rate slider was already off the pick list. The
table did: its sortable **Pass Difficulty** header stood over the pass-*rate* cell rather than
over the Tier cell that renders the pass-difficulty category, so the Tier header carries that
sort now and the mislabelling went out with the column.

`piucenter` joined the Murloc ratchet's protected proper nouns. The passthrough is attributed
to the site by name, which is the same standing `PIU Center` already had.

~20 new l10n keys across nine locales, inserted alphabetically, landing with the commit that
introduces them. The `Pass rate` key stays — `StaminaTournament.razor` used it until the March of Murlocs
Slice 4a retired that page (2026-09-05), and a
call-site grep scoped to this page would wrongly call it dead.

### Open

- The gate admits ~500 broken Phoenix 2 records whose judgement sums *do* equal the note
  count. Deriving their combo is arithmetically identical; whether a failed stage's score
  is formula-comparable is the part nobody can check. Currently they derive, because the
  gate is the note-count rule and nothing else.
- `PhoenixComboSolver` is deliberately reusable at ingestion. Capturing max combo on the
  record instead of solving it needs a column and a source that reports one; neither exists
  yet, and the solver is what that change would replace itself with.
