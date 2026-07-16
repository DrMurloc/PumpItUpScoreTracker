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
| **`ChartVideoDisplay`** | A `MudDialog` opened by the `ChartVideoDisplayer` service event. Static: the event fires, `StateHasChanged` does nothing, no dialog. | **7 pages** |
| **`MudLayout`** | MudBlazor's drawer host. `MudDrawer` finds it by **cascading parameter**, and cascades do not cross render modes. Without it a drawer falls into normal flow. | **3 pages** — HomeDashboard, TierLists/ChartSkills, Tools/ChartRandomizer |
| **`PageDock`** | Renders `PageDock.DockContent`, a `RenderFragment` a page hands to a service. Interactive page → static host is the same boundary violation as §2. | **1 page** — Tools/ChartRandomizer |
| **Recap pointer** | A `MudDialog` with once-per-user `UiSettings` state. | all pages, one-time |

**Two things get better, not worse:**

- **The legacy-mix gate.** It currently calls `NavManager.NavigateTo` client-side. In static SSR
  that becomes a real server redirect — strictly better than a bounce after paint.
- **`NavManager.LocationChanged` + most of `OnAfterRenderAsync`** become dead weight. Enhanced
  navigation is **off** (`data-enhance-nav="false"`, render-modes.md §7.1), so every navigation is
  already a full page load and a static layout re-renders per request anyway.

**Shape of the fix:** three island components (`ChartVideoDisplay`, a recap pointer, a page-dock
host), `MudLayout` relocated into the 3 pages that actually open drawers, and the gate moved to
the server path. Islands in the same circuit share a DI scope, so the `PageDockService`
`RenderFragment` still works **provided both ends are islands**.

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

### PR-2 — the route-aware static head [M]

The §4 decision, built. Ships real crawler heads for the whole site while every body is still a
circuit — this is the PR that fixes Discord unfurls and search titles on its own.

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
- Files: the new service + record, `App.razor` head edit, one `Program.cs` DI line, E2E facts.
  Presentation-only.

### PR-3 — the flip: every page opts in, MainLayout goes static [M–L, the risky one]

§2's mechanics plus everything §3 catalogued. **Zero intended behavior change**; one site-wide
QA on its own branch, exactly like Stage 1's.

- Drop `@rendermode` from `<Routes>` (`App.razor:79`); new shared `RenderModes` class holding
  the single `new InteractiveServerRenderMode(prerender: false)` instance (today a private
  static in `App.razor`); add `@rendermode RenderModes.Interactive` to **all 58 pages**.
- **`ShellMixSeed`** interactive root ahead of `<Routes>` — the §2 mix-seed fix. `Routes.razor`
  loses its `CurrentMix` parameter/seeding role.
- **MainLayout static** (§3): `ChartVideoDisplay` → island (7 pages fire its service event);
  recap pointer dialog → island (signed-in only — its UiSettings reads/writes take the DB path,
  static-safe, but the dialog itself needs a circuit); PageDock content host → island (a page's
  `RenderFragment` crossing into it stays legal because both ends are islands on one circuit);
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
    too** — recorded hazard.
  - ⚠ **Historical triples share the canonical shape**, so they cannot live in an MVC
    controller — the same pattern registered twice is an `AmbiguousMatchException`. The **page
    itself** compares the requested triple to the resolved chart's canonical and issues the 301
    through `HttpContext.Response` — possible only under static SSR, which is *why* the cutover
    is welded to this PR.
  - **New `ChartPermalinkController`** (MVC): `/Chart/{guid}` → 301 canonical, forever;
    `/Record` + bare `/Chart` → 301 `/Charts`.
  - **In-app links → `ChartSlugs.CanonicalPath(chart)`** — full verified inventory, 5 sites:
    `ChartDetails.razor:76` (siblings), `:245` (`GoToChart`), `AppBarSearch.razor:26`,
    `SimilarChartCard.razor:101`, `Sessions/HighlightRow.razor:15`. All hold a full `Chart`.
    Caveat: a non-default-mix record yields that mix's historical URL → one 301 hop; accepted.
  - **Sitemap swap** to vanity (one line, post-PR-1 shape) + the PR-2 head resolver gains the
    vanity route, self-canonical `<link>`, and JSON-LD.
- **Output caching — this page ships it**: anonymous GETs only; vary by path + resolved culture
  + mix cookie; bypass on auth cookie; TTL from config. Spike ①: does
  `@attribute [OutputCache(...)]` on a component page flow into endpoint metadata (fallback:
  path-keyed policy/middleware). Spike ②: find the live `Cache-Control: no-cache, no-store`
  writer — prime suspect is antiforgery on the component endpoints (`UseAntiforgery`,
  `Program.cs:340`) — and neutralize it for cacheable anonymous GETs. Owner/Azure item
  (static-shell.md D18): ARR affinity cookies make first-hit responses edge-uncacheable — if the
  plan runs a single instance, disable ARR affinity.
- **Corrections to carry into chart-details-overhaul.md when syncing**: `ChartOverview` is NOT
  dead — `Charts.razor:426` still opens it; its retirement belongs to `/Charts`' own conversion.
- **FT**: view-source is real HTML; Discord unfurl shows the jacket; a GUID URL 301s.
- **Owner decision ③:** hold the vanity sitemap swap for this PR (default, the ONE-unit rule) or
  advertise correct-head-over-empty-body earlier once PR-2 lands.

### PR-5 — E2E facts + doc sync [S–M]

- E2E: anon vanity URL serves content **pre-circuit**; GUID / historical / stale-slug 301
  chains; glow + your-best render only when signed in; 404 status for unknown charts and garbage
  three-segment paths; cached-anon-HTML actually contains content.
- Docs: ARCHITECTURE / UX-GUIDELINES / API / DATABASE-SCHEMA sync; chart-details-overhaul.md P3
  + P4 rows close; this doc's ladder gets its shipped-marks.

### Open owner decisions

| # | Decision | Blocks | Default/lean |
|---|---|---|---|
| ① | Front-door canonical: `/Login` or `/Welcome` | PR-1 | **decided 2026-07-16: `/Welcome`** |
| ② | `ChartRecordPanel`: whole-panel island vs static-your-best split | PR-4 | whole-panel island |
| ③ | Vanity sitemap: wait for PR-4 (ONE unit) vs advertise after PR-2 | PR-4 timing | wait for PR-4 |
