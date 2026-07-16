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
| D1 | Clean-slate curated default at cutover — **no migration** of WSIP settings. Missing-thing complaints are answered with "add it to your page." Default template ships as `DefaultDashboardTemplate` (see §3), a solid "Home" built for the 90% who never customize. |
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

**Optional header slot (owner, 2026-07-13).** The host owns the title bar, but a widget may push a small
fragment into it to reclaim body space. `WidgetHost` cascades a `WidgetHeaderSlot`; a widget opts in via
`[CascadingParameter]` and calls `Set(fragment)` — the host renders it right of the title (suppressed in
edit mode, where the edit controls own the bar). Opt-in and generic: widgets that ignore it are
unaffected. Quick Record (§4.2) is the first consumer — the selected chart's art/bubble/✕ ride up there.

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

## 3. The curated default (`DefaultDashboardTemplate`)

At the go-live cutover, "Create" seeds the curated default (D1) — eight widgets ordered to pack the
4-column, non-dense (`grid-auto-flow: row`) grid into a 4×4 block plus a full-width footer strip,
with no gaps. Every widget follows the current mix (nothing pins a `Mix`, the page's `DefaultMix`
stays null). Order is load-bearing — gaps never backfill, so Folder Completion must precede Weekly
or Weekly claims the open bottom-left slot:

```
row 1: [Account Stats 1x2][Suggested·Pumbility 1x2][Import 1x1 ][Daily 1x2 ]
row 2: [    (cont.)      ][       (cont.)         ][Community  ][ (cont.)  ]
row 3: [Folder Completion 2x2                     ][ 1x3       ][Weekly 1x2]
row 4: [       (cont.)                            ][ (cont.)   ][ (cont.)  ]
row 5: [Suggested·Title Hunt — full width 4x1                             ]
```

Folder Completion is the By-Level Breakdown widget on `Pass`/`Breakdown`, singles+doubles combined,
levels 1–29. Suggested·Pumbility and Suggested·Title Hunt are the Suggested Charts widget on the
`PumbilityPush` and `TitleHunt` goals (natural ranges — no dynamic spread). The per-page cap is 10,
so the default leaves two slots to add. `DefaultDashboardTemplateTests` (Tests.Components) pins the
template against registry drift: every TypeId resolves, every size is supported, configs round-trip,
nothing pins a mix.

The **pre-release starter trio** (Competitive Level, Account Stats, Weekly Challenge) that PR #2
shipped is retired by the cutover — chosen then because all three were existing single-query reads
with zero new business logic, proving the registry, live-pull rendering, per-widget states, and the
chart pipeline. Their specs live in their own files:

Full specs now live in their own files (C0 split — each widget's spec is edited in isolation, so
widget PRs stop colliding on this doc):

- **W1 — Competitive Level graph** → [competitive-level.md](competitive-level.md)
- **W2 — Account Stats** (TypeId `pumbility`) → [pumbility.md](pumbility.md) (consumes the Projections v2 work in §5)
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
| PlayerProgress | Suggested Charts | ✅ | **SHIPPED — catalog-walk pick 1** (spec [suggested-charts.md](suggested-charts.md)). Proposed goal *"Based on recent sessions"* (2026-07-15) is scoped-but-blocked in that doc: it needs the same over/under-performance primitive the **HELD** skill-gaps goal needs, so settle that once and both unblock |
| PlayerProgress | Title Progress | ✅ | pushing title + remaining-charts math from WSIP |
| PlayerProgress | Stats snapshot | ✅ | competitive levels block (Pumbility has its own widget) |
| PlayerProgress | Recent Highlights | ✅ | `GetScoreHighlightsQuery` — the Discord-card engine, on the home page |
| PlayerProgress | Milestones | ✅ | `GetPlayerMilestonesQuery` |
| PlayerProgress | Season-recap teaser | ✅ | permanent front door to `/Player/{id}/PhoenixRecap` |
| ScoreLedger | Session Journal | ✅ | `GetRecentSessionsQuery`; score history was a top user ask |
| ScoreLedger | Chart Journey | ✅ | pin a white-whale chart, `GetChartScoreJourneyQuery` sparkline |
| ScoreLedger | To-Do List | ✅ | must survive WSIP (parity phase) |
| ScoreLedger | Quick Record | ✅ | **SPEC'd — catalog-walk pick (§4.2)**; ChartSelector + a purpose-built record row (not EditChartGrid); the arcade widget |
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
| WeeklyChallenge | Daily Step | ✅ | D12 — shipped: per-mix daily board + Limbo Day + widget ([daily-step.md](../daily-step.md)) |
| OfficialMirror | Import Scores | ✅ | **BUILT** — 1x1 credentialed import (background job + remembered password, [import-widget-remember-password.md](../import-widget-remember-password.md)); XX/older route to the spreadsheet upload |
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

### 4.2 Quick Record (catalog-walk pick — the arcade widget)

