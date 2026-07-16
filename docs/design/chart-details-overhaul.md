# Chart details overhaul — design (Stage 3 of the SSR/islands migration)

Decided in the 2026-07-14 chart-details workshop (owner + Claude); visual mock approved the same
day (round 2 — community glow + honest plate framing). Companion specs:
[chart-similarity.md](chart-similarity.md) (the similarity graph + settled formula) and
[chart-verdicts.md](chart-verdicts.md) (the verdict engine).

**Status (2026-07-15).** B1–B4, P1+P2 and **the whole R-series (R1–R8)** are built and on this
branch: the similarity graph (table, nightly job, calculator, three read queries), the verdict
engine, the URL lattice services (dark), and the page itself — rebuilt to the approved mock
anatomy in **circuit form** (today's hosting), with every island-to-be a self-loading component
keyed by chart id.

**The similarity rework is complete and the formula is V1.** A 07-14/07-15 calibration session
against real data found B2's shape unsalvageable — it flattened to `sd = 0.030` across a whole
folder because it averaged a chart property (is this the same kind of problem?) with a viewer
property (how hard is it?). V1, now built: **similarity is skill + intensity only**
(`S_skill^0.75 · S_intensity^0.25`), difficulty is an ordering layer applied per viewer at read
time, metadata is filters that reduce the target list and recompute live.
⚠ **Read [chart-similarity.md](chart-similarity.md) §9 before touching the formula.** It records
every rejected alternative *with its measurement* — nearly all of them were tried and killed for
a recorded reason, including two claims this very doc set used to make. Weight-tuning breaks the
hand-computed fixtures; that warning still holds.

**Stage 2 is merged in** (`20c6b41e`, 2026-07-15) — the render-mode infrastructure exists and
this branch carries it. It did **not** make any page static: `<Routes @rendermode="Interactive">`
keeps the whole router in one circuit, which was Stage 2's deliberate "zero behaviour change".
So P3 is unblocked in the sense that the machinery is here, and **still blocked** in the sense
that one site-wide flip has to precede it — see "What P3 actually costs" under the build plan,
which also records the head problem that has no answer yet.

Remaining: **P3** (301 lattice live, sitemap vanity swap, static head/JSON-LD, output caching —
ships as ONE unit, never split: pretty URLs must never point at empty shells) and **P4** (E2E
facts + doc sync). **R6's island packaging folds into P3-1** for the same reason.
Owner-only steps: UX field-test rounds, and the **floor calibration run** (trigger
`recalculate-chart-similarity` in /hangfire, then set the floor from the score distribution — it
is a render constant now, so that is a redeploy rather than a job run). **The 2026-07-15 run
validated 0.55 against Rush-More exactly** (3 matches, all owner-graded good) — but on a
0.013-wide window, with THE REVOLUTION missing by 0.0004. Any constant that moves puts it back.

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
  stay Korean). Two hard rules from the catalog audit (below): the **difficulty slug is
  slot-aware** (`crazy-6` when a `LegacySlot` exists, `s6` otherwise — `DifficultyDisplay`'s
  own logic), and **`SlugifySong` carries an unslugifiable-name fallback** (`"!"` →
  `"exclamation"`). Name twins that never share a mix (`STEP`/`Step`) collide on nothing; the
  suffix-with-artist contingency stays shelved until two same-slug songs coexist in one mix.
- When the site default mix flips (~per mix era), the canonical namespace 301s wholesale and the
  sitemap regenerates — accepted (mass 301 migrations transfer signals).
- Sitemap lists **canonical vanity URLs only** (replacing GUIDs), and ships **in the same PR** as
  the prerendered page — pretty URLs pointing at empty shells would invite a wasted recrawl.
- Rejected: origin-mix-as-canonical (stale search intent; content mismatch). The stable-identity
  job belongs to the GUID permalink.

### The slug audit — full catalog, measured (2026-07-16)

Every `(mix, song-slug, difficulty-slug)` triple was computed over the populated dev catalog
(30,259 chart-mix rows, 1,040 distinct song names) with the shipped `ChartSlugs` logic.
**The canonical namespace — Phoenix, Phoenix 2, XX — is collision-free.** Every break lives in
legacy-mix historical URLs, and each settles a rule:

