# An SEO-friendly site — what static rendering actually costs

> **STATUS: SCOPED — §7 is the PR ladder.** §1–§6 were established by reading shipped source on
> 2026-07-15, after Stage 2 (`20c6b41e`) merged; the ladder was scoped 2026-07-16 against this
> tree, and each PR gets its full scoping as it comes up. What this doc still does **not**
> contain is the per-page audit — which of the **58** routable pages converts, and in what
> order, is the owner's scoping. Treat the mechanics here as load-bearing.

Companion to [render-modes.md](render-modes.md) (Stage 2, shipped) and
[static-shell.md](static-shell.md) (Stage 1, shipped). This doc is Stage 3's problem statement
and its plan (§7).

---

## 1. Where we actually are

Stage 1 made the **shell** real HTML. Stage 2 flipped the **hosting model**. Neither made a
single page static, and that was deliberate — Stage 2's own §3 says *"Every page stays
interactive via `<Routes>`. Nothing goes static in this stage."*

So today, a crawler hitting `/Chart/{guid}` — the page the whole overhaul was aimed at — receives:

```html
<title>PIU Scores</title>          <!-- App.razor, a literal. Identical on all 4,426 chart pages -->
<!-- no meta description, no og:title, no og:image, no JSON-LD -->
<main class="shell-main">
                                   <!-- empty -->
</main>
```

Three lines produce that, and **all three are working as designed**:

| | Where | Effect |
|---|---|---|
| `new InteractiveServerRenderMode(prerender: false)` | `App.razor` | the content region renders **nothing** server-side. Non-negotiable — prerendering is the flip that broke the site once already. |
| `<HeadOutlet @rendermode="Interactive" />` | `App.razor` | `<PageTitle>`/`<HeadContent>` exist only once a circuit connects. A crawler never sees them. |
| `<title>@L["PIU Scores"]</title>` | `App.razor` | the only title any crawler ever gets. |

**What Stage 1 did buy is real**: the nav, mega-menus, mix pill and their links are server-rendered,
so a crawler that lands anywhere sees a site and can follow it. The *page* is what is missing.

---

## 2. The blocker chain — why a page cannot go static on its own

```razor
<main class="shell-main">
    <Routes @rendermode="Interactive" CurrentMix="_shell.CurrentMix" />
</main>
```

A render mode propagates **down** and a child cannot opt back out. So:

1. **Nothing under `<Routes>` can be static** while that attribute is there.
2. **No page carries a mode of its own** — verified, zero hits across all routable pages. They
   all inherit. (Count corrected 2026-07-16: **58 page files, 66 `@page` routes** — the "38"
   this doc first recorded was wrong.)
3. Therefore the first step is: **drop the attribute from `Routes`, add
   `@rendermode RenderModes.Interactive` to every page.** Zero behaviour change; every page still
   a circuit. Pages then convert one at a time by deleting their own line.

⚠ **Plus one seed the flip silently breaks — the mix.** `UiSettingsAccessor.GetSelectedMix`
short-circuits on a `ShellContext` that only `Routes.razor` seeds *into the circuit* today
(`Routes.razor:32`, an interactive root). Once `Routes` is static, its `OnInitialized` runs in
the request scope instead — harmlessly, `ShellModelFactory` already seeded that scope — and
**every interactive page's circuit is left unseeded**: anonymous users on interactive pages
silently fall back to Phoenix, signed-in users fall back to the per-page settings query that
static-shell.md D10 killed. Static pages are unaffected. The fix rides §7 PR-3: a tiny
interactive root (`ShellMixSeed`) rendered ahead of `<Routes>`, seeding the circuit-scoped
`ShellContext` exactly as `Routes` does now — interactive roots share the circuit's DI scope
(the shell islands prove it daily) and activate in document order by the framework's sequence
contract (render-modes.md §7 Q4).

⚠ **And that step is not mechanical, because of the layout.**

`RouteView` instantiates the layout, so a static `Routes` means a **static `MainLayout`**. It
cannot be rescued by marking the layout interactive instead: `LayoutComponentBase.Body` is a
`RenderFragment`, parameters crossing into an interactive component must be serializable, and a
`RenderFragment` is not. **An interactive layout under a static router is a runtime error.**

So: **`MainLayout` must become static before any page can.** That is the real entry fee.

---

## 3. What `MainLayout` is holding, and what breaks

Verified against `Shared/MainLayout.razor`. It is `IDisposable`, subscribes to two events, and
calls `IJSRuntime` from `OnAfterRenderAsync` — none of which happens in a static component.

