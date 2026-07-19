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
| Result unit | **Grouped chart identity** (`ChartMix` spans mixes): one card per lineage, debut → latest, level history. Mix is a facet. |
| All Mixes | **Page-local scope toggle** (Current mix / All / Custom). Global mix pill and theme untouched. |
| VDP link target | Selected mix when the chart appears there, else its latest mix. My-score overlay shows the linked mix's best + cross-mix marker (dashed-green family). |
| Skills facet | **Granular piucenter badge vocabulary** (`top3:` metrics: `staggered_bracket`, `twist_over90`, `anchor_run`, `yog_walk`, `hands`, …). NOT the rollup `Skill` enum (the high-level generic tags — slated to retire), NOT `SkillCategory` buckets. **Highlighted-only matching** = badge present in the chart's top-3 dominance summary; contains-level badges neither render nor match. |
| NPS | The banked piucenter `nps` metric ONLY — never `NoteCount / Duration` (holds inflate perfects). Unmatched charts silently drop from NPS filter/sort. |
| Tier facet source | Splits by scoring family: **Phoenix / Phoenix 2 = score-derived** (`Pass Count` / `Scores` tier lists); **XX and older = community votes** (`Difficulty` tier list entries). There is deliberately no single "legacy" flag — each feature keeps its own boundary (scoring family here, Exceed for rerates, slot era for chips). Known divergence: today's `/TierLists` serves XX through the score-derived lens set; aligning it is out of scope here. |
| Rerates | `Rerated Up` / `Rerated Down` filters + signed **Level Change** sortable. Computed over **Exceed-onward** appearances only — pre-Exceed levels live on per-era scales. |
| My Score | **Phoenix Score and Legacy Score are separate facet groups**, never compared or blended. XX letter grades behave as clear-quality (plate-like). Each group filters only its own family's appearances. |
| Score state | Unplayed / Played / Passed / Failed, plus per-family grade ≥ / plate ≥ / score range / recorded date. |
| Re-clear gap | *Passed in an in-scope mix, no pass in the target mix* — promoted to prominent placement while the Phoenix 2 transition is hot; demotes to the drawer later. |
| Popularity | Site score counts drive the sort (all mixes); official mirror rank renders as a badge when present (display-only, never a filter). |
| Default sort | **Level descending**, community-difficulty tiebreak within a level. |
| Sorts | Level · community difficulty · popularity · pass rate · newest content (debut era) · level change · name · BPM · NPS · duration · my grade · my recent. |
| Dropped | Has-video, recently-added (needs version/date backfill the owner defers), letter-grade percentiles, single-select level (→ range), the `/{userId}/Charts` share view, UCS (separate rethink). |
| Nulls | Facets with gappy coverage (NPS, badges, BPM on legacy) silently exclude unmatched charts. |
| Rendering | Interactive circuit page. Load state from the query string, filter live without reloads, write state back via the history interop (the PR #164 pattern — no programmatic `NavigateTo` for filter state). SSR/SEO facets are explicitly not v1. |
| Density | Comfortable / Compact / Table via `Density__Charts`. **Compact = the jacket sticker sheet** (tier-list idiom — mini art tiles, intentionally low information, never rows). Table = view-only; the column toggles do not return. |
| Quick record | Reuse the Quick Record widget's record body (chart pre-filled, family fork preserved). Entry point = the card's ✎ only; unships cleanly if it reads as bloat. |
| Nav | Desktop top-nav promotion. Mobile stays in More — tier lists own mobile discovery. |
| Default landing | Unfiltered = the catalog, Level ↓. |

"Legacy Difficulty" is the user-facing name for the pre-Exceed slot filter (matches the
`--slot-*` chip vocabulary in UX-GUIDELINES); it activates only when scope includes
pre-Exceed mixes.

## 3. Layer scope

### Presentation (`ScoreTracker.Web`)

- **Rebuild `Pages/Charts.razor` in place** — same `/Charts` route, `@rendermode
  RenderModes.Interactive`. The `/{userId}/Charts` route, share dialog, `ChartOverview` +
  `ChartDetailsDialog` usage on this page, inline edit cells, the column/filter toggle
  UiSettings, and the dead vote plumbing (`GetChartRatingsQuery` full-table load,
  uncalled `UpdateDifficultyRating`) are deleted with it.
- **Components** (one concept, one component): a search card (Comfortable), the compact
  **jacket sticker tile** (the tier-list idiom verbatim: art tile + bubble + tier dot +
  grade overlay, identity in the tooltip, the owner-locked state-border language — solid
  green passed / dashed blue To-Do / dashed green other-mix), and a table renderer over
  the same result model; the All-Mixes span line
  (debut → latest · n mixes · level change) and re-clear/cut markers; the applied-chip row;
  the filter drawer; the sort menu. Reuses `SongImage`, `DifficultyBubble` (modern image
  bubbles; legacy CSS chips), `LetterGradeIcon`.
- **URL contract**: every facet is a query-string key; init parses, changes push via history
  interop. The old page's param names (`Difficulty`, `ChartType`, `SongName`, `SongType`,
  `SongArtist`, `ScoreState`, `SavedCharts`) are honored as read-time aliases so shared
  links keep working; the page emits only the new names.