| Break | Count | Rule it settles |
|---|---|---|
| Slot-era ambiguity — same song + level, different `LegacySlot`: `/nxa/moonlight/s19` is two charts (the Hard-6/Crazy-6 problem `Chart.DifficultyDisplay` exists for) | 91 paths | The slot-aware difficulty slug (above). Canonical URLs untouched — modern mixes carry no slots. |
| Duplicate chart rows — same song + type + level + slot, two GUIDs (`Final Audition Ep. 2-2` D18 twice in NX, NX2 *and* NXA) | 47 paths | No URL scheme splits identical rows. Historical resolution picks **deterministically** (stable order, never `FirstOrDefault` luck); the loser stays reachable by GUID forever — the rebalance-collision philosophy. Flagged separately as legacy-import data quality: these double-render in legacy folder lists too. |
| Duplicate `Song` rows — `PICK ME` (Arcade) exists as two songs → `/prime-2/pick-me/d16` is ambiguous | 1 path | Same deterministic tiebreak; the duplicate row is a data-cleanup candidate. |
| Empty slug — `!` (Infinity only) slugifies to nothing → `/infinity//s17` is malformed | 1 song | The `SlugifySong` fallback (above), plus a fixture. |

Latent, no action: `STEP` (Phoenix/XX era) and `Step` (Fiesta 2/Prime era) share a slug but
have never shared a mix, so no path collides today. Renamed songs cannot alias — the catalog
keeps one `Name` per song, so an old community name is simply a slug that doesn't resolve; the
GUID permalink is the durable address.

**Open: where the lattice mounts** — bare root `/{mix}/{song}/{difficulty}` (this doc's
original shape) vs `/Charts/{mix}/{song}/{difficulty}`. Bare root makes the vanity route the
site's three-segment catch-all (component routes take no custom constraints); the `/Charts`
prefix contains it completely. Decision ④ in seo-friendly-site.md §7.

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
  empty in the initial HTML, so each island's **static wrapper** carries a layout-matched CSS
  skeleton (UX rule 9). Because a `prerender: false` island renders nothing server-side, it
  cannot remove a skeleton it never rendered — the settled mechanism (one pattern for all four
  islands, decided 2026-07-14): every island root renders `data-island-ready` on its outermost
  element, and the wrapper hides its skeleton via
  `.island-wrap:has([data-island-ready]) .island-skeleton { display: none }`.
