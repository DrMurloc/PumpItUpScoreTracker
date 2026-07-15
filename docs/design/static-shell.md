# The Shell — design & implementation spec

Stage 1 of the SSR/islands migration. Decided in the 2026-07-14 workshop, re-scoped
2026-07-14 after auditing every claim in the original draft against the code.

**Where this doc and the code disagree, the code wins — but update this doc in the same commit.**

## Stage map

The site renders with `render-mode="Server"` and no prerendering: every page ships an ~8KB
empty shell and renders inside the circuit. Crawlers, link unfurlers and LLM readers get
nothing. Three stages move it to SSR-by-default with interactive islands.

| Stage | What | Visible change | Status |
|---|---|---|---|
| **1 — The Shell** (this doc) | Nav, theme and `<head>` become server-rendered HTML on every page. Pages untouched, still fully interactive. | **None.** Pixels don't move. | scoped |
| **2 — Render modes** ([render-modes.md](render-modes.md)) | `AddServerSideBlazor` → Blazor Web App render modes, shipped `prerender: false`. `_Host.cshtml` + `_Layout.cshtml` die. | None intended | needs scoping |
| **3 — Pages** | Each page drops `@rendermode` → real HTML + islands. `/Chart/{id}` first ([chart-details overhaul doc](chart-details-overhaul.md)). | Per page, per PR | after 1+2 |

**Prerendering never turns on.** *Prerender* = render twice (server, then again in the
circuit) — double `OnInitializedAsync`, JS interop before the circuit exists. That is the
app-wide flip that broke the site once already and it stays off permanently. *Static
rendering* = render once, as real HTML, no circuit. Stage 3 makes pages **static**, not
prerendered. Do not conflate them.

**The Shell is the backbone, not the payoff.** It carries one unavoidable full-site QA
(the shell is on every page). Everything after it is per-page and rolls back on its own.

---

## 1. End state

```
GET /TierLists (anonymous)
  → full HTML: <head> with a real <title> + themed static shell (nav, bottom nav)
    + the Blazor root marker
  → blazor.server.js connects; page content renders in the circuit
  → the shell was already painted; no blank void, no theme flash
```

The shell is rendered by **static-rendered Razor components** (`render-mode="Static"`) hosted
from the cshtml layout. Nothing in the shell needs a circuit. Nothing in the shell prerenders.
The split is by content type, which is what avoids the historical `ServerPrerendered` breakage.

At Stage 2 those same components move into `App.razor`'s body unchanged. **That portability is
why they are `.razor` and not cshtml partial markup** — cshtml markup would be written twice.

## 2. Decisions

- **D1 — Cutover, not duplication.** MainLayout stops rendering chrome; the Shell components
  are the only nav implementation. (A static skeleton Blazor replaces was rejected: drift +
  flash. Note this rejection is about *the shell*; a loading skeleton for an island is a
  different thing and is fine.)
- **D2 — Static `.razor` components, not cshtml partials.** `Shell.razor` et al render via
  `<component type="typeof(Shell)" render-mode="Static" />`. This buys real Razor authoring,
  DI, `L[…]`, `OnInitializedAsync`, a live `HttpContext`, **`UiColorTokenTests` coverage**
  (it scans `.razor`, not `.cshtml`), and **working `MudIcon`** (see §9). At Stage 2 they drop
  into the static root layout unchanged.
- **D3 — Rendered per request, split by auth.** Anonymous → default mix + sign-in link.
  Authenticated → personalized at origin. No client-side identity hydration, no flash-of-wrong-shell.
- **D4 — Reuse the existing CSS vocabulary.** `wwwroot/css/site.css` already defines
  `.wordmark .top-nav .top-nav-btn .mega-panel .mega-col .mega-head .mix-pill .mix-dot
  .bottom-nav .bn .bn-wide .more-sheet .nav-avatar-wrap .nav-import-pulse .page-dock` at top
  level. Static markup emits the same classes. Only Mud-coupled selectors get re-scoped (§4).
- **D5 — The shell is `--mud-*`-free, by construction.** Every `--mud-palette-*`,
  `--mud-elevation-*`, `--mud-zindex-*` and `--mud-typography-*` is emitted by
  `MudThemeProvider` **inside the circuit** (§9). A single `var(--mud-…)` in the shell
  reintroduces a full FOUC. Shell colors come from `--mix-*` only.
- **D6 — Menus go vanilla.** Click-to-open (parity with MudMenu — not hover), one open at a
  time, close on outside click / Escape / link click. More sheet = fixed bottom sheet, 75vh,
  toggled class. `MudHidden` → media queries at **960px** (Mud md: desktop `min-width: 960px`,
  bottom nav + sheet `max-width: 959.98px`).