- **Quick record**: extract the record body of
  `Components/HomeWidgets/QuickRecordWidget.razor` into a shared `RecordScoreForm`
  component consumed by both the widget (behavior unchanged) and the SRP's ✎ dialog.
  Save updates the card overlay in place.
- **Nav**: `ShellModelFactory` / top-nav markup gains the desktop entry; mobile nav untouched.
- **Localization**: every string through `L[…]`; all nine locales in the same pass, per the
  locale glossaries. The granular badge display names are new key volume (~30 keys).
- **Tests**: bUnit (`Tests.Components`) for filter-bar dispatch, card states (families,
  span/rerate/re-clear, unplayed), drawer, densities, `RecordScoreForm` under both
  consumers. One Playwright E2E fact for the URL round-trip + card → VDP navigation —
  history-interop territory bUnit can't observe.

### Application (`ScoreTracker.Catalog`)

Catalog owns the search — it is the content-reads vertical, and the reference math only
works from here: ChartIntelligence already references Catalog (verdict handler sends
Catalog queries), so Catalog must never reference ChartIntelligence; everything the search
needs from other verticals is reachable through Domain ports. **Zero new project
references.**

- **`Contracts/Queries/SearchChartsQuery.cs`** (+ result records): filters (text, level
  range, types, co-op player count, song type, artist, step artist, BPM/NPS/duration/note
  ranges, badges, mix scope incl. debut / available-in / not-in / rerated, tier categories,
  pass-rate floor, score-state + per-family score facets, re-clear target, recorded-date
  range, `RestrictToChartIds`), sort + direction, page + size. Returns identity groups
  (chart + per-mix appearances + span/rerate data + facet payload for the card) and a
  total count (+ enum facet counts).
- **Handler pipeline** (`Application/`):
  1. Content facets + mix scope against Catalog-owned data (charts, `ChartMix`, songs,
     `top3:`/`nps` metrics).
  2. Community facets via Domain ports: `ITierListRepository` — `Pass Count`/`Scores`
     entries for Phoenix-family mixes, `Difficulty` entries (votes) for XX-and-older;
     `IChartScoringLevelRepository`; `IScoreReader.GetChartScoreAggregates` (popularity,
     pass rate, PG).
  3. User facets when signed in: `IScoreReader` best-scores per family; saved-lists arrive
     from the page as `RestrictToChartIds` (Catalog stays agnostic of list storage).
  4. Sort, tiebreak, page slice.
- **Caching**: community-wide dictionaries (per mix: aggregates, tier entries, scoring
  levels, badge index) in `IMemoryCache`, expiring after the nightly analytics chain like
  `ChartVerdictHandler` (13:00 UTC). User reads are per-request, never cached cross-user.
  Scale check: ~4–5k charts per modern mix, ~25k `ChartMix` rows across all mixes —
  in-memory composition over cached dictionaries is comfortably within budget; the
  structural win is that the *circuit* stops holding four full tables.