- **Caching — this page ships it** (stage-plan call, 2026-07-14: output caching pays nothing
  while every page still boots a circuit for its content, so it lands with the first static
  page — this one). This work owns: the anonymous output-cache policy (GETs only, vary by path +
  resolved culture + mix, TTL from config), the response-header audit (no `Set-Cookie` on
  cacheable paths — the culture-cookie writer fix itself is Stage 2's), and the CDN rule spec
  (bypass on auth cookies; never-cache list). Post-flip the static body reads `HttpContext`
  directly, so the Stage-1 mix-as-root-param cache-poisoning hazard doesn't apply here.
  Signed-in renders at origin per request. Query results shared with the nightly analytics
  (verdicts, similarity, letter difficulties) are `IMemoryCache`d with TTLs aligned to the
  recompute cadence.
- **Retirement within the rebuild**: the dead `ChartOverview` dialog, the `TournamentId`
  query-param relic, the admin form's mid-page squat, and the `/Record` route alias all go;
  navigation into charts keeps working everywhere because `/Chart/{guid}` 301s forever.
  `ChartDetailsDialog` stays (widgets' quick-look surface) — shared pieces (skill bars, meta
  grid) extract into components both consume where practical (one concept, one component).
- **Localization**: every new string through `L[…]`, all nine locales in the same pass. Verdict
  templates live Web-side (see [chart-verdicts.md](chart-verdicts.md) — the engine returns
  structured facts; Web renders words).

## Technical scope by layer (settled 2026-07-14)

**Core (SharedKernel + Domain): untouched.** No new value types, no scoring-engine changes, no
new shared secondary ports — every read goes through ports/contracts that already exist
(`IScoreReader`, `IPlayerStatsReader`, `IChartRepository`, Catalog's published queries).
**Core `ScoreTracker.Application`: untouched** (it stays shrinking).

**ChartIntelligence — the only vertical with changes:**
- `Domain/` (internal, pure, unit-tested): `ChartSimilarityCalculator` — the settled formula
  (gates → five signals → weight renormalization → level affinity → top-8 @ 0.55) over internal
  feature models (`ChartSimilarityFeatures`, `SimilarityEdge`); `ChartVerdictService` — the
  eight facet computers with min-evidence bars and quantization, returning facts, never strings.
- `Contracts/`: `GetSimilarChartsQuery` + `ChartSimilarityRecord` (edge + per-signal breakdown),
  `GetChartVerdictQuery` + `ChartVerdictFacet`, `Contracts/Messages/RecalculateChartSimilarityCommand`.
- `Application/` (internal): the similarity saga (`IConsumer` + the two query handlers; verdicts
  computed on read, `IMemoryCache` daily TTL, no verdict table v1).
- `Infrastructure/` (internal): `ChartSimilarityEntity` + EF repository behind an internal port,
  registered on the vertical's **existing** model contribution — no CompositionRoot change.
- `Wiring/`: one consumer-hook line.

**Read-only vertical touchpoints** (existing contracts, no code changes expected): Catalog
(skills, step analysis, videos), ScoreLedger (scores, journey), PlayerProgress (stats),
Communities (glow membership). Two flagged possible additions, decided when hit: a **bulk
step-analysis read** on Catalog if the nightly sweep is too chatty per-chart (B2), and a
**capped top-N-plus-rank leaderboard read** so the page never inherits the uncapped World
query (P2).

**Infrastructure (`ScoreTracker.Data`): migration only** — the `ChartSimilarity` table. No new
clients, no new external APIs.

**Presentation (Web)**: `ChartDetails.razor` rebuilt + five islands + presentational components
(verdict card, timeline, shelf card, fact strip, extracted skill bars) + the `--mud-*` token
audit; `ChartPermalinkController` + `ChartSlugService`; sitemap swap; `Program.cs` job line +
output-cache policy (P3); verdict templates ×9 locales; `UiColorTokenTests` allowlist burn;
deletions (`ChartOverview`, `TournamentId` relic, mid-page admin form, `/Record`).

**Hangfire**: exactly one new recurring job — `recalculate-chart-similarity`, cron
`0 12 * * *` UTC (07:00 ET), deliberately after the analytics chain it reads (tier lists
07:00–09:30, letter difficulties 10:00 UTC). Verdicts add no job.

**Secrets: none.** No new external calls anywhere in this branch; nothing lands in AppHost
user-secrets or pipeline variables.

## Build plan

**The table below is the commit order.** B1→B4 and P1→P2 are **built and on the branch**; R1→R8 is
the similarity rework settled at the 2026-07-15 workshop; P3→P4 remain gated on Stage 2 (hosting
flip) merged forward. Suites green at every row; FT = owner field test.

| # | Commit | Contents |
|---|---|---|
| B1 ✅ | Similarity: table + job skeleton | `ChartSimilarity` contribution + migration, `RecalculateChartSimilarityCommand` + consumer + Hangfire line, DATABASE-SCHEMA + SCHEDULED-JOBS rows |
| B2 ✅ | Similarity: signals + formula | **superseded by R1–R4** — three of its five signals leave the score |
| B3 ✅ | Verdict engine | facet computers + salience ranking (unit-tested), `GetChartVerdictQuery`, caching |
| B4 ✅ | URL lattice (dark) | slug service + historical (mix, song, level) resolver, tested; not yet linked or sitemapped |
| P1 ✅ | The page, hero → evidence | `ChartDetails.razor` rebuilt in place, circuit form |
| P2 ✅ | History, leaderboard, shelf | timeline, glowing top-10 + explorer island, similarity shelf, sibling bubbles, admin island |

**The similarity rework (R-series).** Settled 2026-07-15 — see
[chart-similarity.md](chart-similarity.md), which is a rewrite, not an edit. R1→R4 have no stage
dependency. Partly landed already: Bray-Curtis + γ over raw badges, the geometric combiner, note
count out, and the level tax removed are **on the branch**; `GetChartDominancePicksQuery` was built
and deleted.

| # | Commit | Contents |
|---|---|---|
| R1 ✅ | Strip similarity to skill + intensity | Delete `DifficultySimilarity` / `PlayerSimilarity` / `MetaSimilarity` + their weights + `MinimumSharedScorers`; `ChartSimilarityFeatures` 17 fields → 7; `ChartSimilarityEdge` sheds Difficulty/Players/Meta/SharedScorers; the saga loses `BuildResiduals` and its tier-list / letter-percentile / scoring-level reads — **and with them `IScoreReader.GetScores` (a ~50k-row folder read per level) and the `IPlayerStatsReader` fan-out**. Rewrite the `nonMetaAvailable >= 2` gate to say what it now means (both signals mandatory). Fixtures shed ~8 of 19 cases. |
| R2 ✅ | Intensity: decompose + geometric | `susFrac`/`burstFrac` replace `tensionFrac` (sustain ⊆ tension was double-counted); geometric over the three dims; weights `burst .40 / sus .40 / nps .20`, K = 3. **Fixtures: Gargoyle - FULL SONG - D25 sentinel + the Hymn S19-vs-S22 controlled inversion** (chart-similarity.md §8). |
| R3 ✅ | Storage: top-20, floor-free, match reasons | `ChartSimilarityEdge` gains `SharedBadges` (the `min(a,b)` terms); entity drops `SharedScorers`; `SignalsJson` reshape; migration; `TopK` → 20; `ScoreFloor` leaves the domain and becomes a render constant. **Cron dependency drops** — SCHEDULED-JOBS row. |
| R4 ✅ | Read contracts | `GetSimilarChartsQuery` (precalculated default) + a live filtered query (filters reduce the target list, then recompute) + `GetOppositeChartQuery` (novelty; live, no storage). |
| R5 ✅ | The card | `SimilarChartCard`: 16:9 art, `DifficultyBubble` on the corner, original mix on the meta line, named match chips, `<a href>` wrapping art+body with the **play button as a sibling overlay** (both pinned by bUnit — they are the two halves P3's caching depends on). Video swaps into the art's exact box, so nothing reflows. 3-across → 2 → 1. |
| R6 ✅ | The controls | Sort (No sorting / Community / Pass·me / Score·me), filters + reach line, near-misses, degraded state. **The island packaging moved to P3** — `data-island-ready` needs Stage 2's render modes, which are not on this branch (the app is still classic `AddServerSideBlazor`). Built as circuit components, which is what the page already is; the crawlable `<a href>` + sibling play button that P3 depends on are already pinned. |
| R7 ✅ | Localization | 21 new keys × 9 locales, and the 5 keys the rework killed removed from each. **The 29 badge labels are deliberately untranslated** — the site does not localize its pattern vocabulary at all (`Skill.GetName()` bypasses the localizer entirely), so they fall back to English through `L[…]` exactly as skills do, leaving the hook for a later resx-only change. |
| R8 ✅ | Tests + docs | bUnit card + shelf coverage, `SimilarChartsShelfTests` rewrite, DATABASE-SCHEMA + SCHEDULED-JOBS sync. |
| **FT** | **Calibration run** | Owner runs the analyzer → query the score distribution → **set the floor** (chart-similarity.md §10). Nothing before this teaches us anything about shelf sizes. |

| # | Commit | Contents |
|---|---|---|
| P3 | SEO cutover + caching | static head/JSON-LD live (old dead-channel `HeadContent` retired), sitemap swaps to vanity, 301 lattice goes public, in-app links switch to vanity URLs, output-cache policy + CDN header spec ship (first meaningfully cacheable page — verify cached anon HTML actually contains content) — **FT: view-source is real HTML; Discord unfurl shows the jacket** |
| P4 | Tests + docs | E2E facts (anon chart page serves content pre-circuit; GUID 301s; glow only when signed in), bUnit island coverage, ARCHITECTURE/UX-GUIDELINES/API/DATABASE-SCHEMA sync |

### What P3 actually costs — scoped 2026-07-15, against shipped source

Stage 2 **is merged** (`20c6b41e`), so the render-mode infrastructure exists. It did not,
however, make any page static, and **this page cannot go static on its own.**

**The blocker is one line in `App.razor`:**

```razor
<main class="shell-main">
    <Routes @rendermode="Interactive" CurrentMix="_shell.CurrentMix" />
</main>
```

The shell paints statically around it, but the **whole router is one circuit**, and a render
mode propagates *down* — a child cannot opt back out. Nothing under `<Routes>` can be static
while that attribute is there, and **no page carries a render mode of its own** (verified: zero
hits across all 38 routable pages). Stage 2's "zero intended behaviour change" was achieved
exactly this way, and it was the right call — but it means the entry ticket to Stage 3 is a
site-wide flip, not a per-page one.

⚠ **An earlier revision of this doc said the page "goes static SSR by dropping `@rendermode` at
the hosting flip". That is wrong** — there is no `@rendermode` on the page to drop. It inherits.

**P3-0 — the prerequisite, and it is not this page's work.** One mechanical commit plus a
site-wide QA, the same shape as Stage 1's:
1. Drop `@rendermode="Interactive"` from `<Routes>` — the router renders static.
2. Add `@rendermode InteractiveServer` to **all 38 routable pages** — every page still a
   circuit, still zero behaviour change.
3. From then on, a page converts by deleting its own line and islanding its children.

**P3-1 — then this page.** Delete `ChartDetails`' `@rendermode`; island what needs a circuit:
`ChartVideoPlayer`, `ChartRecordPanel` (also personal → must never enter the cache),
`ChartEvidenceSection` (Apex), `ChartLeaderboardSection`, `SimilarChartsShelf`'s controls (the
cards are already real `<a href>`s and stay static), and `ChartSelector` on the empty state.

⚠ **The head is an unsolved problem, and it is the whole SEO point.** `HeadOutlet` is
`@rendermode="Interactive"`, so it collects from *interactive* components only. The moment
`ChartDetails` goes static, its `<PageTitle>`/`<HeadContent>` stop being collected — and
`App.razor`'s static `<title>` is a fixed string ("PIU Scores"), so **every crawled chart page
would share one title.** A static `HeadOutlet` is not the answer either: it would break the
`<PageTitle>` on all 38 still-interactive pages. This needs a real mechanism (the shell
resolving per-route head content server-side, or a second outlet) and it should be scoped
before P3 starts, not during. render-modes.md §7.2 flagged the composition question; this is
its sharp end.

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