- **D7 — Mix switching = endpoint + reload.** `GET /Mix/Set?mix=Phoenix2&redirectUrl=/TierLists`,
  modeled on [CultureController](../../ScoreTracker/ScoreTracker/Controllers/CultureController.cs).
  Mix switching already forces a full reload (MainLayout 530–534), so UX is unchanged.
- **D8 — ⚠ Every shell anchor to a non-Blazor route carries `target="_top"`.** Blazor attaches
  a **global** click listener and intercepts any same-origin `<a href>` lacking a
  `target`(≠`_self`) or `download` — including anchors outside the component tree (§9). Without
  it, `/Mix/Set`, `/Culture/Set`, `/Login` and `/Logout` route internally and render
  `<NotFound>`. `target="_top"` is checked by Blazor's own qualification, so the browser does a
  normal full navigation. **`/Login` and `/Logout` work today only because MudBlazor's `Href`
  path dodges interception somehow; plain anchors inherit none of that.**
- **D9 — Anonymous mix moves from ProtectedLocalStorage to a cookie.** `UiSettingsAccessor`
  stores the anonymous mix in `ProtectedLocalStorage` — JS interop, circuit-only, throws
  outside a live circuit (its own comment says so). A static component can never read it.
  Therefore: anonymous mix = cookie `CurrentMix` (value = `MixEnum` name). Anonymous users'
  previously selected mix resets once; owner-accepted.
- **D10 — The mix is resolved server-side and handed to the circuit as a root parameter.**
  Nothing inside the circuit re-derives request state. See §4 `ShellContext`. This retires
  `_mixLoaded`, the body-hold progress bar, and one settings query per page load — the
  WhatShouldIPlay gate race stops existing rather than being held behind a spinner.
- **D11 — Import pulse = `ShellImportPulse` island.** The shell always renders
  `<span class="nav-import-pulse" hidden id="shell-import-pulse">`. The island subscribes as
  MainLayout does today (`IUiNotificationHub.Subscribe<ImportStatusUpdatedEvent/
  ImportStatusErrorEvent>` on `UiTopics.User(id)`, MainLayout 585–620) and toggles `hidden` via
  JS interop. Hidden-by-default, so no pre/post-circuit delta.
- **D12 — Bottom-nav active state**: server-rendered from `HttpContext.Request.Path` (same
  prefix rules as `BnClass`, MainLayout 471–478); `nav.js` re-evaluates on SPA navigation by
  wrapping `history.pushState`/`replaceState` + `popstate`. Blazor does go through both (§9).
- **D13 — MainLayout keeps its non-chrome jobs**: the four Mud providers, `ChartVideoDisplay`,
  theme resolution for the *Mud* palette, the legacy-mix gate + `RedirectIfGated`, the PageDock
  slot + Recap popup, `OnLocationChanged`. It is transitional scaffolding with a known
  demolition date (Stage 2/3) — do not grow new responsibilities on it.
- **D14 — `LegacyMixGate` applies server-side** to nav filtering (`Services/LegacyMixGate.cs` is
  a static class — trivially callable from a static component). Page-level gating stays in
  MainLayout (D13).
- **D15 — No feature flag.** Straight cutover, FT-gated. A flag would double the test matrix.
- **D16 — `_Layout.cshtml` retires.** `_SiteLayout.cshtml` becomes the one layout. Audited:
  its only consumers are `_Host.cshtml` and `FrontDoor.cshtml`'s ShowApp branch. `Error.cshtml`
  and `Privacy.cshtml` are standalone (own DOCTYPE, no layout) and are not touched.
- **D17 — The front door's static branch is NOT touched.** `FrontDoor.cshtml` has two branches:
  `ShowApp` (signed-in, `Layout = "_Layout"` + `<component typeof(App)>`) moves to `_SiteLayout`
  because `_Layout` retires; the **anonymous static branch keeps `Layout = null`, its own
  topbar, its own `body.front-door` class and `front-door.css` — untouched.** The original
  draft's plan to delete its topbar and adopt the shared shell contradicted its own "visually
  unchanged" gate and would have broken 137 CSS rules and 3 E2E assertions for zero gain.
