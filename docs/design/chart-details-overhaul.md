# Chart details overhaul — design (Stage 3 of the SSR/islands migration)

Decided in the 2026-07-14 chart-details workshop (owner + Claude); visual mock approved the same
day (round 2 — community glow + honest plate framing). Companion specs:
[chart-similarity.md](chart-similarity.md) (the similarity graph + settled formula) and
[chart-verdicts.md](chart-verdicts.md) (the verdict engine).

**Stage context (owner-approved 2026-07-14, aligned with the shell/hosting session):** the site
moves to SSR-by-default + islands in three stages — Stage 1 the static shell (every page, one
unavoidable site-wide QA), Stage 2 the hosting flip (`AddServerSideBlazor` → Blazor Web App
render modes, shipped `prerender: false` globally, zero intended behavior change; `_Host` and
`_Layout` die), Stage 3 pages one at a time, **this page first**. Page work here depends on
Stages 1–2 being merged forward. **Prerendering stays off permanently — static SSR ≠
prerendering; never conflate them.** (An earlier revision of this doc coded against
static-shell.md §7's Razor-Page-plus-`<component>`-islands shape; that is obsolete and was never
built.)

**Context.** `/Chart/{id}` (`Pages/ChartDetails.razor`) is the pre-overhaul chart surface: no
jacket, no tier placement, raw `#FF0000/#00FF00` graph literals, an uncapped world leaderboard
(9,950px page for a niche chart), double-loading lifecycle, a dead `ChartOverview` dialog — and
for crawlers, nothing at all: `render-mode="Server"` serves a 7.9KB shell with no title, while
the sitemap advertises every chart GUID. Meanwhile `ChartDetailsDialog` (the owner-iterated
shared surface) outgrew the page. This overhaul makes the chart page the site's best SEO/AEO/GEO
surface and the destination worthy of the dialog's vocabulary.

## Goals

1. **Crawlers, Discord, and LLMs receive real HTML**: title, verdict-bearing meta description,
   OG jacket, JSON-LD, canonical — and the full page content server-rendered.
2. **The answer above the fold** (UX rule 1): identity + verdict + your score in the first
   viewport at 390×844.
3. **Historical data becomes first-class**: debut mix + cross-mix level changes (derivable today
   from mix catalogs sharing chart GUIDs — `ChartCompare.razor` proves it).
4. **"Is X harder than Y" gets settled by sentences**, with the graphs as evidence
   ([chart-verdicts.md](chart-verdicts.md)).
5. **Every chart page links onward**: similar-charts shelf ([chart-similarity.md](chart-similarity.md)),
   sibling difficulties of the same song, step artist, tier-list folder — the internal-link mesh.

## URLs and the redirect lattice (settled)

| URL | Behavior |
|---|---|
| `/{default-mix}/{song-slug}/{difficulty}` e.g. `/phoenix/baroque-virus-full-song/d20` | **Canonical** — the site's anonymous default mix; self-canonical; sitemapped |
| `/Chart/{guid}` (and legacy `/Record`) | Permanent identity — 301 → canonical, forever |
| Historical (mix, song, level) triple, e.g. `/xx/…/d19` | Resolved against **that mix's** catalog (the mix segment timestamps the level, so cross-mix renumbering is never ambiguous) → 301 → the chart's canonical |
| Stale/mangled slug | 301 → canonical |
| Within-mix rebalance collision | current holder of the level owns the URL; former holder reachable via GUID |

- Slugs: lowercase, hyphens, strip URL-hostile punctuation, **preserve unicode** (Korean titles
  stay Korean). Song names are unique today; if that breaks, suffix with artist.
- When the site default mix flips (~per mix era), the canonical namespace 301s wholesale and the
  sitemap regenerates — accepted (mass 301 migrations transfer signals).
- Sitemap lists **canonical vanity URLs only** (replacing GUIDs), and ships **in the same PR** as
  the prerendered page — pretty URLs pointing at empty shells would invite a wasted recrawl.
- Rejected: origin-mix-as-canonical (stale search intent; content mismatch). The stable-identity
  job belongs to the GUID permalink.

## Page anatomy (mock R2, approved 2026-07-14)

Static SSR (real server HTML) unless marked **[island]** = a child component with
`@rendermode InteractiveServer`, never prerendered. Section order = answer → evidence → record →
onward.

1. **Hero** — jacket-led (`SongImage` art as a real `<img>`, also the `og:image`; the LCP).
   Click-to-load video overlay on the jacket **[island: video]** — no iframe weight until asked.
   `DifficultyBubble` art overlapping the jacket corner. Identity: song name (h1), song type,
   song artist, step artist (link), **sibling difficulty bubbles** (the song's other charts —
   navigation, deliberately excluded from similarity). Fact strip: BPM, note count, NPS, debut
   mix, scores tracked, pass rate. **Verdict card**: headline sentence + Pass/Score/Plates chips
   (difficulty-ramp colors via `ThemeScales`). Actions: Record score (primary), Watch video,
   PIU Center link-out (attribution pattern kept).