| What | Breaks how | Blast radius |
|---|---|---|
| **`ChartVideoDisplay`** | A `MudDialog` opened by the `ChartVideoDisplayer` service event. Static: the event fires, `StateHasChanged` does nothing, no dialog. **Retires at PR-2 instead of islanding** — callers move to `ChartDetailsDialog`, which is per-page and video-led, with no layout involvement. | **4 pages** (recounted 2026-07-16: the "7" here counted inject lines — 2 are dead injections) |
| **`MudLayout`** | MudBlazor's drawer host. `MudDrawer` finds it by **cascading parameter**, and cascades do not cross render modes. Without it a drawer falls into normal flow. | **3 pages** — HomeDashboard, TierLists/ChartSkills, Tools/ChartRandomizer |
| **`PageDock`** | Renders `PageDock.DockContent`, a `RenderFragment` a page hands to a service. Interactive page → static host is the same boundary violation as §2. | **1 page** — Tools/ChartRandomizer |
| **Recap pointer** | A `MudDialog` with once-per-user `UiSettings` state. | all pages, one-time |

**Two things get better, not worse:**

- **The legacy-mix gate.** It currently calls `NavManager.NavigateTo` client-side. In static SSR
  that becomes a real server redirect — strictly better than a bounce after paint.
- **`NavManager.LocationChanged` + most of `OnAfterRenderAsync`** die with in-circuit
  navigation. ⚠ An earlier revision of this doc claimed navigation was "already a full page
  load" — **false**: pre-flip, the interactive router swapped pages inside one living circuit
  (SPA). **The flip itself is what makes every navigation a full document load**
  (owner-accepted 2026-07-16): the static shell paints instantly from server HTML, the page's
  circuit boots per view, and enhanced navigation (render-modes.md §7.1) is the designed
  SPA-feel mitigation, parked for its own field test.

**Shape of the fix:** two island components (a recap pointer and a page-dock host —
`ChartVideoDisplay` retires at PR-2 rather than islanding), `MudLayout` relocated into the 3
pages that actually open drawers, and the gate moved to the server path. Islands in the same
circuit share a DI scope, so the `PageDockService` `RenderFragment` still works **provided both
ends are islands**.

---

## 4. The head problem — decided: a route-aware static head (§7 PR-2)

**Making the chart page static does not, by itself, give a crawler its `<HeadContent>`. It takes
it away.**

Head content flows through section outlets. An outlet only collects `SectionContent` registered
in **its own renderer** — static SSR and the circuit are different renderers with different DI
scopes. render-modes.md §7.2 states the forward direction:

> If static, it only collects head content from static components — the 54 pages' `<PageTitle>`
> and 8 pages' `<HeadContent>` stop working.

Read it backwards and that is the trap: **an *interactive* outlet collects nothing from a
*static* page.** So:

| Page | HeadOutlet | Crawler sees |
|---|---|---|
| interactive *(today)* | interactive | `PIU Scores`, no OG — head arrives only with the circuit |
| **static** | **interactive** | `PIU Scores`, no OG — **head content goes nowhere at all** |
| static | static | correct — but every still-interactive page loses its `<PageTitle>` |

There is no configuration of one outlet that serves a mixed world — so the outlet is not the
mechanism. **Decided 2026-07-16: a route-aware static head, and no outlet changes, ever.**

- A resolver in the `ShellModelFactory` mold maps the request path → title / meta description /
  OG image (later: canonical `<link>` + JSON-LD), and `App.razor` renders it statically in
  `<head>`. Unknown routes fall back to today's literal.
- **Interactive pages**: the static head serves crawlers; `<PageTitle>` takes over when the
  circuit connects — that is the shipped title-swap pattern already working on every page
  (static-shell.md §9: boot removes the marker-less title and uses its text as the fallback),
  zero new machinery. A page whose circuit also emits `HeadContent` meta duplicates it in the
  live DOM post-boot — the known-harmless case (static-shell.md §9); each duplicate retires when
  its page converts.
- **Static pages**: the route-aware head is the *only* head. The page's `<PageTitle>` /
  `<HeadContent>` become dead code, deleted at conversion.
- `HeadOutlet` stays `Interactive` permanently. The two-outlet idea dies un-spiked: one
  deterministic mechanism covers both worlds, and this one **works before any page goes
  static** — real chart titles, descriptions and Discord unfurls ship at §7 PR-2 while every
  body is still a circuit.

---

## 5. Why this cannot be verified the usual way

