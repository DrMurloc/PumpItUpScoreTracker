# An SEO-friendly site — what static rendering actually costs

> **STATUS: INCOMPLETE. This is a findings dump, not a plan.**
> Everything below was established by reading shipped source on 2026-07-15, after Stage 2
> (`20c6b41e`) merged. What it does **not** contain is a per-page audit — 38 routable pages have
> to be looked at individually, and the owner is doing that scoping. Treat the mechanics here as
> load-bearing and the effort estimates as absent on purpose.

Companion to [render-modes.md](render-modes.md) (Stage 2, shipped) and
[static-shell.md](static-shell.md) (Stage 1, shipped). This doc is Stage 3's problem statement.

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
2. **No page carries a mode of its own** — verified, zero hits across all 38 routable pages. They
   all inherit.
3. Therefore the first step is: **drop the attribute from `Routes`, add
   `@rendermode RenderModes.Interactive` to every page.** Zero behaviour change; every page still
   a circuit. Pages then convert one at a time by deleting their own line.

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

## 4. ⚠ The head problem — unsolved, and it is the entire point

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

There is no configuration of one outlet that serves a mixed world. Candidate answers, none
verified:

- **Two outlets**, one per render mode. Only one is ever populated, because a page lives in
  exactly one mode. Plausible and cheap *if* the section registries are per-renderer as expected —
  **needs a spike, do not assume.**
- **A route-aware static head.** `ShellModelFactory` already resolves per-request state for
  `App.razor`; it could resolve title/description/OG from the route. Bespoke, but it is the one
  option that **works without any page going static** — i.e. it would fix Discord unfurls and
  search titles on their own, while the body stays a circuit. Half a loaf, available now.
- **Finish Stage 3 first**, then flip the outlet to static once no interactive pages remain.
  Correct end state; useless in the interim.

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

- **The 38 pages.** Which are genuinely static-able, which are personalized end-to-end (`/Account`,
  the upload pages, `/Dev/Populate`) and should simply stay interactive forever, and which are
  worth the conversion at all. **Owner is scoping this.** SEO value is not uniform — `/Chart/{id}`,
  `/Charts`, `/TierLists` are the crawl targets; `/Dev/Populate` is not.
- **Output caching.** The chart page is the first meaningfully cacheable page, but nothing personal
  may enter the cached HTML — the record panel and every "you" marker must be islands, and the
  signed-in path must bypass. Not designed.
- **Whether the 301 lattice/sitemap ship before or after the body is real.** The chart-details doc
  says P3 ships as ONE unit precisely to avoid pointing crawlers at empty shells; that constraint
  should be re-read once the head fix lands, because a correct head with an empty body may already
  be worth advertising.
- **Enhanced navigation.** Still off. Turning it on is its own change with its own field test
  (render-modes.md §7.1) — and static pages plus enhanced nav is the combination nobody has tried.