2. **Your best** — server-rendered at origin for signed-in requests (they bypass the cache):
   `ScoreBreakdown` + rarity-ramp percentile chip + score age + the cross-mix score journey
   (`GetChartScoreJourneyQuery`). Beside it the **record row [island: record]** — score / plate /
   broken / save, prefilled from your best (the dialog's round-11 pattern, not `EditChartGrid`).
3. **The evidence** — the four distribution graphs, each **led by its verdict caption**
   (sentence first, curve second): letter-grade percentiles, plate distribution (residual
   framing — see the plate constraint in [chart-verdicts.md](chart-verdicts.md)), score-by-
   competitive-level (min/avg/max + "you" marker — a point, not the old flat-line hack),
   passes-by-level. **[island: graphs]** — Apex, `ApexChartTheming.BaseOptions`, layout-matched
   skeletons until connect (UX rule 9), plate bars in `--plate-*`, no raw literals (burns the
   `UiColorTokenTests` allowlist entries).
4. **Skill fingerprint** — the dialog's mapped-skill bars at page scale (`GetChartSkillChipsQuery`,
   `--skillcat-*` tints, dominance-ordered), NPS/sustain/TUT strip, PIU Center attribution.
5. **History** — timeline band: debut mix → per-mix level chips with deltas (from mix catalogs).
6. **Leaderboard** — top 10 server-rendered + your pinned row, **community glow** in the
   widgets' vocabulary (`.weekly-lb-me`/`.weekly-lb-community` pattern, `--daily-you` blue /
   `--daily-community` green, `CommunityGlowReader` — opt-in communities only). Glow and the
   you-row are personal → origin-rendered for signed-in; the anonymous cached copy has neither.
   "Explore all N" **[island: leaderboard explorer]** — paged, replaces the uncapped dump.
7. **Charts like this** — the similarity shelf: jacket cards, difficulty bubbles, **why-chips**
   from the edge's signal breakdown, match score. Static HTML — this is the link mesh.
8. **Admin** — the BPM/video/step-artist editor leaves the public flow; admin-only "✎" affordance
   opens it **[island: admin edit]**, visible only to admins.

Mobile (390×844): hero stacks jacket-row → verdict → your best inside the first viewport;
**Record score owns the page dock** (`PageDock` contract); no chart selector on the page — the
shell's search covers "find another chart". Leaderboard rows reflow two-line; scores never clip.

## Technical shape (Stage-3 form)

- **The page stays `Pages/ChartDetails.razor`, rebuilt in place**: it drops `@rendermode` →
  static SSR. Interactive regions are child components marked `@rendermode InteractiveServer`
  (the islands). No Razor Page, no `<component>` tag helpers, no `IslandRoot`, no
  `ShellModelFactory` handoff — those were the pre-Stage-2 shape. All dispatch via `IMediator`
  (unchanged rule); no repository injection.
- **Routes**: the component declares only the canonical vanity route. The GUID permalink
  `/Chart/{id}`, legacy `/Record`, and historical (mix, song, level) resolution live in an MVC
  controller issuing **real 301s** — a redirect from a static component is a 302, which doesn't
  consolidate SEO signals. ⚠ Blazor's link interception is a global document click listener:
  an in-app `<a>` to an MVC endpoint renders `<NotFound>` unless it carries `target="_top"` —
  so **in-app chart links build the canonical vanity URL directly** (the slug is deterministic
  from the chart record); the 301 endpoints exist for external/legacy links only.
- **Head**: `PageTitle` + `HeadContent` render server-side under static SSR — title,
  meta-description-from-verdict, OG set, canonical `<link>`, JSON-LD `<script>`
  (`MusicRecording` + `additionalProperty`; exact vocabulary settled at implementation). The
  page's current dead-channel `HeadContent` block retires with the rebuild.
- **Auth/personalization**: `HttpContext` is available during static SSR — "your best" and
  leaderboard glow render server-side for signed-in requests (which bypass response caching);
  the anonymous cached copy contains neither.
- **Token discipline (verified against shipped source by the hosting session)**: every
  `--mud-*` custom property is emitted by `MudThemeProvider` *inside the circuit* — zero
  declarations in `MudBlazor.min.css`. Static regions style from `--mix-*`/semantic tokens
  only, or they FOUC. **Audit the statically-used shared components** (`DifficultyBubble`,
  `ScoreBreakdown`, `UserLabel`, `SongImage`) for `--mud-palette-*`/`--mud-elevation-*` usage
  and migrate to mix tokens. Presentational MudBlazor (`MudText`/`MudIcon`/`MudPaper`/
  `MudAvatar`) is static-safe; behavioral MudBlazor (`MudMenu`/`MudDialog`/`MudSelect`/
  autocomplete/snackbar) only inside islands.