**bUnit does not model render modes.** A render-mode boundary violation — a `RenderFragment`
crossing, a cascade that no longer arrives, a `MudDrawer` that lost its container — is a
**runtime** error. The fast suites will stay green through all of it.

The only real verification is `ScoreTracker.Tests.E2E` (Kestrel-hosted app, Testcontainers,
headless Chromium): `StaticShellTests`, `HomeDashboardTests`, `ScoreImportTests`,
`PiuGameLoginTests`, `FrontDoorDispatcherTests`, `NonComponentEndpointTests`. Anything Stage 3
touches that E2E does not cover is unverified until the owner field-tests it.

**Plan around that**, not against it: land the layout change on its own branch off `main` so one
QA covers one risk, and so a second pass on the layout does not strand unrelated work behind it.

---

## 6. What this doc does NOT know

- **The 58 pages.** Which are genuinely static-able, which are personalized end-to-end (`/Account`,
  the upload pages, `/Dev/Populate`) and should simply stay interactive forever, and which are
  worth the conversion at all. **Owner is scoping this.** SEO value is not uniform — `/Chart/{id}`,
  `/Charts`, `/TierLists` are the crawl targets; `/Dev/Populate` is not. One audit criterion
  already found: a page that reads UiSettings for *anonymous* users goes through
  `ProtectedLocalStorage` — circuit-only JS interop — so its anon path must stay in an island or
  change stores (the per-page density prefs are the known case).
- **Output caching** — now scoped into §7 PR-4: the policy shape (anonymous GETs only; vary by
  path + resolved culture + mix cookie; bypass on auth cookie; TTL from config) plus two spikes
  (does `@attribute [OutputCache]` flow into component endpoint metadata; what writes the
  observed `Cache-Control: no-cache, no-store` — prime suspect is antiforgery on the component
  endpoints) and the ARR-affinity owner item (static-shell.md D18).
- **Whether the 301 lattice/sitemap ship before or after the body is real** — now owner decision
  ③ in §7: the default stays the chart-details doc's ONE-unit rule (the vanity sitemap waits for
  PR-4); the PR-2 head makes "correct head over an empty body" possible earlier if wanted.
- **Enhanced navigation.** Still off. Turning it on is its own change with its own field test
  (render-modes.md §7.1) — and static pages plus enhanced nav is the combination nobody has tried.

---

## 7. The PR ladder

Scoped 2026-07-16 against this tree. **Ship order is the numbering**; the true dependency graph
is looser — PR-1, PR-2 and PR-3 are mutually independent, PR-4 needs 2+3, PR-5 needs 4. Each PR
gets a full scoping pass when it comes up; the entries below are everything known now, written
so a cold session can pick one up. Sizes: S/M/L.

### PR-1 — sitemap validity, robots.txt, front-door canonical [S]

The sitemap Google flags as invalid is one bug: `SitemapController.cs:33–39` creates `urlset`
in the sitemap namespace but its children with `new XElement("url", …)` — LINQ-to-XML children
do **not** inherit the parent's namespace, so every entry serializes as `<url xmlns="">`,
escaping the namespace. That is Google's "needs a namespace" complaint, verbatim. Also true
today: no XML declaration (`XElement.ToString()` never emits one), no `robots.txt` anywhere, and
the front door's two routes (`/Welcome`, `/Login` — same page, `Program.cs:51`) serve identical
HTML with no `rel=canonical` between them while anonymous `/` 302s into them (`Program.cs:347`).

| File | Change |
|---|---|
| `Controllers/SitemapController.cs` | `XNamespace` on **every** element; emit `<?xml version="1.0" encoding="UTF-8"?>` (`XDocument` + UTF-8 `StringWriter`); `application/xml; charset=utf-8`; add the front-door canonical `/Welcome`; swap the direct `IChartRepository` injection to constructor `IMediator` + Catalog's `GetChartsQuery` (the direct port injection is a pre-existing Web-convention violation, and PR-4 rewrites this controller anyway) |
| `wwwroot/robots.txt` — **new** | `Sitemap:` pointer (discovery today is Search-Console-only) + `Disallow` for `/hangfire`, `/api/`, `/Dev/`, `/Admin` crawl waste; served by `UseStaticFiles` as-is |
| `Pages/FrontDoor.cshtml` | `<link rel="canonical">` to `/Welcome`; meta description + OG title/description/type/url — the description reuses the hero pitch-line key, already localized ×9, so zero new resx entries. No `og:image`: wwwroot's only image is the favicon; a share-card asset is a later nicety |
| `Tests.E2E/NonComponentEndpointTests.cs` | strengthen the sitemap pin (today only `Contains("<urlset")`): parse the body, assert every element is in the sitemap namespace (no `xmlns=""`), declaration present, front-door URL listed; add a robots.txt fact |
| `Tests.E2E/FrontDoorDispatcherTests.cs` | pin the canonical tag on `/Login` and `/Welcome` |
| `Program.cs:49` | comment correction only — "the front door owns /" is stale (it owns `/Welcome`) |