- **Official ranks** are page-side enrichment: the page sends the OfficialMirror contract
  query for the returned page of ids. Keeps Catalog free of OfficialMirror types; ranks are
  display-only.

### Domain

- **`ScoreTracker.Domain` / SharedKernel: no changes.** `IScoreReader.GetChartScoreAggregates`
  already exists; query params ride existing value types.
- **Catalog `Domain/` (internal)**: the span/rerate calculator (appearances → debut, latest,
  mix count, Exceed-onward signed level delta; slot-era rows excluded from the math), the
  badge display-name catalog (piucenter key → English display, Title-cased fallback for
  unknown keys so new vocabulary degrades gracefully; UI layer localizes), the
  `ModernScaleStart = Exceed` constant, family classification helpers (delegating to
  `UsesLegacyScoring`).
- **Unit tests** (`DomainTests/`): rerate math (up/down/net, per-era exclusion, single-
  appearance = no rerate), span derivation, badge naming fallback.

### Infrastructure (Catalog `Infrastructure/`)

- Extended internal repositories: identity-grouped reads (chart + all `ChartMix`
  appearances + song in one shape), badge reads (`top3:` names per chart; distinct-name
  enumeration for the facet cloud), `nps` reads.
- **No new tables; no expected migrations.** `ChartMix` is already indexed on
  MixId/Level/ChartId. If profiling shows the metrics table needs a `MetricName`-prefix
  index, it lands as a standard scaffolded migration.
- Integration tests (`Tests.Integration`): identity grouping and metric reads against real
  SQL.

## 4. Commit order

Each commit leaves all fast suites green; integration/E2E green at their touchpoints.

| # | Commit | Layer |
|---|---|---|
| C1 | This design doc | docs |
| C2 | Catalog contracts + domain: `SearchChartsQuery`/results, span+rerate calculators, badge display catalog + unit tests | Application/Domain |
| C3 | Catalog infrastructure: identity-grouped reads, badge/nps reads + integration tests | Infrastructure |
| C4 | Search handler v1: content facets, mix scope, paging, sorts, cached community dictionaries (tier source fork incl. votes-for-legacy) + component tests | Application |
| C5 | User facets: per-family score facets, re-clear gap, `RestrictToChartIds` + component tests | Application |
| C6 | Page skeleton: rebuilt `/Charts`, core bar, Comfortable cards, paging, URL contract with old-name aliases; old page internals deleted + bUnit | Presentation |
| C7 | All Mixes: scope toggle, grouped identity cards (span/rerate/re-clear/cut), linked-mix VDP resolution + bUnit | Presentation |
| C8 | Drawer + sort menu + applied chips + enum facet counts + bUnit | Presentation |
| C9 | Densities: Compact + Table + `Density__Charts` persistence + bUnit | Presentation |
| C10 | Quick record: `RecordScoreForm` extraction (widget pinned unchanged), ✎ dialog, in-place overlay update + bUnit both consumers | Presentation |
| C11 | Official-rank badges (page-side), desktop nav promotion | Presentation |
| C12 | Localization ×9 for all new keys incl. badge display names | Presentation |
| C13 | E2E fact (URL round-trip + card → VDP) + docs sweep (ARCHITECTURE page table; UX-GUIDELINES if reviewers judge the card a new shared pattern) | tests/docs |

No new scheduled jobs, no migrations expected, no post-deploy owner presses.

## 5. Risks and open items

- **Badge coverage**: piucenter matched ~4,337 modern charts; legacy content has no badges
  or NPS. Facet UX must read as absence, not zero (nulls silently excluded — locked).
- **Facet counts**: enum facets only, computed from the cached dictionaries; free-text
  facets get no counts (that's where count cost hides).
- **Old-URL aliases**: read-time only; if a legacy param combination has no new-model
  equivalent it is dropped silently.
- **`/TierLists` XX divergence**: the SRP will show XX tiers vote-sourced while the tier
  page still runs XX through score-derived lenses. Owner decides separately whether to
  align the page.
- **`GetChartsQuery` consumers elsewhere** (randomizer, upload pages, admin) are untouched —
  the SRP query is additive; the old page's four full-table loads die with the page.
