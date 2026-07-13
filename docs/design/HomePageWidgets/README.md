# Home Page Widget Builder

**Status**: spec locked 2026-07-12; **PR #1 (§5 + history capture) merged as PR #141**;
**PR #2 (shell + trio, C1–C10) implemented — this PR**. Next: owner field-test, then the §4 catalog
walk (D18). Sequencing (owner): **PR #1 = Pumbility history capture + Projections v2
(§5)** — small, merged fast so trend data accrues immediately; **PR #2 = the shell + starter trio
(§2–§3)**. Mocks: [dashboard](https://claude.ai/code/artifact/d55215b1-ce72-4623-ba10-9950266f4847)
· [widget spec sheets](https://claude.ai/code/artifact/809e2710-c596-4454-96fd-f5a8fc765871).

`/WhatShouldIPlay` is being retired. In its place: a fully customizable, multi-page, cross-mix widget
dashboard. This doc is the working spec for PR #1 and PR #2; later phases (WSIP parity, cutover, widget
catalog) get their sections as they start.

---

## 1. Locked decisions (owner, 2026-07-12)

| # | Decision |
|---|---|
| D1 | Clean-slate curated default at cutover — **no migration** of WSIP settings. Missing-thing complaints are answered with "add it to your page." Default templates ship, including a solid "Home" built for the 90% who never customize. |
| D2 | Pages are **private** in v1. Widget configs must never contain secrets — sharing may come later. |
| D3 | Mobile layout is **derived**: single column in desktop grid reading order. Mobile edit = ↑↓ reorder arrows + size sheet (no touch drag). Pages swipe on mobile. |
| D4 | Caps: **8 widgets per page, 8 pages per user**. Raised only on pain points + healthy telemetry. |
| D5 | Grid: 4 columns desktop, per-widget **size presets** (1×1, 2×1, 1×2, 2×2, 3×1…), auto-flow, snap. No freeform resize. |
| D6 | Placement model is an **ordered list + spans** (CSS grid auto-flow) — not x/y coordinates. Drag = reorder (insertion index), same operation as the mobile arrows. |
| D7 | The dashboard stays **hidden until market release** — an unlisted route, NOT admin-gated: no nav links, but anyone who finds it may use it. Edit mode shows non-admins a disclaimer: *configurations may be wiped if the schema changes before final release*. Each widget after the shell trio is its own PR. |
| D8 | Time-sensitive widgets v1: **Weekly Challenge only**. New-charts and tournaments widgets are HELD. |
| D9 | **Rivals ships later as one complete project** — the registry must let a new vertical contribute widget types with zero home-page changes. |
| D10 | Suggester = **one widget type**; the goal (Title Hunt / level push / skill gaps) is config. The add-drawer lists preset-configured entries for discoverability. Feedback/veto machinery survives. |
| D11 | **Mix Migration is its own widget type** (re-pass tracker, source→target mix; `GetCrossMixPassesQuery` exists). Featured at P2 launch, de-featured later. |
| D12 | **Daily Step** (renamed from "Daily Dance" — community says *step*, not *dance*): **one site-wide shared** daily chart + mini leaderboard (owner-confirmed), level 16–24; once a week on a random day = **Limbo Day**: level ≤ 15, *lowest passing score wins*. Lives beside WeeklyChallenge; own PR in catalog phase, not shell scope. The owner also has plans for a **per-player daily challenge — a separate concept, its own future session**; do not conflate. |
| D13 | Mix resolution cascade: **widget override → page default → current mix**. |
| D14 | Widget lifecycle contract (§3.3) — the page **never gates as a whole** (kills today's all-or-nothing P2 gate on WSIP). |
| D15 | Verticals own data/queries/precompute; **Web owns the registry + render components**; layout persistence is a new small vertical. |
| D16 | Widget telemetry (add/remove/move/resize/configure + page events) from day 1. |
| D17 | **PR sequencing** (owner, 2026-07-12): PR #1 = SkillRating history capture + Projections v2, shipped first and merged fast; PR #2 = shell + trio. PR #1 branches only after PR #140 (personalized breakdown) merges — it relocated the skill machinery into `TierListBlendBuilder`. |
| D18 | **Catalog process**: after the PoC trio ships, the owner walks the §4 backlog **one widget at a time**. Rivals and the per-player daily challenge are standalone future sessions — not part of the walk. |
| D19 | **Page config export/import + capability schema** (owner, 2026-07-12): edit mode exports/imports a page's configuration as JSON, and the full "what's available" schema is exportable so people can build dashboards with AI. Both documents are **public API DTOs** — breaking-change discipline like `api/*`, pinned by approval tests in `ScoreTracker.Tests.Api`. See §2.6. |

---

## 2. Architecture

### 2.1 New vertical: `ScoreTracker.HomePage`

Owns dashboard layout persistence — pages and widget instances. Standard vertical shape
(`Contracts/`, `Wiring/` public; everything else internal; `IDbModelContribution` registered in
`VerticalModelContributions.All()` — **forgetting this silently drops the tables from migrations**).

Tables (rows to be added to DATABASE-SCHEMA.md in the shell PR):

| Table | Columns (sketch) |
|---|---|
| `HomePage` | Id PK, UserId (idx), Name nvarchar(64), Ordinal tinyint, IsDefault bit, DefaultMix tinyint NULL |
| `HomePageWidget` | Id PK, PageId FK (idx), WidgetType varchar(64), Title nvarchar(64) NULL, Ordinal tinyint, SizePreset varchar(8), ConfigJson nvarchar(2000), ConfigVersion int |

Contracts (all `[ExcludeFromCodeCoverage]` records; caps enforced in handlers, not just UI):

- **Queries**: `GetMyHomePagesQuery` → pages + widget instances in one shot (max 8×8, trivially small).
- **Commands**: `CreateHomePageCommand` (name, optional template), `RenameHomePageCommand`,
  `DeleteHomePageCommand`, `ReorderHomePageCommand`, `SetDefaultHomePageCommand`, `SetHomePageMixCommand`;
  `AddHomePageWidgetCommand`, `RemoveHomePageWidgetCommand`, `MoveHomePageWidgetCommand` (newOrdinal —
  the one operation both drag and arrows dispatch), `ResizeHomePageWidgetCommand`,
  `RenameHomePageWidgetCommand`, `UpdateHomePageWidgetConfigCommand`.

Every mutation persists immediately (Blazor Server circuits are not to be trusted with staged edits).

### 2.2 Widget registry (Web)

Razor components can't live in the plain-classlib verticals, so render components live in Web under
`Components/HomeWidgets/`, and a static registry maps type → everything the shell needs:

```csharp
public sealed record WidgetDescriptor(
    string TypeId,              // stable, stored in HomePageWidget.WidgetType — never renamed
    string NameKey,             // L10n keys
    string DescriptionKey,
    WidgetCategory Category,    // Play / Progress / Compete / Utility (drawer grouping)
    string Icon,
    SizePreset[] SupportedSizes,
    SizePreset DefaultSize,
    MixEnum[] SupportedMixes,   // config UI restricts; resolution clamps
    Type RenderComponent,       // gets the WidgetInstance (config + size + resolved mix)
    Type? ConfigComponent);     // null = no per-widget config beyond title/mix
```

Verticals contribute *data* (contract queries, precompute jobs); Web contributes the descriptor +
components. A future vertical (Rivals) adds descriptors without touching shell code (D9).

### 2.3 Widget lifecycle contract (D14)

Every render component must provide:

1. **Skeleton** at fixed footprint for its size preset (no layout shift on load).
2. **Empty state** with a setup CTA (no scores → import link; no data yet → what will make it appear).
   A widget with no data is an onboarding surface, not a blank box.
3. **Isolated errors** — each grid cell wraps its widget in `<ErrorBoundary>`; one vertical throwing
   never takes the board down.
4. **Freshness** — precomputed widgets show a quiet "as of …"; live widgets may offer refresh.
5. **Config JSON + `ConfigVersion`** — old blobs are tolerated or migrated forever. Config records are
   additionally **public contract** via export/import and the capability schema (§2.6): shape changes
   are breaking-change review, not refactoring.
6. Distinction between persisted config and session-transient state (a randomizer's "already rolled"
   list never hits the DB).

Rendering rules: widgets render with `@key="InstanceId"` (reorders move component subtrees, preserving
internal state); **widget data refresh is paused while edit mode is active** (nothing re-renders the
grid mid-drag, and a board shouldn't reshuffle while being rearranged).

### 2.4 Grid + drag

- CSS grid, 4 columns desktop, auto-flow; widgets are `(Ordinal, SizePreset)` — D6.
- **Arrow-move baseline ships first** (desktop too): keyboard-accessible, forces `MoveHomePageWidgetCommand`
  to be right before any JS exists, and is the mobile mechanism anyway.
- **Drag is a client-side enhancement**: `wwwroot/js/dashboard-grid.js` (~200–250 lines, pointer events —
  not HTML5 DnD, not `MudDropContainer`, which is list/zone-shaped and would fight a spanning grid).
  Pointerdown on handle → ghost → insertion-gap highlight from cell geometry → drop → one `[JSInvokable]`
  call `(instanceId, newIndex)`. 60Hz pointermove never crosses SignalR; only the drop does.
  Precedent for JS interop modules: `helpers.js`, `phoenix-recap.js`.
- Escape hatch if field-testing demands true x/y pinning: vendor Gridstack.js. Not v1.

### 2.5 Shell infrastructure

- **`ChartCatalogCache`** (Web, circuit-scoped): memoizes `GetChartsQuery` per mix so N widgets share
  one chart dictionary instead of each loading it (Weekly needs it in the trio; Suggested/Randomizer
  later).
- **Chart series palette**: ApexCharts needs literal colors at config time (can't read CSS vars), so
  `MixPalette` gains `ChartSingles`/`ChartDoubles`/`ChartCombined` per mix, exposed the same way
  `DifficultyHex` serves the share card. Values (validated CVD-safe, worst adjacent ΔE 16.3, contrast
  ≥ 5.1:1 on their surfaces): Phoenix `#FF6B35/#38B6FF/#CBD5E1`, Phoenix 2 `#E93CF2/#29C9F7/#CBDCD0`,
  XX `#FF2FA0/#3B9EFF/#D5CDE3`. This also retires WSIP's hardcoded red/green/cyan trio, which has a
  deutan collision.
- **Telemetry**: Clarity custom events via `helpers.js` (`widget_added/removed/moved/resized/configured`,
  `page_created/deleted/switched`, `edit_entered`), tagged with `WidgetType`.
- **Beta gate**: unlisted route — no nav entry anywhere, open to anyone who finds it (D7). Edit mode
  renders a banner for non-admin users: "This page is pre-release — your layout and widget settings may
  be reset if the schema changes before launch." WSIP keeps `/` until cutover.
- **Localization**: every widget commit lands its keys in **all** locales in the same pass (coordinate
  with the es-ES PR #133 if it merges first).

### 2.6 Page config export/import + capability schema (D19)

Two JSON documents, both treated as **public API contracts** (camelCase, stable names, versioned
envelope, golden-JSON approval tests in `ScoreTracker.Tests.Api` — a failing approval is
breaking-change review, exactly like `api/*`):

**1. Page export/import** (edit-mode buttons: Copy / Download / Import):

```json
{
  "version": 1,
  "page": {
    "name": "Session Day",
    "defaultMix": "Phoenix2",
    "widgets": [
      { "type": "pumbility", "title": "Doubles push", "size": "2x1", "config": { "mix": "Current", "showProjections": true, "dismissedCharts": [] } }
    ]
  }
}
```

- `type` = registry `TypeId` (stable forever); array order **is** widget order (no ordinals in the
  public shape); `size` = preset token. Per-widget `config` shapes are part of the contract.
- **Import replaces the current page** after validation + a confirm dialog summarizing what will be
  replaced. Validation is exhaustive and human-readable (errors will be pasted back into an AI):
  unknown type, unsupported size for that type, invalid mix, config schema violations, cap violations
  (> 8 widgets), payload size cap. Applied via a single transactional
  `ReplaceHomePageWidgetsCommand` — the one mutation that is *not* per-interaction.
- Side benefit: pre-release schema wipes (D7) are survivable — export first, re-import after.

**2. Capability schema** (downloadable from the add-drawer: "Building with AI? Download the schema"):

```json
{
  "version": 1,
  "limits": { "widgetsPerPage": 8, "pagesPerUser": 8 },
  "sizes": ["1x1", "2x1", "1x2", "2x2", "3x1"],
  "mixes": ["Phoenix", "Phoenix2", "XX"],
  "widgets": [
    { "type": "competitive-level", "name": "Competitive Level", "description": "…", "category": "Progress",
      "supportedSizes": ["2x1", "2x2"], "defaultSize": "2x1", "supportedMixes": ["Phoenix", "Phoenix2"],
      "configSchema": { "…JSON Schema for this widget's config…": true } }
  ]
}
```

- Generated from the registry + each widget's config record (small hand-rolled JSON-Schema emitter for
  the constrained shapes we use — no new package; enums emit as string unions). The C# config record
  stays the single source of truth; the approval test pins the emitted document so any drift is a
  reviewed diff.
- v1 surfaces both documents in the page UI only; a public `api/*` endpoint (and docs/API.md entry)
  ships at cutover when the dashboard goes public. The DTO discipline starts now so the format never
  churns.

---

## 3. Shell PR (PR #2) — starter trio specs

Chosen because all three are existing single-query reads with zero new business logic in the shell
itself — they prove the registry, live-pull rendering, per-widget states, and the chart pipeline.
The one domain prerequisite (Projections v2 + history capture, §5) ships first as **PR #1**, so the
Pumbility widget consumes finished contracts.

Full specs now live in their own files (C0 split — each widget's spec is edited in isolation, so
widget PRs stop colliding on this doc):

- **W1 — Competitive Level graph** → [competitive-level.md](competitive-level.md)
- **W2 — Pumbility** → [pumbility.md](pumbility.md) (consumes the Projections v2 work in §5)
- **W3 — Weekly Challenge** → [weekly-challenge.md](weekly-challenge.md)

### PR #2 (shell) commit plan

| C | Content |
|---|---|
| C1 | `ScoreTracker.HomePage` vertical skeleton: assembly, wiring, model contribution (+ `VerticalModelContributions.All()`), entities, migration |
| C2 | Contracts + handlers (pages + widgets CRUD, caps, templates) + component tests |
| C3 | Registry + unlisted route + read-only board render (grid, tabs, skeletons, error boundaries) |
| C4 | Edit mode: add-drawer, remove, arrow-move, resize, rename, per-mutation persistence + the non-admin pre-release disclaimer banner (D7) |
| C5 | W1 Competitive Level graph (multi-mix, no Combined, left-off markers) + `MixPalette` chart-series colors |
| C6 | W2 Pumbility — consumes PR #1's Projections v2 (pools, skill chips, plate curve) + scrollable targets + dismiss blacklist + conditional sparkline |
| C7 | W3 Weekly Challenge (filter modes via `WeeklyChartSuggestionPolicy`) + `ChartCatalogCache` |
| C8 | Page export/import + capability schema (D19/§2.6): DTOs, validation, `ReplaceHomePageWidgetsCommand`, schema emitter, edit-mode UI, golden-JSON approval tests in Tests.Api |
| C9 | `dashboard-grid.js` drag module + Playwright drag fact |
| C10 | Telemetry events; docs (DATABASE-SCHEMA rows, UX-GUIDELINES widget section, ARCHITECTURE vertical list); starter-template seeding |

---

## 4. Widget catalog (planned — NOT fully scoped; each is its own PR when its turn comes)

Readiness: ✅ published contract already serves it · 🔨 needs new data/query · ⏸ HELD by owner (D8) ·
🔮 gated on a later project. One-liners only — each widget gets its spec section when it's picked up.

| Vertical | Widget | Ready | Note |
|---|---|---|---|
| PlayerProgress | Suggested Charts | ✅ | **SHIPPED — catalog-walk pick 1** (spec [suggested-charts.md](suggested-charts.md)) |
| PlayerProgress | Title Progress | ✅ | pushing title + remaining-charts math from WSIP |
| PlayerProgress | Stats snapshot | ✅ | competitive levels block (Pumbility has its own widget) |
| PlayerProgress | Recent Highlights | ✅ | `GetScoreHighlightsQuery` — the Discord-card engine, on the home page |
| PlayerProgress | Milestones | ✅ | `GetPlayerMilestonesQuery` |
| PlayerProgress | Season-recap teaser | ✅ | permanent front door to `/Player/{id}/PhoenixRecap` |
| ScoreLedger | Session Journal | ✅ | `GetRecentSessionsQuery`; score history was a top user ask |
| ScoreLedger | Chart Journey | ✅ | pin a white-whale chart, `GetChartScoreJourneyQuery` sparkline |
| ScoreLedger | To-Do List | ✅ | must survive WSIP (parity phase) |
| ScoreLedger | Quick Record | ✅ | ChartSelector + EditChartGrid inline; the arcade widget |
| ScoreLedger | On the bubble | 🔨 | near-miss grades/plates ("18,797 from AAA+"); compute from records |
| ScoreLedger | Activity heatmap | 🔨 | calendar of play days from the journal |
| ScoreLedger | Mix Migration | ✅ | D11 — own widget; `GetCrossMixPassesQuery`; featured at P2 launch |
| ScoreLedger | By-Level Breakdown | ✅ | **BUILT (C0–C7)** — one configurable per-level graph (spec [by-level-breakdown.md](by-level-breakdown.md)) |
| ChartIntelligence | Tier-list folder peek | ✅ | 28% of traffic deserves a home-page hook |
| ChartIntelligence | Next breaks | ✅ | closest-to-passing from tier data |
| ChartIntelligence | Vote prompt | 🔨 | "you played X — rate it"; community-rating engagement |
| Catalog | Randomizer | ✅ | port = the moment to kill the transitional Catalog→Application ref |
| Catalog | Skill spotlight | ✅ | chart skills / step analysis surface |
| Catalog | New content feed | ⏸ | HELD (D8) despite P2 cadence fit |
| OfficialMirror | Rankings snapshot | ✅ | world/regional position; movement arrows need rank history 🔨 |
| OfficialMirror | Import nudge | ✅ | "last sync 9 days ago" + one-tap re-import; upload is the #2 page |
| OfficialMirror | Benchmark player | 🔮 | rivals-lite on mirror data; decided during the catalog walk (D18), rival machinery stays out until the Rivals project |
| WeeklyChallenge | Weekly Challenge | ✅ | shell trio (W3) |
| WeeklyChallenge | Daily Step | 🔨 | D12 — daily rotation job + shared board + Limbo Day; own PR ([daily-step.md](../daily-step.md)) |
| WeeklyChallenge | Per-player daily challenge | 🔮 | owner has plans — separate concept from Daily Step; its own future session (D12/D18) |
| EventCompetition | Active tournaments | ⏸ | HELD (D8) |
| EventCompetition | Qualifiers countdown | ⏸ | HELD (D8) |
| Communities | Community Feed | ✅ | scoping selector: specific / non-region / non-world / all |
| Communities | Leaderboard position | ✅ | mini "where you stand" per community |
| Ucs | UCS spotlight | 🔨 | new UCS + leaderboard activity |
| — (utility) | Note / Quick Links / Countdown / Divider | n/a | no data; disproportionate personalization value |

Standing rules: folder-completion widgets frame as **self-progress, never peer comparison** (owner);
rarity/difficulty coloring only ever via `ThemeScales`; every chart row carries the standard card
affordances (video / record / todo) as shared components.

### 4.1 Suggested Charts → [suggested-charts.md](suggested-charts.md)

## 5. Pumbility Projections v2 — **PR #1**, ships before the shell (D17)

Bundled with the SkillRating history capture into one small PR the owner merges fast — trend data starts
accruing immediately, and v2 goes live on the existing `/Pumbility` page (including enabling Phoenix 2
there) before the widget exists.

**Current algorithm** ([PumbilityProjectionSaga](../../../ScoreTracker/ScoreTracker.PlayerProgress/Application/PumbilityProjectionSaga.cs)):
cohort = players within ±1 competitive level per chart type; my expected score on a candidate chart =
cohort score distribution interpolated at my average percentile across played charts in that (type, level)
slice (×0.95 damping); expected pumbility assumes an EG plate; gains measured against the lowest of ONE
mixed top-50 pool. Phoenix 2 is explicitly disabled — the in-code TODO defers two-pool projection to
"the What-Should-I-Play overhaul," i.e. this project.

**V2 scope:**

1. **Two-pool P2 correctness.** Pools abstraction: Phoenix = one mixed pool; Phoenix 2 = independent
   S and D pools (`GetTop50ForPlayerQuery` already takes `ChartType?`). Baseline ("lowest of top 50")
   and gain comparisons run against the chart's own pool. `ScoringConfiguration.PumbilityScoring(mix)`
   already encodes each mix's rating math (P2 = Base × (Grade + PlateBonus), additive).
2. **Skill-distribution adjustment** — reuse the tier-list competence machinery, never re-derive it:
   - New ChartIntelligence contract: `GetPlayerSkillDeviationsQuery(userId, mix, levelWindow)` →
     per-`Skill` deviation on the floored proficiency scale. **PR #140 already did the extraction step**:
     the machinery now lives in `TierListBlendBuilder` (shared by the blended-tier-list and Personalized
     Breakdown handlers), constants unchanged (proficiency = clamp(score − 900k, 0, 100k)/100k;
     segment-fraction weights with 0.5 default; `FolderDecay {1, .6, .3, .15}`; `MinSkillEvidence 2.0`;
     `MinUsableSkills 3`), with per-skill `SkillEvidence(Deviation, Evidence, Usable)` records already
     structured. #140's final revision also **freshness-weights the folder baselines** (age weights on
     proficiency evidence) — the deviation query inherits that automatically, which is desirable:
     deviations reflect current ability, not stale scores. P2 exposes the computation through the
     published query — PlayerProgress consumes the contract; cross-vertical reads stay contract-only.
     **PR #1 branches only after #140 merges.**
   - Application: expected score → proficiency scale → + damping × Σ(chart skill weight × my skill
     deviation) → back to score, clamped. Damping starts at 0.5, tunable. Charts without banked chips
     (`GetChartSkillChipsQuery` absent) get no adjustment.
   - **"Why" surface**: `PumbilityProjection` gains per-chart skill-match contributions (signed) so the
     widget renders "+Twist / −Bracket" chips — targets should explain themselves.
3. **Plate assumption: empirically calibrated score→plate step function** (owner direction 2026-07-12:
   map expected plate from the projected score — plate contribution is small, precision not required).
   Calibrated against the local dev database (prod-synced, n = 922,765 non-broken plated Phoenix records,
   modal plate per score band):

   | Projected score | Expected plate |
   |---|---|
   | 1,000,000 | Perfect Game |
   | ≥ 996,000 | Ultimate Game |
   | ≥ 972,000 | Marvelous Game |
   | ≥ 964,000 | Talented Game |
   | below | Fair Game |

   Data notes: Superb and Extreme are *never* the population mode in any band — real plate progression
   modally ladders FG → TG → MG → UG, so the expectation function never emits SG/EG/RG. Crossovers were
   measured at 2k-band granularity (FG→TG at 964k, TG→MG at 972k, MG→UG at 996k). Ship as a pure Domain
   function with the table pinned by a DomainTest; recalibrate for P2 once its plate data accumulates
   (same query, one constant swap) — explicitly not an exact science.
4. While in there: assert `PlayerRatingsImprovedEvent.NewTop50` equals the P2 two-pool combined value
   (feeds the P1 history capture in this same PR). *Verified: `PlayerRatingSaga` sums the int-floored
   S+D pools so `SkillRating == SinglesRating + DoublesRating` exactly, and the same variable feeds
   the stats record and the event.*

**Tests**: pool math + proficiency adjustment as pure DomainTests; saga component tests with mocked
readers over a fixed cohort fixture; the skill-deviation query handler gets tests pinning equivalence
with `TierListBlendBuilder`'s computation (same inputs → same deviations — #140's handler tests already
pin the builder's behavior from the tier-list side).

### PR #1 commit plan

| P | Content |
|---|---|
| P1 | **History capture**: `SkillRating` column on the history entity + migration; `PlayerRatingRecord` field; `PlayerHistorySaga` maps `NewTop50`; repository read/write mapping; NewTop50 ≡ two-pool-combined assertion; DATABASE-SCHEMA row |
| P2 | **Skill-deviation query**: expose `TierListBlendBuilder`'s skill computation (post-#140 home of the machinery) through a published `GetPlayerSkillDeviationsQuery`; equivalence tests pin same-inputs-same-outputs |
| P3 | **Pools abstraction** in `PumbilityProjectionSaga`: Phoenix single mixed pool, P2 independent S+D pools; DomainTests for the pool math |
| P4 | **Plate curve**: pure Domain step function (empirical table pinned by DomainTest) replaces the flat-EG assumption |
| P5 | **Skill adjustment + "why"**: damped deviation adjustment on expected scores; signed skill-match contributions on `PumbilityProjection`; enable Phoenix 2 on `/Pumbility` (delete the in-code TODO) |
| P6 | Docs + localization for any new `/Pumbility` strings (all locales, one pass) |

## 6. Later phases (parked)

1. **WSIP parity**: Suggested Charts (goal config, feedback/veto), Title Progress, To-Do, Quick Record.
   WSIP inventory that must find homes: pushing-title bar + remaining-charts math, category hide/show →
   per-instance config, thumbs/veto dialog, top-50 crowns (chart-card affordance, not a widget), XX→Phoenix
   `_dataMix` fallback (handled by `SupportedMixes` + cascade), dev empty-DB redirect (page level).
2. **Cutover**: default templates, curated Home for everyone, `/WhatShouldIPlay` → `/`, WSIP deleted.
3. **Catalog**: §4, one widget per PR — sequenced by the owner's one-at-a-time walk (D18).
4. **Rivals**: entire ecosystem, one standalone project/session (D9).
5. **Per-player daily challenge**: owner has plans; separate concept from Daily Step, its own session.