Chart pages **stay GUID URLs** here; the vanity swap is PR-4's, welded to real HTML. Entirely
presentation-layer; no contracts, no migrations, no new resx keys. **Decision ① (owner,
2026-07-16): `/Welcome` is canonical — it describes the page better.** `/Login` keeps serving
the same HTML and canonicalizes to it; the sitemap lists only `/Welcome`. **Owner post-deploy:**
resubmit the sitemap in Search Console.

### PR-2 — the route-aware static head + the video-dialog retirement [M]

Two independent halves packed into one PR (owner call, 2026-07-16: minimize PR count). Half A
is the §4 decision, built — real crawler heads for the whole site while every body is still a
circuit; the PR that fixes Discord unfurls and search titles on its own. Half B retires
MainLayout's video dialog, shrinking PR-3's blast radius before the flip exists.

- **New** `Services/StaticHeadResolver.cs` (name at impl): request path → head model (title,
  description, OG title/image; canonical + JSON-LD deliberately deferred to PR-4, where the
  canonical namespace settles). Scoped service beside `ShellModelFactory`; `App.razor` renders
  the result in `<head>` in place of the literal `<title>` (which becomes the fallback for
  unmatched routes).
- **First resolver: chart routes** (`/Chart/{guid}` now; the vanity shape joins at PR-4). Data
  through existing cached reads — `ChartUrlResolver`'s 1h `GetChartsQuery` cache for identity,
  `GetChartVerdictQuery` (daily-cached) if the description goes verdict-flavored; otherwise the
  page's current `MetaDescription` shape. Nothing new below Web.
- Candidate cheap follow-ons, same mechanism: `/TierLists/{type}/{level}` folder titles, the
  calculators. Each is a resolver case, not a design question.
- **Verification is E2E-only by nature** (the head is `App.razor`'s document — bUnit renders
  components, not the document): anon `GET /Chart/{guid}` body contains the chart-named
  `<title>` and `og:image` **before any circuit**, StaticShellTests-style.
- Files (half A): the new service + record, `App.razor` head edit, one `Program.cs` DI line,
  E2E facts. Presentation-only.

**Half B — retire `ChartVideoDisplay` into `ChartDetailsDialog`** (verified inventory,
2026-07-16):

- The old dialog's live callers are **4 pages / 5 buttons**, all the same YouTube-icon-per-row
  gesture: `Charts.razor:284`, `Pumbility.razor:65+168`, `WeeklyCharts.razor:104`,
  `MatchTournamentQualifiersSubmit.razor:134`. `ChartSkills.razor:30` and
  `CompletionLeaderboards.razor:114` inject the service and never call it — dead injections,
  free deletions.
- `ChartDetailsDialog` **leads with the video** (its design brief: "leads with the video like
  today's video dialog"), is hosted **per page** (`@bind-Visible` + `Chart`/`Mix` params,
  pattern live on HomeDashboard/ChartSkills/ChartRandomizer/RandomizerSpectate), and renders
  through `MudDialogProvider` — zero MainLayout involvement. Flipping a caller = host an
  instance + set two fields in the click handler.
- **Port Report Video** into `ChartDetailsDialog`'s video block (the one feature the old dialog
  has that the new one lacks — `IAdminNotificationClient.NotifyAdmin` + snackbar; the resx keys
  exist in all locales). Every dialog surface gains reporting as a side effect.
- `Charts.razor` rows are `BestAttemptDto` (ChartId only) — that one call site needs a chart
  lookup on click; the other three hold full `Chart` records.