The manual recorder's widget: search a chart, punch the score, Save, on to the next. One
`UpdatePhoenixBestAttemptCommand` per save — the same write path the ChartDetailsDialog uses. Owner
field-test (2026-07-12, one round) locked it deliberately tiny. Mock:
[artifact](https://claude.ai/code/artifact/5d4bce53-a5e1-4610-8af2-f62fcea24436).

**Locked decisions (owner, 2026-07-12):** (1) **no recent-charts shortcut** — replaying a chart you just
played doesn't mean re-keying a score every time; (2) **Plate + Broken inline** on the action row;
(3) **always clears to Search after Save** — rapid-fire, no toggle; (4) **1×1 only**; (5) **pure
recorder** — the chart display is not clickable, no ChartDetailsDialog, no navigation of any kind.

- **Composition**: reuses `ChartSelector` (its `S18`/`D22` shorthand is already built in) as the
  persistent top row; the record inputs are a **purpose-built compact row, NOT `EditChartGrid`**. The
  ChartDetailsDialog already hit this wall at round 11 — *"EditChartGrid brought duplicate
  To-Do/favorite buttons and a layout that fell apart on phones"* — so Quick Record borrows the
  selector but rebuilds score/plate/broken to fit the ~114px of body a 1×1 gives. Save dispatches
  `UpdatePhoenixBestAttemptCommand(chartId, isBroken, score, plate, Mix)`.
- **Data — all existing ScoreLedger contracts; no new query, table, or vertical**: the chart list comes
  from the shell's `ChartCatalogCache` (shared per mix, no per-widget `GetChartsQuery`) and is passed
  into `ChartSelector` via its `Charts` param; on pick, `GetPhoenixRecordQuery(chartId, mix)` prefills
  the current best so it's an edit, not a blank; Save writes through the existing best-attempt command.
- **States** (one 1×1 frame, three faces):
  - **Search** (resting) — the selector (placeholder) + a shorthand hint + a muted "Posts to {Mix}"
    footer (the mix cascade D13 is real; the recorder should see which board it writes to).
  - **Recording** — the selected chart's identity (art + `DifficultyBubble` + clear ✕, **no song name**)
    rides up into the host **title bar** via the header slot (§2.3, owner 2026-07-13), so the body opens
    straight into a half-width tabular Score field beside a **live-derived letter-grade badge** (display
    only — the domain derives the grade from the score, we send score + plate + broken) and a compact
    **plate/result dropdown** (shorthand: RG…PG). Save sits on its own row so it never clips on narrow
    widths. **Broken has no checkbox** (owner, 2026-07-13): the dropdown carries a `Broken` shortcut at
    top (→ broken, no plate — the clean-fail path), and **clicking the grade icon toggles broken without
    clearing the plate** — the subtle, undocumented-by-design path to a *plated broken* grade (a fail
    whose accuracy still earned a plate; the owner captures those). Both write the same
    `UpdatePhoenixBestAttemptCommand`. Legacy/XX keeps its own Broken checkbox (no plate dropdown there).
  - **Saved** — an inline success flash ("Recorded · SS+ 985,320 · Marvelous") for ~1.4s, then
    **always** back to Search. The `UpdatePhoenixBestAttemptCommand` Snackbar is suppressed in-widget to
    avoid a double confirmation.
- **Grade badge**: score → Phoenix letter grade via `PhoenixLetterGrade`'s own thresholds (feedback
  only — never sent). Colored off the rarity ramp through `ThemeScales`/tokens (SSS→sapphire, SS/S→gold,
  AAA/AA→emerald, A/B→silver, C/D/F→common) — never a hand-rolled color.
- **Validation / edges**: Save is disabled until a score is entered (score is the minimum; plate/broken
  optional). Broken defaults **off** — a hand-keyed record is usually a pass. An empty score never sends
  a record *removal* — that deletion path stays the dialog's job, not the arcade widget's.
- **Sizes**: **1×1 only** — `SupportedSizes = [1×1]`, `DefaultSize = 1×1`. The one widget whose
  per-widget size list is a single entry.
- **Config v1**: **`Mix` scope** — `Follow current mix` / Phoenix / Phoenix 2 / **All mixes**, plus a
  **Remember last mix** toggle shown only under All mixes (owner, 2026-07-12/13). `ClearAfterSave` was
  considered and dropped — it always clears.
- **EditMode**: inputs render disabled while the board is being arranged (no accidental writes
  mid-drag); the selector shows its resting frame. Grid data refresh is already paused in edit mode by
  the shell (§2.3).
- **Lifecycle (§2.3)**: skeleton = the selector frame at fixed footprint; empty state = the search box
  IS the CTA (logged-out → "Sign in to record"); isolated error = a failed save shows an inline retry,
  board stays up; persisted vs transient = the half-typed entry is session-only, only Save writes;
  config + version = `{ mix }`, version 1.