- **D18 — Caching is not Stage 1.** Culture-cookie set-only-when-missing → Stage 2 (it's a
  cookie-writer fix and `_Host.cshtml` dies there). Output cache + CDN bypass → Stage 3, with
  the first static page: caching pays ~nothing while every page still boots a circuit, and it
  is only meaningfully testable once anonymous HTML contains content.

  ⚠ **Two obstacles Stage 3 inherits, observed live on production** (`curl` of
  `https://piuscores.arroweclip.se/TierLists`, 2026-07-14):
  - `Cache-Control: no-cache, no-store, max-age=0` on every response. Stage 3's caching must
    overturn this, and must find out what sets it first.
  - **Azure App Service ARR affinity**: `Set-Cookie: ARRAffinity=…` +
    `Set-Cookie: ARRAffinitySameSite=…` on a cookieless request. Platform-set (not app code),
    it's the sticky-session cookie that binds a client to one instance. A response carrying it
    can't be edge-cached as-is without leaking one instance's affinity to every visitor. It
    appears to be first-response-only (a client that already holds it doesn't get a new one),
    so the D9 culture fix isn't the whole story — **the cacheable-response question is
    "cookieless first hit sets three cookies," not one.** Blazor Server needs affinity only
    across multiple instances; whether the plan is scaled out is an owner question.

## 3. The islands audit

`Shared/MainLayout.razor` @ `01904329`, every interactive/dynamic element:

| Shell element | Today (line) | Becomes | Island? |
|---|---|---|---|
| Wordmark, nav links/groups | 69–200 | static HTML | no |
| Mega menus (Play/Progress/Compete/Community/Tools) | `MudMenu` 73–199 | static markup + `nav.js` click-toggle | no |
| Nav visibility (auth / `LegacyMixGate` / XX-vs-Phoenix) | 78–196 | server-side conditionals | no |
| App-bar chart search (`ChartSelector`) | 204–208 | **island** (owner: no visual changes, so it stays) | **yes** |
| Mix pill + `MixSelector` menu | 212–219 | static `<details>` menu + `/Mix/Set` links + full reload | no |
| Avatar + user menu | 221–247 | static HTML (claims + cached settings) | no |
| Import-status pulse dot | 234–237, 585–620 | **island** (`ShellImportPulse`) toggling a server-rendered dot | **yes** |
| Live SkillRating subscription | 582, 586, 598–603 | **DELETE — dead code.** `Rating` is assigned at 582, declared at 593, reassigned at 601, and rendered nowhere. Audited: only self-references. Deleting it also removes a `GetPlayerStatsQuery` from every signed-in page load. | n/a |
| More sheet (mobile) | `MudDrawer` 257–335 | static bottom sheet + `nav.js` toggle | no |
| Bottom nav + active state | 341–371 | static HTML; active class server-rendered from path, re-evaluated by `nav.js` on SPA navigation | no |
| PageDock slot + focus mode | 342–347, 445–453 | dock content stays in the Blazor root (page-supplied); `has-dock`/`focus-mode` move to `<html>` classes via a JS bridge | no |
| `pageDock.watch()` bootstrap | 455–461 | called by `nav.js` on DOMContentLoaded. **`watch()` is idempotent** (guards on `watching`), so an overlapping MainLayout call is harmless during the transition. | no |
| Theme vars `<style>` block | 30–32 | emitted server-side by the layout | no |
| MudThemeProvider/Popover/Dialog/Snackbar, `ChartVideoDisplay` | 24–28 | stay in the (gutted) MainLayout — invisible providers, Blazor pages need them | no |
| Legacy-mix body hold + `RedirectIfGated` | 374–394, 480–500 | gate stays; **the `_mixLoaded` hold + progress bar DELETE** (D10 makes the mix synchronous) | no |
| B1G ONE popup | 396–408, 512, 554–559 | **DELETE — expired**: cutoff was 2024-10-15 | n/a |
| Phoenix Recap popup | 413–425, 560–570 | stays in MainLayout (app-root, signed-in only) | no |
| Sign-in affordance | 250 | static link to `/Login` **with `target="_top"`** (D8) | no |

**Two islands (import pulse, app-bar search), ~150 lines of vanilla JS, everything else is
server HTML.**

## 4. File-by-file plan

### NEW `Components/Shell/Shell.razor` (+ `ShellNav`, `ShellMoreSheet`, `ShellBottomNav`, `ShellMixMenu`)

Static-rendered. Markup parity with MainLayout 68–371: wordmark, the same nav groups with the
same items and the same `L[…]` keys, same auth/gate/mix conditionals (`IsLoggedIn`,
`IsGatedMix`, XX→`/Progress` vs `/Phoenix/Progress`), avatar block with the `onerror`-fallback
`<img>` exactly as MainLayout 230–232, the hidden import-pulse span (D11), sign-in icon link
when anonymous. Then the More sheet and `<nav class="bottom-nav">`.

- **Icons: keep `<MudIcon Icon="@Icons.Material.Filled.Home" />`.** MudIcon renders statically
  (§9) — identical glyphs, no `@Html.Raw` hack, no hand-rolled SVG.
- **Menu containers**: `<div class="shell-menu" data-menu>` with an activator button + panel.
  `nav.js` owns open/close.
- **Desktop/mobile**: `shell-desktop-only` / `shell-mobile-only` + the 960px media queries, not
  `MudHidden`.
- **Highlighted tournaments** (MainLayout 138–152) render from the model with the same three
  link forms (Stamina / LinkOverride / Qualifiers).
- **Localization**: reuse the exact existing resx keys — target **zero new keys**.
- **No color literals** — `UiColorTokenTests` scans these files (they're `.razor`).

`ShellMixMenu`: the mix pill's menu. `MixSelector`'s two-level disclosure (primary trio →
More Mixes → 7 collections → expand) becomes nested `<details>`/`<summary>` — native browser
disclosure, no JS, keyboard-accessible. Every entry is `/Mix/Set?mix=X&redirectUrl=…` with
`target="_top"` (D8). ~38 rows; `<details>` hides content visually but still ships the markup.

### NEW `Services/ShellContext.cs` + `ShellModelFactory` / `ShellViewModel`

```csharp
// Request-resolved shell state, carried into a circuit that can no longer see the request.
// The shell resolves it while the HttpContext is live; App re-seeds it from its root
// parameter once the circuit starts. Scoped, so it never crosses circuits.
public sealed class ShellContext
{
    public MixEnum? CurrentMix { get; set; }
}
```

```csharp
public sealed record ShellViewModel(
    bool IsLoggedIn, Guid? UserId, string? DisplayName, string? GamerTag, string? AvatarUrl,
    MixEnum CurrentMix, MixEnum ThemeMix, bool IsGatedMix, bool HasRecap,
    IReadOnlyList<TournamentRecord> HighlightedEvents, string ActivePath);
```

`ShellModelFactory` (scoped, plain class, memoized per request — `_Host` and the layout both
call it):

| Field | Source | Cost |
|---|---|---|
| IsLoggedIn / UserId / DisplayName | `HttpContext.User` claims | free |
| GamerTag, AvatarUrl | `GetUserUiSettingsQuery` (keys `GameTag`, `ProfileImage`; same defaults as MainLayout 573–581) | `IMemoryCache` per user, 5 min |
| CurrentMix | signed-in: same dict, key `Universal__CurrentMix`; anon: `CurrentMix` cookie else `MixEnum.Phoenix` | free / cached |
| ThemeMix | `MixThemes.ResolveThemeMix(settings[MixThemes.OverrideSettingKey], CurrentMix)`; anon: CurrentMix | cached |
| IsGatedMix | `LegacyMixGate.IsGatedMix(CurrentMix)` | free |
| HasRecap | `GetPlayerRecapQuery(userId) != null` | `IMemoryCache` per user, 30 min |
| HighlightedEvents | `GetAllTournamentsQuery` → `Where(IsHighlighted)` | `IMemoryCache` global, 15 min |
| ActivePath | `HttpContext.Request.Path` | free |

No new Application/Domain contracts — every query above already exists.
⚠ **Never call `IUiSettingsAccessor` for anonymous values here** (D9).

The factory also seeds `ShellContext.CurrentMix` for its own (request) scope so MVC-side
callers agree with circuit-side ones.

### NEW `Pages/Shared/_SiteLayout.cshtml`

The one full-page layout. Emits, in order: head boilerplate (fonts/Mud/site/charts CSS,
`asp-append-version`), `<html lang="@CultureInfo.CurrentUICulture.TwoLetterISOLanguageName">`
(today's `_Layout` hardcodes `lang="en"`), the theme `<style>` block, **a real static
`<title>`** (§9 — Blazor removes it on boot; crawlers read it), the `Head` section hook,
Clarity + ApexCharts blocks verbatim from `_Layout` 16–39, then
`<component type="typeof(Shell)" render-mode="Static" model="…" />`, `<main class="shell-main">
@RenderBody()</main>`, `blazor-error-ui`, scripts.

`<body class="@MixThemes.CssClassFor(shell.ThemeMix) @ViewData["BodyClass"]">` — the
`BodyClass` hook exists so pages can add their own (the front door's `front-door`, if it ever
adopts this; today it doesn't — D17).

### NEW `Controllers/MixController.cs`

Clone CultureController's shape: `[Route("[controller]")]`, `[ApiExplorerSettings(IgnoreApi = true)]`,
`GET Set(string mix, string redirectUrl)` → validate `Enum.TryParse<MixEnum>`; if
`ICurrentUserAccessor.IsLoggedIn` → send `SaveUserUiSettingCommand("Universal__CurrentMix", mix)`;
**always** also append the `CurrentMix` cookie (keeps anon working and the future cache key
honest after logout); `LocalRedirect(redirectUrl)`.

⚠ Set the cookie's `Expires` explicitly. The original draft claimed the cookie-policy
middleware forces 30-day expiry — **`AddCookiePolicy` is registered but `app.UseCookiePolicy()`
is never called**, so it doesn't run.

### NEW `wwwroot/js/nav.js` (~150 lines, zero dependencies)

Contract: (1) menu toggling for `[data-menu]` (activator click toggles `.open`; opening one
closes others; outside click + Escape close; `<a>` click closes — but **`<summary>` clicks must
not**, or the mix menu's disclosure closes the menu). (2) More sheet open/close. (3)
`shell.refreshActiveNav()` — compares `location.pathname` prefixes against each `.bn[data-href]`
(same rules as old `BnClass`), toggles `.active`; wraps `history.pushState`/`replaceState` and
listens to `popstate`. (4) `shell.setDockState(hasDock, focusMode)` — toggles `html.has-dock` /
`html.focus-mode`. (5) `shell.setImportPulse(bool)`. (6) on `DOMContentLoaded`:
`pageDock.watch()` + `refreshActiveNav()`. No colors, no `innerHTML`.

(7) **`shell.positionMenu(panel)` — viewport flip/shift.** Today each mega-menu is a
`MudPopover`: rendered at end of `<body>`, `position: fixed`, **JS-positioned with viewport
flip/shift**. Statically it's a `position: absolute` child of the appbar with no flip, so at
960–1100px a wide two-column panel (Compete) overflows into horizontal scroll where Mud would
have shifted it back on-screen. On open, measure the panel against `innerWidth` and clamp/flip
its inline offset. **Owner call: match Mud's behavior rather than approximate it in CSS** —
pure-CSS right-anchoring is cheaper but isn't pixel-identical at the extreme, and the gate for
this project is that pixels don't move.

### NEW `Components/ShellImportPulse.razor` + `Components/AppBarSearch.razor` (islands)

`ShellImportPulse`: renders nothing visible. If `IsLoggedIn`: subscribe exactly as MainLayout
585–620 and call `JSRuntime.InvokeVoidAsync("shell.setImportPulse", running)` on change (guard
interop for disconnects). `IDisposable` with MainLayout's defensive null pattern (502–511).

`AppBarSearch`: wraps today's `ChartSelector` (MainLayout 204–208) as its own root.
⚠ **Prove at C3**: its MudBlazor popover must render through MainLayout's `MudPopoverProvider`
in a *different root* on the same circuit. Should work (Mud's popover is service-mediated and
roots share the circuit's scope) — if it doesn't, build `IslandRoot` (a provider wrapper) here,
because Stage 3 needs it for every page that leaves MainLayout's umbrella anyway.

### MODIFIED `Pages/_Host.cshtml`

`Layout = "_SiteLayout"` + `<component type="typeof(App)" render-mode="Server"
param-CurrentMix="@shell.CurrentMix" />`.

⚠ Root component parameters round-trip through the client for `Server` render mode, so they're
tamperable. The mix is a display preference an anonymous visitor already fully controls, so
tampering is a no-op — but **this channel must never carry anything that gates access.**

### MODIFIED `App.razor`

```razor
@inject ShellContext Shell
<Router AppAssembly="@typeof(App).Assembly">…unchanged…</Router>
@code {
    [Parameter] public MixEnum CurrentMix { get; set; }
    protected override void OnInitialized() => Shell.CurrentMix = CurrentMix;
}
```

`OnInitialized` completes before Router/RouteView/MainLayout/page initialize, so the seed is
always present before the first read.

### MODIFIED `Services/UiSettingsAccessor.cs`

```csharp
public async Task<MixEnum> GetSelectedMix(CancellationToken ct = default)
{
    if (!_currentUser.IsLoggedIn) return _shell.CurrentMix ?? MixEnum.Phoenix;
    // …unchanged DB path…
}
```

All **33 callers untouched** — they keep calling `await UiSettings.GetSelectedMix()`; only the
source changes. `ProtectedLocalStorage` leaves the mix path entirely. **`SetSelectedMix`
deletes** — audited, its only caller is MainLayout:532, which is deleted (MixController owns
the write). All other settings keep ProtectedLocalStorage (only read inside circuits).

### MODIFIED `Shared/MainLayout.razor` — the gutting

KEEP: 22–28 (PageTitle, four providers, ChartVideoDisplay); the gate + `@Body` + `MudContainer`
(minus `MudLayout`/`MudAppBar`/`MudMainContent` wrappers — content width parity via
`MudContainer` remains); Recap dialog (413–425) + init (560–570); `PageDockService` wiring with
`OnDockChanged` → `JSRuntime.InvokeVoidAsync("shell.setDockState", hasDock, focusMode)`; the
PageDock content slot (342–347, fixed-position, DOM location irrelevant); `OnLocationChanged`
(keep gate redirect + `pageDock.reset`; drop `_showMore`); theme resolution for
`MudThemeProvider` (518–550 **minus** the CSS-var style block); `RedirectIfGated`; `Dispose`.

DELETE: `MudLayout`/`MudAppBar` + entire top nav (65–252); More-sheet drawer (253–335); bottom
nav (337–371); `GoToChart` (463–467); `BnClass` (469–478); `SetMix` (530–534); CSS-var style
block (29–32); dense-nav/drawer `<style>` (33–64 — audited: `.dense-nav` and `.mud-drawer` are
used **only** here, they die with the drawer); `OnAfterRenderAsync`/`pageDock.watch` (455–461);
B1G ONE popup + fields + init (396–408, 512, 515, 517, 554–559); the **dead** stats subscription
(`_statsSubscription`, `Rating`, `OnPlayerStatsUpdated`, the `GetPlayerStatsQuery` at 582);
avatar/profile fetching (571–581 → factory); import-status handlers (585–620 → island);
tournaments query (551–552 → factory); **`_mixLoaded` + the body-hold progress bar (374–380)**
(D10).

### MODIFIED `Pages/FrontDoor.cshtml`

ShowApp branch only: `Layout = "_SiteLayout"` + the `param-CurrentMix` component tag. **The
static branch is not touched** (D17).

### MODIFIED `wwwroot/css/site.css`

Re-scope every Mud-coupled selector (line refs @ `01904329`):

| Today | Becomes | Why it matters |
|---|---|---|
| `.mud-layout.theme-phoenix` / `.theme-xx` / `.theme-phoenix2` (167, 176, 185) | `body.theme-*` | **The per-mix atmosphere gradients.** `.mud-layout` won't exist. Silent regression — no compile error, no test failure, the site just loses its per-mix ground light. The original draft missed all three. |
| `.mud-main-content` (244) | `.shell-main` | bottom-nav clearance; also missed |
| `.has-dock .mud-main-content` (249) | `html.has-dock .shell-main` | |
| `.focus-mode .mud-appbar` (301) | `html.focus-mode .shell-appbar` | |
| `.focus-mode .mud-main-content` (305) | `html.focus-mode .shell-main` | |
| `.focus-mode .page-dock` (287) | `html.focus-mode .page-dock` | |

`html.nav-away .bottom-nav` (267) already works unchanged. `.appbar-search .mud-*` rules stay
(the search island still renders Mud inputs).

ADD — the shell's own base, from `--mix-*` only (D5):

```css
body { background: var(--mix-bg); color: var(--mix-ink); font-family: <the Roboto stack>; }
.shell-appbar {
    position: fixed; top: 0; right: 0; left: 0;
    z-index: 1300;                 /* == MudTheme ZIndex.AppBar; see §9 */
    display: flex; align-items: center;
    height: 48px;                  /* == MudAppBar Dense; see §9 */
    box-sizing: border-box;
    background: var(--mix-nav);    /* == PaletteX.AppbarBackground */
    color: var(--mix-ink);         /* == PaletteX.AppbarText */
    box-shadow: <MudTheme Shadow.Elevation[4]>;
}
.shell-main { padding-top: 48px; }
```

⚠ `.shell-appbar` must **not** set `overflow: hidden` or the menu panels clip at 48px.
Also add: More-sheet open/closed transitions (replacing MudDrawer's) and the
`shell-desktop-only`/`shell-mobile-only` helpers at the 960px seam.

### MODIFIED `ScoreTracker.Tests/ArchitectureTests/UiColorTokenTests.cs`

No change required for the shell (D2 keeps it `.razor`). **Optional but recommended:** widen the
scan to `.cshtml`, which is currently invisible to it. Doing so surfaces real pre-existing debt
— 4 literals in `_Layout.cshtml` (the ApexCharts block, copied verbatim into `_SiteLayout`) and
7 in `Privacy.cshtml`. Tokenize or allowance them honestly.

### MODIFIED `Program.cs`

Register `ShellContext` and `ShellModelFactory` scoped next to the other Web services (~247–254).
Nothing else. **Response compression needs no change** — it's already on in production via
Azure's IIS dynamic compression (§9), so the shell's ~117 nav rows (~25KB uncompressed against
today's ~5KB) cost ~4KB on the wire.

### UNCHANGED but load-bearing

`wwwroot/js/page-dock.js` (already vanilla; `nav.js` takes over calling `watch()`),
`Services/LegacyMixGate.cs`, `MixThemes`, `CultureController`, `FrontDoor.cshtml.cs`.
There is no `_ViewImports.cshtml` — each cshtml declares its own `@using`/`@addTagHelper`.
Adding one would clean up all five files; optional.

## 5. Verification

- View-source contains the nav **before any circuit**:
  `curl -s https://localhost:<port>/TierLists | grep -c "shell-appbar\|bottom-nav"` ≥ 1.
- View-source contains a real `<title>`.
- All five suites green. **Audited: there are zero shell selectors in `Tests.E2E` or
  `Tests.Components`** — the original draft's feared selector churn does not exist.
- ⚠ **`FrontDoorDispatcherTests.cs:60`** asserts `.mud-layout` count 0 on the anon front door —
  its proof that the page is circuit-free. After the cutover `.mud-layout` exists nowhere, so it
  **passes while proving nothing**. Re-point it at something real (absence of
  `blazor.server.js`, or of `#blazor-error-ui`).
- FT matrix (owner, once): every top-level page × {desktop 1280, tablet 768, phone 390} ×
  {anon, signed-in} × {Phoenix, XX, one gated legacy mix} — nav parity, menus, More sheet, dock
  pages (randomizer), focus-mode flow, mix switch, locale switch, avatar fallback.

## 6. Build plan

| # | Commit | Contents |
|---|---|---|
| C1 | Shell components + factory + nav.js | `Shell.razor` et al, `ShellViewModel`/`Factory`/`ShellContext`, `nav.js`, site.css additions + re-scopes. Not yet wired. |
| C2 | The cutover | `_SiteLayout`, `_Host` → it, MainLayout gutted per §4, `MixController`, `UiSettingsAccessor` + `App.razor` param, FrontDoor ShowApp branch, dead-code deletions — **FT1: full §5 matrix** |
| C3 | Islands | `ShellImportPulse`; `AppBarSearch` + the cross-root popover proof (→ `IslandRoot` if it fails) |
| C4 | Tests + docs | E2E guard re-point, new facts (anon HTML contains nav pre-circuit; static `<title>` present), optional `.cshtml` ratchet widening, ARCHITECTURE.md shell paragraph, UX-GUIDELINES.md shell note, sync this doc |

## 7. Contract for Stage 2/3

- Shell components are static-rendered `.razor` in `Components/Shell/`. At Stage 2 they move
  into `App.razor`'s body; nothing else about them changes.
- JS: `shell.setDockState`, `shell.setImportPulse`, `shell.refreshActiveNav` are public and
  stable; `html.nav-away` / `html.has-dock` / `html.focus-mode` are the class contract.
- Any static region is `--mud-*`-free (D5) and `--mix-*`-only.
- Any anchor to a non-Blazor route carries `target="_top"` (D8).
- **`IslandRoot`** (a MudBlazor-provider wrapper for islands outside MainLayout's umbrella) is
  built at C3 if the cross-root popover proof fails, and by Stage 3 regardless.

## 8. Risks

- **Visual parity** is the main surface: fixed-header offset, menu positioning/keyboard, sheet
  height/scroll, safe-area insets, the 960px seam. Mitigation: D4 same-classes + §5 matrix + the
  exact values in §9.
- **FOUC** if any `var(--mud-…)` reaches the shell (D5). This is the failure mode that turns
  "no visual changes" into "every page flashes."
- **Two roots, one circuit**: scoped services (PageDockService, IUiNotificationHub) are shared
  across roots — verify disposal on teardown.
- **One new layout shift**: `/ChartRandomizer` registers a dock on load
  ([ChartRandomizer.razor:608](../../ScoreTracker/ScoreTracker/Pages/Tools/ChartRandomizer.razor:608)).
  Shell paints without `html.has-dock` → circuit adds it → `padding-bottom` 72px → 132px, so
  mobile content jumps 60px. Bounded to dock pages, mobile only. Accepted. Focus mode is page
  state that starts false, so no flash there.
- **`MudHidden` → media queries** puts both navs in the DOM with one hidden, so every nav link
  appears 2–3× in the HTML. Normal responsive pattern; good for crawl paths; ~2KB.
- **resx churn**: target zero new keys; any new key → all nine locales in the same commit.

## 9. Verified against shipped source

The load-bearing facts. Each was checked, not assumed — do not re-litigate without re-checking.

- **Every `--mud-*` var is circuit-only.** `MudBlazor.min.css` has **zero** `--mud-palette-*`
  declarations, and neither `--mud-elevation-4` nor `--mud-zindex-appbar` appear. All are
  emitted at runtime by `MudThemeProvider`, a component inside the circuit. Meanwhile the
  shipped stylesheet styles the page entirely from them:
  `body{color:var(--mud-palette-text-primary);font-family:var(--mud-typography-default-family);…;background-color:var(--mud-palette-background)}`.
  Pre-circuit each declaration is invalid at computed-value time → **black text, white
  background, serif font**, then a snap to the dark theme. There is no flash today only because
  nothing paints pre-circuit; The Shell is what introduces the exposure. → D5.
- **Appbar parity is arithmetic.** `MixThemes` sets `AppbarBackground = p.Nav` → `--mix-nav`
  (the token `.bottom-nav` already uses) and `AppbarText = p.Ink` → `--mix-ink`. It sets no
  `LayoutProperties`, so `--mud-appbar-height` is MudBlazor's default `64px`; `Dense` computes
  `calc(64px - 64px/4)` = **48px**, and `.mud-main-content` takes the same 48px as padding-top.
- **`ZIndex.AppBar` defaults to 1300**, not MUI's 1100. Full ladder: Drawer 1100, Popover 1200,
  AppBar 1300, Dialog 1400, Snackbar 1500, Tooltip 1600. `MixThemes` overrides none. Note the
  popover sits *below* the appbar — that works only because menus hang beneath the bar. Mud also
  layers things at `calc(var(--mud-zindex-appbar) + 1/2/4)`, so the value is load-bearing.
- **`MudIcon` renders statically.** `.mud-icon-root` and `.mud-icon-size-small` are in the
  static stylesheet, not the runtime palette vars, so icons are identical pre-circuit and the
  shell reuses `<MudIcon Icon="@Icons.…" />` unchanged. (General rule: in a static region Mud
  components **render but don't respond** — `OnAfterRenderAsync` never runs, so a `MudButton`
  looks perfect and does nothing. Sort by *display vs interaction*, never by *does it render*.)
- **⚠ MudBlazor assumes ONE interactive tree with its providers at the top; islands are many
  trees.** There is exactly one `MudPopoverProvider` per circuit — it subscribes a section
  outlet by a fixed id, so a second throws *"already a subscriber to the content with the given
  section ID 'mud-overlay-to-popover-provider'"*. Roots initialise in **document order**, so any
  island ahead of the provider gets *"Missing `<MudPopoverProvider />`"* the moment it opens a
  popover, select, menu, tooltip or dialog. **Therefore the providers mount as the first root
  on the page** (`Components/MudProviders.razor`), not in MainLayout — which is the *last* root
  and so is behind every island. This replaces the original §7 `IslandRoot` idea, which had each
  island bring its own providers: that is impossible, not merely undesirable. **Stage 3 inherits
  this rule** — every page it makes static hosts islands under the same constraint.
- **A static `<title>` is safe.** `blazor.server.js`'s `getAndRemoveExistingTitle()` iterates
  `<head>`'s titles backwards and removes any **not** preceded by a Blazor marker comment,
  returning its text as the fallback. So: crawlers read the static title from raw HTML; browsers
  get `<PageTitle>` working identically; no duplicate. **It does not remove `<meta>`** — a static
  meta plus a page's `HeadContent` meta gives two in the live DOM after boot. Harmless for
  raw-HTML readers; the clean resolution is retiring `HeadContent` per page at Stage 3.
- **Blazor's link interception is a global document listener.**
  `notifyAfterClick(e){…this.eventInfoStore.addGlobalListener("click")}`, and the qualification
  is `(!target||"_self"===target) && hasAttribute("href") && !hasAttribute("download")` → if the
  href is within base-URI space: `preventDefault()` + internal routing. It catches anchors
  outside the component tree. → D8.
- **Blazor navigation goes through `history.pushState`/`replaceState`** (present in
  `blazor.server.js`), so D12's wrapper premise holds.
- **The baseline, confirmed live** (`curl -H "Accept-Encoding: gzip,br"` of
  `https://piuscores.arroweclip.se/TierLists`, 2026-07-14): **5,283 bytes decompressed, zero
  `<title>` elements, zero nav markup.** The site really does serve an empty themeless shell to
  every crawler and every first paint.
- **Response compression is ON in production** — `Content-Encoding: gzip` +
  `Vary: Accept-Encoding`, applied by Azure App Service's IIS dynamic compression (nothing in
  `Program.cs` registers it). So the shell's 4× HTML increase costs ~4KB on the wire, and
  registering `UseResponseCompression` is unnecessary. (gzip, not brotli — brotli would be
  ~15–20% smaller; not worth fighting for.)
- **8 pages already emit SEO metadata no crawler has ever seen** — `ChartDetails`,
  `ChartSkills` (including the daily-regenerated folder share card), `PersonalizedBreakdown`,
  `ChartRandomizer`, `LifeCalculator`, `PhoenixCalculator`, `PhoenixToXXCalculator`. All of it
  goes through `HeadContent` → `HeadOutlet` → the circuit. Production HTML has no `<title>` at
  all. **This is the actual payoff of the migration**, and it lands at Stage 3.

## 10. Open

- **`/Login` + `/Logout` today** — both are non-Blazor routes linked from the shell, and both
  work, so MudBlazor's `Href` dodges interception by a mechanism not yet identified. Doesn't
  block D8 (plain anchors get `target="_top"` regardless), but worth knowing whether the shell
  is fixing two live bugs or avoiding creating them.