- **Deletes**: `Components/ChartVideoDisplay.razor`, `Services/ChartVideoDisplayer.cs`, the
  MainLayout host line, the `Program.cs` registration, both dead injections. With them dies a
  real per-page-load cost: the old dialog loads the **entire video catalog** into a dictionary
  on every circuit (`GetChartVideosQuery()` in MainLayout's tree); the new dialog fetches one
  chart's video lazily.
- ⚠ `WeeklyCharts.razor` is mid-overhaul on the challenges-hub branch — expect a trivial merge
  conflict there.
- Pure circuit-world refactor (every page is still interactive), so today's suites verify it —
  bUnit for the report affordance, existing dialog coverage keeps passing.
- **Effect on PR-3**: MainLayout's islands drop from 3 to 2, and the fiddliest one — an
  event-driven dialog fed by a scoped service across roots — is deleted rather than migrated.

### PR-3 — the flip: every page opts in, MainLayout goes static [M–L, the risky one]

§2's mechanics plus everything §3 catalogued. **Zero intended behavior change**; one site-wide
QA on its own branch, exactly like Stage 1's.

- Drop `@rendermode` from `<Routes>` (`App.razor:79`); new shared `RenderModes` class holding
  the single `new InteractiveServerRenderMode(prerender: false)` instance (today a private
  static in `App.razor`); add `@rendermode RenderModes.Interactive` to **all 58 pages**.
- **`ShellMixSeed`** interactive root ahead of `<Routes>` — the §2 mix-seed fix. `Routes.razor`
  loses its `CurrentMix` parameter/seeding role.
- **MainLayout static** (§3): two islands — the recap pointer dialog (signed-in only; its
  UiSettings reads/writes take the DB path, static-safe, but the dialog itself needs a circuit)
  and the PageDock content host (a page's `RenderFragment` crossing into it stays legal because
  both ends are islands on one circuit). `ChartVideoDisplay` is already gone — PR-2 retired it.
  `MudLayout` relocates into the 3 drawer pages (HomeDashboard, TierLists/ChartSkills,
  Tools/ChartRandomizer); the legacy-mix gate's `NavigateTo` becomes a real server redirect
  (static SSR turns `NavigationManager.NavigateTo` into an HTTP redirect — strictly better than
  today's bounce-after-paint); `LocationChanged` + the `OnAfterRenderAsync` dock re-sync die
  (enhanced nav is off; every navigation is a full page load).
  - MainLayout's `<PageTitle>` (line 13) dies harmlessly: pages without their own title fall to
    the boot fallback, which **is** the static title's text.
  - `MudContainer` is presentational and static-safe; it stays.
- **Real 404s**: the static router's `NotFound` branch sets `Response.StatusCode = 404` — today
  every garbage URL 200s an empty shell (soft-404 crawl waste).
- **Ratchet**: new architecture test — every `@page` file in `Pages/` declares a `@rendermode`,
  shrink-only allowlist for pages that have converted to static. bUnit cannot see render modes
  (§5), so "a new page forgot to opt in" must fail as a file scan, not a rendering test.
- **Verification**: the fast suites stay green through every render-mode violation (§5). E2E
  full pass + the owner FT matrix are the actual gate.

**Shipped 2026-07-16, with four reality corrections found by building:**
- **The navigation model changes — this is the flip's one user-visible cost.** The §2 mechanics
  above were right; the "zero behavior change" banner was not: killing the interactive router
  ends SPA navigation, and every in-app click is a full document load now (accepted; enhanced
  nav is the chase).
- **"Every navigation" meant link clicks.** `data-enhance-nav="false"` does not reach
  programmatic `NavigateTo` from an interactive island, which performs an enhanced page load
  (fetch + DOM patch, same document) — so a page that called `NavigateTo` to sync its own URL
  re-fetched and re-initialized itself (render-modes.md §7.1 has the measurements). The
  tier-list pages now write the URL bar through `history.pushState`/`replaceState` instead;
  `NavigateTo` means leaving the page.
- **Real 404s came free**: the static router answers unmatched routes itself — real 404, empty
  body — and the Router's `NotFound` fragment never renders under it. The planned status-setting
  component was dead on arrival and deleted; a branded not-found page is `NotFoundPage` polish,
  recorded as follow-up.
- **`MudLayout` relocated per drawer, not per page**: all six drawers are `Temporary`
  (fixed-position), so each wraps itself in a zero-footprint container where it stands.
Four layout-contract E2E pins (dock, drawer, gate redirect, anon-mix-reaches-circuit) were
landed green against the pre-flip router first, then held through the flip.

### PR-4 — ChartDetails static + output caching + the URL cutover [L — ships as ONE unit]

The chart-details doc's P3, sharpened against this tree. The page is already rebuilt with every
circuit-needing section its own child, so the conversion is mostly render-mode lines — the real
work is caching and the lattice.

- **Page**: delete its `@rendermode` line. Islands: `ChartVideoPlayer`, `ChartRecordPanel`,
  `ChartEvidenceSection` (Apex), `ChartLeaderboardSection`, `SimilarChartsShelf`'s controls
  (cards are already real `<a href>`s). The finder empty-state (`ChartSelector`) dies with the
  bare `/Chart` + `/Record` routes. Unresolvable chart → 404 status via PR-3's mechanism.
- **Skeletons**: the settled `data-island-ready` pattern (one wrapper CSS rule,
  `:has([data-island-ready])`). ⚠ Verified wrinkle: `ChartRecordPanel` renders **nothing** for
  anonymous users (`ChartRecordPanel.razor:23`), and an island that never renders never signals
  ready — so its wrapper + skeleton must sit inside a server-side `@if` on request auth (the
  static page reads `HttpContext`). **Owner decision ②:** v1 keeps the whole panel as the island
  (the shipped comment's intent — everything in it is personal and cache-excluded anyway) vs.
  the design doc's static-your-best-for-signed-in split. Default: whole-panel island.
- **Token audit** (verified): `SkillCoverageBars` carries 1 `--mud-*` var and renders in a
  static section — fix; then a pass over the `.chart-*` CSS rules. `DifficultyBubble`,
  `ScoreBreakdown`, `UserLabel`, `SongImage` came up clean.
- **The URL cutover** (machinery built dark at B4 — `ChartSlugs`, `ChartUrlResolver`):
  - Vanity `@page "/{MixSlug}/{SongSlug}/{DifficultySlug}"` on the page; `/Chart/{guid}`,
    `/Chart`, `/Record` leave it. ⚠ Component routes take no custom constraints, so the vanity
    route is the site's **three-segment catch-all**: every existing 3-segment route out-ranks it
    on literal segments (verified across the route table), and **any future 3-segment route must
    too** — recorded hazard. (Dissolves entirely if decision ④ mounts the lattice under
    `/Charts/…`.)
  - **Slug rules from the 2026-07-16 full-catalog audit** (chart-details-overhaul.md, "The slug
    audit"): the canonical namespace measured collision-free; `DifficultySlug` goes slot-aware
    (`crazy-6` when `LegacySlot` exists), `SlugifySong` gains the unslugifiable-name fallback
    (the song `!`), and historical resolution picks **deterministically** among the 48
    legacy duplicate-row paths (losers stay reachable by GUID forever).
  - ⚠ **Historical triples share the canonical shape**, so they cannot live in an MVC
    controller — the same pattern registered twice is an `AmbiguousMatchException`. The **page
    itself** compares the requested triple to the resolved chart's canonical and issues the 301
    through `HttpContext.Response` — possible only under static SSR, which is *why* the cutover
    is welded to this PR.
  - **New `ChartPermalinkController`** (MVC): `/Chart/{guid}` → 301 canonical, forever;
    `/Record` + bare `/Chart` → 301 `/Charts`.
  - **In-app links → `chart.CanonicalPath()`** — `ChartSlugs.CanonicalPath` becomes a
    `this Chart` extension (owner call 2026-07-16: canonical links sitewide; the GUID permalink
    demotes to sharing/debugging/external-tool duty). Full verified inventory, 5 sites:
    `ChartDetails.razor:76` (siblings), `:245` (`GoToChart`), `AppBarSearch.razor:26`,
    `SimilarChartCard.razor:101`, `Sessions/HighlightRow.razor:15`. All hold a full `Chart`.
    Caveat: a non-default-mix record yields that mix's historical URL → one 301 hop; accepted —
    that is the "mix-specific link" case, and the mix segment makes it resolve correctly.
  - **Sitemap swap** to vanity (one line, post-PR-1 shape) + the PR-2 head resolver gains the
    vanity route, self-canonical `<link>`, and JSON-LD.
- **Output caching — investigated, then split out (see K4 finding below).**
- **FT**: view-source is real HTML; Discord unfurl shows the jacket; a GUID URL 301s.
- **Owner decision ③:** hold the vanity sitemap swap for this PR (default, the ONE-unit rule) or
  advertise correct-head-over-empty-body earlier once PR-2 lands.

**Shipped 2026-07-16 as K1–K3 + K5 (branch `claude/chart-page-static-url-cutover`); K4 caching
split out.** The ONE-unit rule is satisfied — vanity URLs, real HTML, and the 301 lattice ship
together, so a pretty URL never points at an empty body. What landed:
- **K1** slug rules — `/Charts/{mix}/{song}/{difficulty}` (decision ④), `chart.CanonicalPath()`
  extension, slot-aware `DifficultySlug`, `!`→`exclamation` fallback, deterministic dup-row
  tiebreak (lowest chart id owns the URL).
- **K2** the page goes static, five islands, the shelf splits into a crawlable
  `SimilarChartsStaticGrid` + the interactive island (`data-island-ready` swap). **⚠ Trap caught
  by building**: `MudTooltip` renders a popover that **throws in static SSR**, and
  `DifficultyBubble`/`SongImage` wrapped one unconditionally — a lone-chart page dodged it
  (no siblings, empty graph) but a chart *with* siblings 500'd. Fix: gate the tooltip on
  `RendererInfo.IsInteractive` (tooltip in a circuit, bare image/bubble static). Same fix the
  weekly-charts branch needs on the same shared components — coordinate the merge. bUnit needs
  `RendererInfo` set (`BunitInteractive.RenderInteractive()`, called *last* — reading the
  renderer locks the service collection).
- **K3** the cutover — page-issued 301 for historical/stale/**mixed-case** slugs (case-sensitive
  canonical compare normalizes casing in one hop; **proven that a static SSR page CAN emit a
  real 301**, not just a 302), `ChartPermalinkController` for GUID/`/Record`, in-app links via
  `chart.CanonicalPath()` (4 live sites — `GoToChart` died with the finder in K2), sitemap
  swapped to vanity, `<link rel="canonical">` + `MusicRecording` JSON-LD in the static head.
- **K5** E2E (`ChartUrlCutoverTests`, auto-redirect off — every 301 asserted directly) + this
  sync. Suites: Tests 1347 / Api 60 / Components 173 / E2E 37.

**K4 — output caching, FINDING + DEFERRED to its own verified follow-up.** Baseline captured
2026-07-16 (E2E header dump of the live canonical page): the anonymous chart GET returns
`Cache-Control: no-store, no-cache, max-age=0` **and** `Set-Cookie: .AspNetCore.Antiforgery.*`
(the component endpoint's antiforgery token) plus the first-hit culture cookie. Two blockers,
both framework-level: (1) `OutputCache` refuses to store any response carrying a `Set-Cookie`,
so the antiforgery cookie alone defeats caching; (2) the `no-store` is emitted by the razor
component endpoint, not app code. Neutralizing the antiforgery cookie safely — without weakening
antiforgery on the many pages that *do* post forms — is a real spike, and getting the cache-vary
or auth-bypass wrong risks serving one user's personalized chart page to another. **Split out
because**: caching changes no URL and breaks no crawler (fully separable from the SEO cutover);
it carries a correctness/security dimension that warrants its own review; and it needs owner
field-verification (it's the fix for the first-load latency noticed after PR-3). The follow-up
owns: the anon-GET output-cache policy (vary by path + culture + mix cookie, bypass on the
`DefaultAuthentication` cookie, TTL config `OutputCache:ChartPageSeconds` ≈ 600), a targeted
antiforgery-cookie/no-store suppression scoped to anonymous 3-segment `/Charts/*` GETs, an E2E
matrix (warm anon GET cacheable + no `Set-Cookie` / signed-in bypasses / form posts elsewhere
still validate), and the ARR-affinity owner item (static-shell.md D18).
- **Correction carried into chart-details-overhaul.md**: `ChartOverview` is NOT dead —
  `Charts.razor` still opens it; its retirement belongs to `/Charts`' own conversion.

### PR-5 — E2E facts + doc sync [S–M]

- E2E: anon vanity URL serves content **pre-circuit**; GUID / historical / stale-slug 301
  chains; glow + your-best render only when signed in; 404 status for unknown charts and garbage
  three-segment paths; cached-anon-HTML actually contains content.
- Docs: ARCHITECTURE / UX-GUIDELINES / API / DATABASE-SCHEMA sync; chart-details-overhaul.md P3
  + P4 rows close; this doc's ladder gets its shipped-marks.

### SERP appearance pass (2026-07-17)

Google's first indexed chart page exposed how a result actually *reads* ("263Scores tracked
92%Pass rate…" — value/label spans fused, description too thin to be quoted alone, site named
"arroweclip.se"). The appearance layer, landed as one pass:

- **Verdict-flavored description** — the PR-2 option, now real: `StaticHeadResolver` folds the
  daily-cached `GetChartVerdictQuery` population into the description ("263 scores tracked, 92%
  pass rate"), making each chart's description unique and substantial enough that engines quote
  it instead of stitching page text. The page dispatches the same query later in the same
  request, so the head *warms* the verdict cache rather than doubling the work; the daily cache
  is also what keeps descriptions stable between analytics rebuilds.
- **`data-nosnippet` on the hero fact tiles** — value/label tiles are label soup to a text
  extractor; the attribute keeps them out of search snippets (the description now carries those
  stats as prose), and value/label sit on separate source lines so any other extractor
  word-breaks cleanly. Verdict sentences stay snippetable on purpose — they read well.
- **Branded document title** — `{Song} {Diff} | PIU Scores`, suffix applied in App.razor only:
  the head model's Title stays the page's own text, so a future circuit `PageTitle` swap can't
  flash the brand away. og:title stays bare; `og:site_name` carries the brand for unfurlers.
- **JSON-LD grew to a graph** — `MusicRecording` (song name + `byArtist`, no longer the chart
  title) + `BreadcrumbList` (Charts › {Song} {Diff}), which replaces raw URL slugs
  ("Charts › phoenix › d23") as the result's displayed trail. **Landing it exposed a latent
  K3 bug**: the static renderer silently drops a `<script>` element whose content is a
  component expression, so the PR-4 MusicRecording script never actually reached a crawler
  (live-site confirmed — canonical `<link>` from the same `@if` served, script absent). Fix:
  the whole element rides one `MarkupString`; the new E2E fact pins `ld+json` in the raw body
  so it can't regress silently again.
- **Site name** — the front door serves `WebSite` JSON-LD (`name: "PIU Scores"`) +
  `og:site_name` on `/` / `/Welcome` / `/Login`: the documented signal for showing
  "PIU Scores" instead of the bare domain above results. Site names for subdomains are
  supported but slow to take — after this markup, the lever is patience, not more markup.
- **og:image was already there — its MIME was not.** Chart pages have served the jacket
  banner as og:image since PR-2 (700×393 piugame art, a near-ideal large-card aspect). What
  was broken: `AzureBlobFileUploadClient` never set a content type, so every app-uploaded
  blob — song jackets, the tier-list folder cards — serves `application/octet-stream`, which
  some unfurlers and crawlers refuse to render as an image. The uploader now stamps the type
  from the extension; **existing blobs need a one-time owner-side content-type stamp**
  (`Downloads\stamp-piuimages-content-types.ps1`; remember a CDN purge after — cached
  octet-stream responses outlive the stamp). Head grew `og:url`, `og:image:alt`, and
  `twitter:card = summary_large_image`. **Second latent bug found in the same client**: the
  plain `UploadAsync(Stream)` overload throws `BlobAlreadyExists`, so the daily
  `refresh-folder-share-cards` job faulted on its first folder every run after its first —
  all 52 tier-list og:image cards were created 2026-07-12 and never refreshed (blob
  creation == modification for every card; unfurls still show that date). `UploadFile` now
  overwrites — save-semantics, which is what every caller wants (the card refresh, UCS
  photo re-submission; photo paths are fresh GUIDs and the avatar mirror guards itself
  with `DoesFileExist`). Every upload path was audited: all blob writes ride
  `IFileUploadClient` — the fixed client is the only `BlobContainerClient` in the repo.
- **Front-door title + snippet hygiene** — the title (and og:title) gained the searchable
  descriptor: `PIU Scores — Pump It Up score tracker & tier lists`, one localized key ×9
  (each locale reuses its own established phrasing from the hero keys; the `WebSite`
  JSON-LD `name` stays the bare brand — that's the site-name signal). `data-nosnippet`
  covers the sign-in column and the showcase cards' illustrative numbers (fused spans like
  "Moonlight998,404" were snippet-eligible), leaving the hero pitch, card blurbs, and the
  live stat band as what a result quotes.

### Open owner decisions

| # | Decision | Blocks | Default/lean |
|---|---|---|---|
| ① | Front-door canonical: `/Login` or `/Welcome` | PR-1 | **decided 2026-07-16: `/Welcome`** |
| ② | `ChartRecordPanel`: whole-panel island vs static-your-best split | PR-4 | **decided 2026-07-16: whole-panel island** (static-your-best split stays a recorded follow-up) |
| ③ | Vanity sitemap: wait for PR-4 (ONE unit) vs advertise after PR-2 | PR-4 timing | wait for PR-4 |
| ④ | Canonical mount: bare `/{mix}/{song}/{diff}` vs `/Charts/{mix}/{song}/{diff}` | PR-4 | **decided 2026-07-16: `/Charts/{mix}/{song}/{diff}`** — contains the namespace under a literal, no catch-all |