- **Islands never prerender** (owner rule: prerendering stays off, permanently). Their slots are
  empty in the initial HTML, so each island's static wrapper carries a layout-matched CSS
  skeleton (UX rule 9) that the island replaces on circuit connect.
- **Caching**: unchanged intent — the anonymous page opts into the output-cache policy landing
  with Stages 1–2 (vary: path, culture, mix); signed-in renders at origin per request. Query
  results shared with the nightly analytics (verdicts, similarity, letter difficulties) are
  `IMemoryCache`d with TTLs aligned to the recompute cadence.
- **Retirement within the rebuild**: the dead `ChartOverview` dialog, the `TournamentId`
  query-param relic, the admin form's mid-page squat, and the `/Record` route alias all go;
  navigation into charts keeps working everywhere because `/Chart/{guid}` 301s forever.
  `ChartDetailsDialog` stays (widgets' quick-look surface) — shared pieces (skill bars, meta
  grid) extract into components both consume where practical (one concept, one component).
- **Localization**: every new string through `L[…]`, all nine locales in the same pass. Verdict
  templates live Web-side (see [chart-verdicts.md](chart-verdicts.md) — the engine returns
  structured facts; Web renders words).

## Build plan

Backend first (zero dependency on wave 2), page last (needs shell C1/C2). Suites green at each
checkpoint; FT = owner field test.

| # | Commit | Contents |
|---|---|---|
| B1 | Similarity: table + job skeleton | `ChartSimilarity` contribution + migration, `RecalculateChartSimilarityCommand` + consumer + Hangfire line, DATABASE-SCHEMA + SCHEDULED-JOBS rows |
| B2 | Similarity: signals + formula | per-signal scorers + combiner (unit-tested against hand-built fixtures), `GetSimilarChartsQuery` — **calibration artifact: top-K dump for ~20 known charts → owner eyeball, weights adjust by PR** |
| B3 | Verdict engine | facet computers + salience ranking (unit-tested), `GetChartVerdictQuery`, caching |
| B4 | URL lattice (dark) | slug service + historical (mix, song, level) resolver + 301 controller, tested; not yet linked or sitemapped |
| P1 | The page, hero → evidence | `ChartDetails.razor` rebuilt in place: static SSR body + video/record/graphs islands (`@rendermode InteractiveServer`), `--mud-*` audit on statically-used components — needs Stages 1–2 merged forward |
| P2 | History, leaderboard, shelf | timeline, glowing top-10 + explorer island, similarity shelf, sibling bubbles, admin island |
| P3 | SEO cutover | static head/JSON-LD live (old dead-channel `HeadContent` retired), sitemap swaps to vanity, 301 lattice goes public, in-app links switch to vanity URLs — **FT: view-source is real HTML; Discord unfurl shows the jacket** |
| P4 | Tests + docs | E2E facts (anon chart page serves content pre-circuit; GUID 301s; glow only when signed in), bUnit island coverage, ARCHITECTURE/UX-GUIDELINES/API/DATABASE-SCHEMA sync |

## Out of scope now, recorded for future iterations (owner, 2026-07-14)

- **Judgment distributions from recorded plays**: OfficialMirror could start banking per-play
  judgment breakdowns from the official site's recent-plays data (recorded scores, not best
  attempts). Not enough data to use yet — and collection is deliberately **not started** in this
  wave. When it lands: real kill-spot analysis, judgment-aware verdicts, `LifebarSimulator`-fed
  gauge claims.
- **piucenter section-by-section density/difficulty**: per-section data exists upstream; future
  iterations can add a section heatmap to the page, localize kill spots, and upgrade the
  similarity intensity vector. The §8a boundary (link out, don't import their full rendering)
  still applies.
- **Chart-vs-chart compare tool** (cut in the workshop; `/ChartCompare` name is currently a
  mix-diff page — naming decision pending when compare returns).
- **Per-mix historical pages**: old-mix URLs 301 to canonical until a frozen-history view earns
  its keep.

## Risks

- **Stage dependency**: P1+ requires the Stage-2 hosting flip (unscoped as of 2026-07-14). Its
  enhanced-navigation default (`blazor.web.js`) interacts with ApexCharts re-initialization —
  this page's graphs island is a named stakeholder in that scoping. Backend commits B1–B4 have
  no stage dependency and proceed now.
- **Similarity cold start**: sparse charts (few scores, no piucenter data) must still get
  neighbors (weight renormalization) or gracefully render a shorter shelf — never an empty box
  without a reason (UX rule 9).
- **Meta-description churn**: verdicts recompute nightly; sentences should be stable unless the
  underlying category actually moved (facets quantize before templating).
- **SEO cutover ordering**: vanity sitemap + real HTML + 301s must land together (P3), or Google
  recrawls thousands of empty pages.