- **Mixes & scoring (owner, 2026-07-12/13)**: the widget records to **every mix** (`SupportedMixes =
  all`). "Follow current mix" honours whatever the site is set to — legacy included; there is **no clamp
  to Phoenix** (that clamp was the "always snaps to Phoenix" bug). The record row adapts by
  `UsesLegacyScoring()`: Phoenix / Phoenix 2 → score + derived grade + plate →
  `UpdatePhoenixBestAttemptCommand`; **XX and older → score + manual `XXLetterGrade` + broken, no plate
  → `UpdateXXBestAttemptCommand`** (the existing legacy path, reused). The **active mix rides the header
  slot** (§2.3) as a chip — the old "Posts to {mix}" footer is gone. The **legacy row** is
  purpose-built (owner, 2026-07-13). In legacy the **letter grade is the primary datum** (players
  record grades, not scores — the score is optional), so the **grade image strip leads** and Score
  drops to the second line. The strip = the common grades **SSS / SS / S / A** as tappable art (the same
  `letters/*.png` CDN the rest of the app uses — the *only* place XX grades render as images; elsewhere
  they're text) with a **"…" more menu** for the rare B-and-lower records. Tap a grade to pick it
  (passing, it highlights); tap the selected one again to mark it **broken** (its art goes cracked) —
  one control replaces the grade select AND the Broken checkbox, the same gesture as the Phoenix side.
  Row 2 = the optional Score field + Save (right-aligned). Legacy `CanSave` needs only a grade.
- **All-mixes flow (owner, 2026-07-13)**: (1) no mix → the body shows **only the picker**; (2) pick a mix
  → the mix chip + a change-✕ appear in the header, body switches to the chart selector; (3) pick a chart
  → art + bubble join the header, body is the inputs. The header **✕ clears one step back** — the chart
  first (keep the mix, rapid-fire the same board), then the mix (back to the picker). A per-instance
  **Remember last mix** toggle stores the pick in UserSettings (keyed by widget id) and **auto-jumps**
  straight to it next time instead of the picker.
- **Shell extensions: none.** Quick Record is the first catalog widget that needs nothing new from the
  shell — no `DrawerPresets`, no `DynamicNameKey`, no `RefreshIcon`, and it never raises `OnChartClick`
  (it declares the five render-contract params and ignores `OnChartClick`/`RefreshToken`).
- **Not in scope**: favorites / To-Do (the dialog and EditChartGrid own those), score history (the
  Session Journal and Chart Journey widgets own that), bulk entry (the Upload pages own that).
  Deliberately one chart, one score, fast.

## 5. Pumbility Projections v2 — **PR #1**, ships before the shell (D17)

Bundled with the SkillRating history capture into one small PR the owner merges fast — trend data starts
accruing immediately, and v2 goes live on the existing `/Pumbility` page (including enabling Phoenix 2
there) before the widget exists.

**Current algorithm** ([PumbilityProjectionSaga](../../../ScoreTracker/ScoreTracker.PlayerProgress/Application/PumbilityProjectionSaga.cs)):
cohort = players within ±1 competitive level per chart type; my expected score on a candidate chart =
cohort score distribution interpolated at my average percentile across played charts in that (type, level)
slice (×0.95 damping); expected pumbility assumes an EG plate; gains measured against the lowest of ONE
mixed top-50 pool. Phoenix 2 is explicitly disabled — the in-code TODO defers the Phoenix 2 projection to
"the What-Should-I-Play overhaul," i.e. this project. (Since 2026-07-13, Phoenix 2 uses the same single
mixed pool as Phoenix.)

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
4. While in there: `PlayerRatingsImprovedEvent.NewTop50` is the overall PUMBILITY (`SkillRating`) that
   feeds the P1 history capture in this same PR. *Corrected 2026-07-13: `SkillRating` is the merged
   top-50, NOT `SinglesRating + DoublesRating` — the same variable feeds the stats record and the
   event, so map `NewTop50` straight through.*

**Tests**: pool math + proficiency adjustment as pure DomainTests; saga component tests with mocked
readers over a fixed cohort fixture; the skill-deviation query handler gets tests pinning equivalence
with `TierListBlendBuilder`'s computation (same inputs → same deviations — #140's handler tests already
pin the builder's behavior from the tier-list side).

### PR #1 commit plan

| P | Content |
|---|---|
| P1 | **History capture**: `SkillRating` column on the history entity + migration; `PlayerRatingRecord` field; `PlayerHistorySaga` maps `NewTop50`; repository read/write mapping; NewTop50 ≡ `SkillRating` (merged top-50); DATABASE-SCHEMA row |
| P2 | **Skill-deviation query**: expose `TierListBlendBuilder`'s skill computation (post-#140 home of the machinery) through a published `GetPlayerSkillDeviationsQuery`; equivalence tests pin same-inputs-same-outputs |
| P3 | **Pools abstraction** in `PumbilityProjectionSaga`: Phoenix and Phoenix 2 both a single mixed pool (P2 corrected 2026-07-13 — was two S+D pools); DomainTests for the pool math |
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
