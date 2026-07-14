# The static shell — design & implementation spec (wave 2 of the SEO/islands plan)

Decided in the 2026-07-14 chart-details workshop (owner + Claude). This doc is written to be
**executed by a separate session** — it front-loads every file path, signature, and landmine so
implementation requires discovery only where explicitly marked ⚠ AUDIT. Where this doc and the
code disagree, the code wins — but update this doc in the same commit.

**Wave map** (context):

- **Wave 2 (this doc)**: the site shell (top nav, bottom nav, More sheet, mix pill, theme) becomes
  server-rendered HTML on every page. Blazor pages boot into the content region under it.
  Anonymous responses become CDN-cacheable.
- **Wave 1 (parallel, branches off this branch)**: `/Chart/{id}` becomes a Razor Page with Blazor
  islands. It codes against §7 of this doc. If §7 changes, update §7 in the same commit — that's
  the cross-session ping.
- **Wave 3+**: other pages migrate page-by-page. Out of scope.

**Context.** Today `_Host.cshtml` serves an empty shell (`render-mode="Server"`, no prerendering
anywhere — verified against production: 7.9KB of HTML with no `<title>`, no nav, no content).
The front door ([front-door.md](front-door.md)) already proved the ingredients: a real Razor Page
with a static topbar, `MixThemes.CssVariablesFor(...)` theming, `L[…]` in cshtml, `IMediator`
from a page model. This doc generalizes that shell to the whole site.

---

## 1. End state

Request flow after this work:

```
GET /TierLists (anonymous)
  → CDN edge HIT (or origin output-cache HIT)
  → full HTML: <head> + themed static shell (nav, bottom nav) + Blazor root markup
  → blazor.server.js connects; page content renders in the circuit
  → shell was already painted; no blank void

GET /TierLists (authenticated cookie present)
  → CDN BYPASS → origin renders personalized shell (avatar, gamer tag, their mix theme)
  → same from there

GET /Chart/... (wave 1, anonymous)
  → cached full HTML including page CONTENT (Razor), islands connect after
```

The shell is rendered by **one Razor layout** consumed by both page families. Nothing in the
shell prerenders Blazor components and no Blazor component renders shell chrome — the split is
by content type, which is what avoids the historical `ServerPrerendered` breakage
(double-`OnInitializedAsync`, JS interop before circuit, HttpContext confusion).

## 2. The islands audit (why "practically fully prerendered" is literally true)

Audit of `Shared/MainLayout.razor` @ commit `01904329`, every interactive/dynamic element:

| Shell element | Today (MainLayout line) | Becomes | Island? |
|---|---|---|---|
| Wordmark, nav links/groups | 69–200 | static HTML | no |
| Mega menus (Play/Progress/Compete/Community/Tools) | `MudMenu` 73–199 | static markup + `nav.js` click-toggle | no |
| Nav visibility (auth / `LegacyMixGate` / XX-vs-Phoenix links) | 78–196 | server-side conditionals in the partial | no |
| App-bar chart search (`ChartSelector`) | 204–208 | **island** on Blazor pages; static link to `/Charts` on circuit-less pages (v1) | conditional |
| Mix pill + `MixSelector` menu | 212–219 | static menu + `GET /Mix/Set` endpoint + full reload (mix switch already forces reload today, 533) | no |
| Avatar + user menu | 221–247 | static HTML (claims + cached settings) | no |
| Import-status pulse dot | 234–237, 585–620 | **island** (`ShellImportPulse`) toggling a server-rendered dot | **yes — the one true island** |
| Live SkillRating subscription | 582, 586, 598–603 | **DELETE — dead code**: `Rating` is subscribed and updated but never rendered anywhere in markup | n/a |
| More sheet (mobile) | `MudDrawer` 257–335 | static bottom sheet + `nav.js` toggle; nav groups → `<details>` | no |
| Bottom nav + active state | 341–371 | static HTML; active class server-rendered from request path, re-evaluated by `nav.js` on Blazor SPA navigation | no |
| PageDock slot + focus mode | 342–347, 445–453 | dock content stays in the Blazor root (it's page-supplied); `has-dock`/`focus-mode` move to `<html>` classes via a JS bridge | no (app-root concern) |
| `pageDock.watch()` bootstrap | 455–461 | called by `nav.js` on DOMContentLoaded (no circuit needed) | no |
| Theme vars `<style>` block | 30–32 | emitted server-side by the layout | no |
| MudThemeProvider/Popover/Dialog/Snackbar, `ChartVideoDisplay` | 24–28 | stay in the (gutted) MainLayout — invisible providers, Blazor pages need them | no (not chrome) |
| Legacy-mix body hold + `RedirectIfGated` | 374–394, 480–500 | stays in MainLayout (guards Blazor page loads) | no |
| B1G ONE popup | 396–408, 512, 554–559 | **DELETE — expired**: cutoff was 2024-10-15 | n/a |
| Phoenix Recap popup | 413–425, 560–570 | stays in MainLayout (app-root, signed-in only) | no |
| Sign-in affordance | 250 | static link to `/Login`; the future front-door sign-in *dialog* (front-door.md D2-context-2) remains a Blazor-page nicety | no |

**Bottom line: one mandatory island (import pulse), one conditional island (app-bar search where
a circuit already exists), ~150 lines of vanilla JS, everything else is server HTML.**

## 3. Decisions

- **D1 — Cutover, not duplication.** MainLayout stops rendering chrome; the partial is the only
  nav implementation. (A static skeleton that Blazor replaces was rejected: drift + flash.)
- **D2 — Rendered per request, split by auth.** Anonymous → cacheable anon shell (default mix,
  sign-in link). Authenticated → CDN bypass (cookie rule), personalized at origin. No client-side
  identity hydration, no flash-of-wrong-shell.
- **D3 — Reuse the existing CSS vocabulary.** The shell classes are already top-level in
  `wwwroot/css/site.css` (§ "Shell: wordmark, top nav, mega-menus, mix pill", lines ~41–330):
  `.wordmark .top-nav .top-nav-btn .mega-panel .mega-col .mega-head .mix-pill .bottom-nav .bn
  .bn-wide .more-sheet .nav-avatar-wrap .nav-import-pulse .page-dock`. Static markup emits the
  same classes. Only the Mud-coupled selectors need re-scoping (see §4 site.css).
- **D4 — Menus go vanilla.** Click-to-open (parity with MudMenu today — not hover), one open at a
  time, close on outside click / Escape / link click. More sheet = fixed bottom sheet, 75vh,
  toggled class, `<details>` for the Rankings/Tools subgroups. `MudHidden` → media queries at
  **960px** (Mud md breakpoint: desktop nav `min-width: 960px`, bottom nav + sheet `max-width: 959.98px`).
- **D5 — Mix switching = endpoint + reload.** `GET /Mix/Set?mix=Phoenix2&redirectUrl=/TierLists`,
  modeled byte-for-byte on [CultureController](../../ScoreTracker/ScoreTracker/Controllers/CultureController.cs):
  signed-in → `SaveUserUiSettingCommand("Universal__CurrentMix", …)`; anonymous → plain cookie
  (§4 UiSettingsAccessor); then `LocalRedirect`. Mix switching already forces a full reload
  (MainLayout 530–534), so UX is unchanged.
- **D6 — Anonymous mix moves from ProtectedLocalStorage to a cookie.** ⚠ LANDMINE:
  `UiSettingsAccessor` (Services/UiSettingsAccessor.cs) stores the anonymous mix in
  `ProtectedLocalStorage` — **JS-interop, circuit-only; it throws outside a live circuit** (its
  own comment says so). A Razor Page / MVC request can never read it. Therefore: anonymous mix =
  cookie `CurrentMix` (value = `MixEnum` name). One-time cost, owner-accepted: anonymous users'
  previously selected mix resets once (signed-in users unaffected — theirs is DB).
- **D7 — Import pulse = `ShellImportPulse` island.** The static shell always renders
  `<span class="nav-import-pulse" hidden id="shell-import-pulse">` in the avatar slot. The island
  (mounted only when Blazor scripts load) subscribes exactly as MainLayout does today
  (`IUiNotificationHub.Subscribe<ImportStatusUpdatedEvent/ImportStatusErrorEvent>` on
  `UiTopics.User(id)`, MainLayout 585–620) and toggles the `hidden` attribute via JS interop.
  No layout coupling, no visible island markup. Pure-static pages (no circuit) show no pulse — accepted.
- **D8 — Bottom-nav active state**: server-rendered from `HttpContext.Request.Path` (same
  prefix rules as `BnClass`, MainLayout 471–478); `nav.js` re-evaluates on SPA navigation by
  wrapping `history.pushState`/`replaceState` + `popstate` (Blazor Server navigations go through
  pushState). MainLayout's `OnLocationChanged` keeps calling `pageDock.reset` (485).
- **D9 — Culture cookie: set only when missing.** `_Host.cshtml` (8–16) and `FrontDoor.cshtml`
  (16–19) append the culture cookie on **every** response — an unconditional `Set-Cookie` makes
  every page uncacheable. Change both to: append only if the request carried no culture cookie or
  its value differs from the resolved culture. Explicit changes already go through
  `/Culture/Set`. ⚠ AUDIT: after C4, `curl -sI` an anon page twice and assert **no `Set-Cookie`**
  on the second request.
- **D10 — Cache policy.** Origin: ASP.NET `OutputCache` (net10 built-in; not currently
  registered) — anon GETs only, vary by (path, query, resolved UI culture, `CurrentMix` cookie),
  TTL from config `StaticShell:AnonCacheSeconds` (default 300). CDN (owner infra): cache only
  when neither `.AspNetCore.DefaultAuthentication` nor `.AspNetCore.ExternalAuthentication`
  cookie is present; key on (URL, culture cookie, mix cookie); never cache `/_blazor*`, `/api/*`,
  `/dev/*`, `/hangfire*`, `/Login*`, `/Culture/*`, `/Mix/*`, `/swagger*`. Known accepted gap:
  cookieless first-time visitors get the edge's default-culture copy until they pick a locale
  (origin still localizes on every miss; `/Culture/Set` writes the cookie → new cache key).
- **D11 — MainLayout keeps its non-chrome jobs**: the four Mud providers, `ChartVideoDisplay`,
  theme resolution for the *Mud* palette (`MudThemeProvider`), the legacy-mix body hold +
  `RedirectIfGated`, the PageDock slot + Recap popup, `OnLocationChanged`. The `--mix-*` CSS-var
  `<style>` block (30–32) MOVES to the layout (server-side) — delete it from MainLayout.
- **D12 — `LegacyMixGate` applies server-side** to nav filtering (`Services/LegacyMixGate.cs`,
  same `IsGatedMix`/`IsGated` calls MainLayout uses). Page-level gating stays in MainLayout (D11).
- **D13 — No feature flag.** Straight cutover on this branch, FT-gated (owner field tests at C2
  and C4). A `StaticShell` flag would double the test matrix; production deploys are already
  approval-gated.
- **D14 — `_Layout.cshtml` retires.** `_SiteLayout.cshtml` becomes the one layout. Consumers to
  migrate: `_Host.cshtml` (`Layout = "_Layout"`) and `FrontDoor.cshtml`'s ShowApp branch (line 15).
  ⚠ AUDIT: `grep -rn "_Layout" ScoreTracker/ScoreTracker/Pages` for stragglers (e.g. `Error` page).

## 4. File-by-file plan

### NEW `Pages/Shared/_SiteLayout.cshtml`

The one full-page layout. Structure (pseudo-markup — implement with real tag helpers):

```cshtml
@inject ScoreTracker.Web.Services.ShellModelFactory ShellFactory
@{ var shell = await ShellFactory.BuildAsync(HttpContext, includeBlazorScripts: <flag>); }
<!DOCTYPE html>
<html lang="@System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName">
<head>
    <meta charset="utf-8" /> <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="~/" />
    @* same font/Mud/site/charts CSS links as today's _Layout (11–14), asp-append-version *@
    <style>@Html.Raw(MixThemes.CssVariablesFor(shell.ThemeMix))</style>
    @if (shell.IncludeBlazorScripts) { <component type="typeof(HeadOutlet)" render-mode="Server" /> }
    @await RenderSectionAsync("Head", required: false)   @* wave-1 pages own title/meta/OG/JSON-LD here *@
    @* Clarity snippet + ApexCharts override <style> verbatim from today's _Layout (16–39) *@
</head>
<body class="@MixThemes.CssClassFor(shell.ThemeMix)">
    <partial name="_Shell" model="shell" />
    <main class="shell-main">@RenderBody()</main>
    @if (shell.IncludeBlazorScripts)
    {
        <div id="blazor-error-ui">…verbatim from _Layout 45–54…</div>
        <component type="typeof(ScoreTracker.Web.Components.ShellImportPulse)" render-mode="Server" />
        <script src="_framework/blazor.server.js" asp-append-version="true"></script>
        <script src="_content/MudBlazor/MudBlazor.min.js" asp-append-version="true"></script>
    }
    <script src="/js/page-dock.js" asp-append-version="true"></script>
    <script src="/js/nav.js" asp-append-version="true"></script>
    @if (shell.IncludeBlazorScripts)
    {
        <script src="~/js/dashboard-grid.js" asp-append-version="true"></script>
        <script src="~/js/credential-storage.js" asp-append-version="true"></script>
    }
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

Notes: `IncludeBlazorScripts` is true for `_Host`, false for static pages (mechanism: `ViewData`
flag read by the layout — pick one mechanism, then record it in §7). The mix CSS class moves to
`<body>` (today it sits on `MudLayout`, MainLayout 65 + 445–448). AppInsights: today only the
*Blazor* AppInsights package is wired (`AddBlazorApplicationInsights`); the production HTML also
carries a JS snippet — ⚠ AUDIT where that's injected before assuming it belongs in the layout.

### NEW `Pages/Shared/_Shell.cshtml` (partial, model = `ShellViewModel`)

Renders: `<header class="shell-appbar">` — markup parity with MainLayout 68–252: wordmark link,
the same nav groups with the same items, same `L[…]` keys, same auth/gate/mix conditionals
(`IsLoggedIn`, `IsGatedMix`, XX→`/Progress` vs `/Phoenix/Progress`), mix pill as a static menu of
`/Mix/Set` links over `MixEnum` values, avatar block with the `onerror`-fallback `<img>` exactly
as MainLayout 230–232, the hidden import-pulse span (D7), sign-in icon link when anonymous.
Then the mobile More sheet markup and `<nav class="bottom-nav">` (same items, same `bn`/`bn-wide`
classes, active class from `Model.ActivePath`). **Menu containers are `<div class="shell-menu"
data-menu>` with an activator button + panel** — `nav.js` owns open/close. Desktop/mobile split
via `shell-desktop-only` / `shell-mobile-only` classes + the 960px media queries, NOT MudHidden.

Localization: reuse the exact existing resx keys from MainLayout — target **zero new keys** (any
unavoidable new key lands in all nine locales in the same commit, repo rule). No color literals —
tokens only (`UiColorTokenTests` scans `Pages/`). Highlighted-tournament entries (MainLayout
138–152) render from `Model.HighlightedEvents` with the same three link forms (Stamina /
LinkOverride / Qualifiers).

### NEW `Services/ShellModelFactory.cs` + `ShellViewModel`

```csharp
public sealed record ShellViewModel(
    bool IsLoggedIn, Guid? UserId, string? DisplayName, string? GamerTag, string? AvatarUrl,
    MixEnum CurrentMix, MixEnum ThemeMix, bool IsGatedMix, bool HasRecap,
    IReadOnlyList<TournamentRecord> HighlightedEvents, string ActivePath, bool IncludeBlazorScripts);
```

`ShellModelFactory` (register **scoped**, plain class — not a MediatR handler) builds it from:

| Field | Source | Cost / cache |
|---|---|---|
| IsLoggedIn / UserId / DisplayName | `HttpContext.User` claims | free |
| GamerTag, AvatarUrl | `GetUserUiSettingsQuery` (keys `GameTag`, `ProfileImage`; same defaults as MainLayout 573–581) | `IMemoryCache` per user, 5 min |
| CurrentMix | signed-in: same settings dict, key `Universal__CurrentMix`; anon: `CurrentMix` cookie else `MixEnum.Phoenix` | free / cached |
| ThemeMix | `MixThemes.ResolveThemeMix(settings[MixThemes.OverrideSettingKey], CurrentMix)`; anon: CurrentMix | cached |
| IsGatedMix | `LegacyMixGate.IsGatedMix(CurrentMix)` | free |
| HasRecap | `GetPlayerRecapQuery(userId) != null` (MainLayout 563) | `IMemoryCache` per user, 30 min |
| HighlightedEvents | `GetAllTournamentsQuery` → `Where(IsHighlighted)` (MainLayout 551–552) | `IMemoryCache` global, 15 min |
| ActivePath | `HttpContext.Request.Path` | free |

⚠ NEVER call `IUiSettingsAccessor` for anonymous values here (D6 landmine).

### NEW `Controllers/MixController.cs`

Clone CultureController's shape: `[Route("[controller]")]`, `[ApiExplorerSettings(IgnoreApi = true)]`,
`GET Set(string mix, string redirectUrl)` → validate `Enum.TryParse<MixEnum>`; if
`ICurrentUserAccessor.IsLoggedIn` → send `SaveUserUiSettingCommand("Universal__CurrentMix", mix)`;
**always** also append the `CurrentMix` cookie (keeps anon working and keeps the CDN cache key
honest after logout); `LocalRedirect(redirectUrl)`. Note: the cookie-policy middleware
(Program.cs 255–259) forces every appended cookie to 30-day expiry — acceptable, don't fight it.

### NEW `wwwroot/js/nav.js` (~150 lines, zero dependencies)

Contract: (1) menu toggling for `[data-menu]` (activator click toggles `.open`; opening one
closes others; outside click + Escape close; any `<a>` click closes). (2) More sheet open/close
(`#shell-more-btn` ↔ `.more-sheet.open`). (3) `shell.refreshActiveNav()` — compares
`location.pathname` prefixes against each `.bn[data-href]` (same rules as old `BnClass`), toggles
`.active`; wraps `history.pushState`/`replaceState` and listens to `popstate` so Blazor SPA
navigations re-evaluate. (4) `shell.setDockState(hasDock, focusMode)` — toggles `html.has-dock` /
`html.focus-mode`. (5) `shell.setImportPulse(bool)` — toggles `hidden` on `#shell-import-pulse`.
(6) on `DOMContentLoaded`: `pageDock.watch()` + `refreshActiveNav()`. No colors, no `innerHTML`.

### NEW `Components/ShellImportPulse.razor`

Island root component; renders nothing visible. If `ICurrentUserAccessor.IsLoggedIn`: subscribe
exactly as MainLayout 585–620 (`UiTopics.User(id)`; `ImportStatusUpdatedEvent` → running =
`Status != "Charts finished saving"`; `ImportStatusErrorEvent` → stop) and call
`JSRuntime.InvokeVoidAsync("shell.setImportPulse", running)` on change (guard interop for
disconnects). Implements `IDisposable` with MainLayout's defensive null pattern (502–511).

### MODIFIED `Pages/_Host.cshtml`

Becomes: `@page` + `Layout = "_SiteLayout"` + culture-cookie append **only when missing/stale**
(D9) + `<component type="typeof(App)" render-mode="Server" />` as the body, `IncludeBlazorScripts`
= true. The root-route comment stays accurate (FrontDoor owns `/`).

### MODIFIED `Shared/MainLayout.razor` — the gutting (keep/delete against current file)

KEEP: 22–28 (PageTitle, four providers, ChartVideoDisplay); gate hold + `@Body` + `MudContainer`
(372–394, minus `MudLayout`/`MudAppBar`/`MudMainContent` wrappers — content width parity via
`MudContainer` remains); Recap dialog (413–425) + its init (560–570); `PageDockService` wiring
with `OnDockChanged` → now `JSRuntime.InvokeVoidAsync("shell.setDockState", hasDock, focusMode)`
instead of re-rendering `LayoutClass`; the PageDock content slot (342–347 — fixed-position, DOM
location irrelevant); `OnLocationChanged` (480–487: keep gate redirect + `pageDock.reset`; drop
`_showMore`); theme resolution for `MudThemeProvider` (518–550 **minus** the CSS-var style
block); `RedirectIfGated`; `Dispose` (minus deleted subscriptions).

DELETE: `MudLayout`/`MudAppBar` + entire top nav (65–252); More-sheet drawer (253–335); bottom
nav (337–371); `GoToChart` (463–467); `BnClass` (469–478); `SetMix` (530–534); CSS-var style
block (29–32); dense-nav/drawer `<style>` (33–64) ⚠ AUDIT for other consumers before deleting;
B1G ONE popup + fields + init (396–408, 512, 515, 517, 554–559); the **dead** stats subscription
(`_statsSubscription`, `Rating`, `OnPlayerStatsUpdated`, the `GetPlayerStatsQuery` call at 582);
avatar/profile fetching (571–581 → ShellModelFactory); import-status handlers (585–620 →
ShellImportPulse); tournaments query (551–552 → factory).

### MODIFIED `Pages/FrontDoor.cshtml`

ShowApp branch: `Layout = "_SiteLayout"`. Static branch: adopt `_SiteLayout` with
`IncludeBlazorScripts = false` (its meta/title move into the `Head` section; front-door.css link
stays) and delete the bespoke `<header class="topbar">` (80–88) — the shared shell renders the
anon variant. Retire the orphaned `.topbar/.topnav` rules from front-door.css. Apply the D9
culture-cookie guard here too (16–19).

### MODIFIED `Services/UiSettingsAccessor.cs`

`GetSelectedMix` anon path: read the `CurrentMix` cookie via `IHttpContextAccessor` (works in
circuits AND MVC) instead of `ProtectedLocalStorage`. `SetSelectedMix` anon path: write the same
cookie via JS interop, or drop the write path entirely if the shell endpoint is the only caller —
⚠ AUDIT `grep -rn "SetSelectedMix" ScoreTracker/ScoreTracker` and migrate any other caller. All
other settings keep ProtectedLocalStorage (only read inside circuits).

### MODIFIED `wwwroot/css/site.css`

Re-scope the Mud-coupled selectors (line refs @ `01904329`): `.has-dock .mud-main-content` (249)
→ `html.has-dock .shell-main`; `.focus-mode .mud-appbar` (301) → `html.focus-mode .shell-appbar`;
`.focus-mode .mud-main-content` (305) → `html.focus-mode .shell-main`; `.focus-mode .page-dock`
(287) → `html.focus-mode .page-dock`. Add `.shell-appbar` (fixed, `--shell-appbar-h` height var,
background `var(--mix-…)` appbar token — match the current MudAppBar look) and `.shell-main {
padding-top: var(--shell-appbar-h) }` (replaces MudAppBar/MudMainContent spacing). Add More-sheet
open/closed transitions (replacing MudDrawer's) and the `shell-desktop-only`/`shell-mobile-only`
helpers at the 960px seam. `html.nav-away .bottom-nav` (267) already works unchanged.
`.appbar-search .mud-*` rules stay (the search island still renders Mud inputs).

### MODIFIED `Program.cs`

Add `builder.Services.AddOutputCache(...)` with an `"AnonShell"` policy — the **dimensions** are
the spec (⚠ verify exact net10 API): GET only; skip when `HttpContext.User.Identity.IsAuthenticated`
or when an `.AspNetCore.DefaultAuthentication` cookie is present; vary by path + query + resolved
`CultureInfo.CurrentUICulture.Name` + `CurrentMix` cookie value; expire after
`StaticShell:AnonCacheSeconds` (default 300). `app.UseOutputCache()` goes **after
`UseAuthorization()`, before `UseEndpoints`**; apply the policy to `MapRazorPages()` and the
`_Host` fallback via endpoint conventions. Register `ShellModelFactory` scoped next to the other
Web services (~lines 247–254).

### UNCHANGED but load-bearing

`wwwroot/js/page-dock.js` (already vanilla; `nav.js` takes over calling `watch()`),
`Services/LegacyMixGate.cs`, `MixThemes`, `CultureController`, `FrontDoor.cshtml.cs` (dispatcher).

## 5. Verification (per-commit gates)

- View-source contains the nav **before any circuit**:
  `curl -s https://localhost:<port>/TierLists | grep -c "shell-appbar\|bottom-nav"` ≥ 1.
- Cacheability: two anonymous `curl -sI` in a row → second has **no `Set-Cookie`**, has
  `Cache-Control`; authenticated request (send the auth cookie) → bypass/no-store. `/_blazor`
  untouched by caching.
- All five suites green: `ScoreTracker.Tests`, `.Api`, `.Components` (fast), `.Integration`,
  `.E2E` (Docker). ⚠ AUDIT selectors first:
  `grep -rn "AppBar\|bottom-nav\|top-nav\|More\|mix-pill" ScoreTracker/ScoreTracker.Tests.E2E
  ScoreTracker/ScoreTracker.Tests.Components` — update to the static markup; add `data-testid`
  where old selectors were Mud-DOM-shaped.
- FT matrix (owner): every top-level page × {desktop 1280, tablet 768, phone 390} ×
  {anon, signed-in} × {Phoenix, XX, one gated legacy mix} — nav parity, menus, More sheet, dock
  pages (randomizer), focus-mode flow, mix switch, locale switch, avatar fallback.

## 6. Build plan

| # | Commit | Contents |
|---|---|---|
| C1 | Shell partial + factory + nav.js | `_SiteLayout`, `_Shell`, `ShellViewModel/Factory`, `nav.js`, site.css additions; **FrontDoor adopts it** (first consumer, both branches) — FT0: front door visually unchanged |
| C2 | The cutover | `_Host` → `_SiteLayout`; MainLayout gutted per §4; `MixController`; `UiSettingsAccessor` cookie migration; site.css re-scopes; dead-code deletions — **FT1: full §5 matrix** |
| C3 | Islands | `ShellImportPulse` + layout mount; app-bar `ChartSelector` island on Blazor pages (own root, shares the circuit); static `/Charts` link fallback elsewhere |
| C4 | Cacheability | D9 culture-cookie guard (both writers); `AddOutputCache` + policy + config knob; response-header audit — **FT2: curl checks green; CDN dry-run** |
| C5 | Tests + docs | E2E/bUnit selector updates + new E2E facts (anon HTML contains nav pre-circuit; second anon response sets no cookie); ARCHITECTURE.md shell paragraph; UX-GUIDELINES.md shell note; CLAUDE.md if conventions shift; sync this doc |

## 7. The shell contract (wave 1 codes against this — keep stable, edit loudly)

- Layout: `Pages/Shared/_SiteLayout.cshtml`. A page owns a `Head` section (title/meta/OG/JSON-LD)
  and optional `Scripts` section. `IncludeBlazorScripts` opt-in flag (islands require true).
- Model: `ShellViewModel` as §4, built by scoped `ShellModelFactory.BuildAsync(HttpContext, …)`.
- Islands: `<component type="typeof(X)" render-mode="Server" param-… />` anywhere in the body;
  multiple roots share one circuit and its scoped DI. Never `ServerPrerendered`. MudBlazor
  providers: wave 2's islands ride MainLayout's providers on Blazor pages; **wave 1 defines the
  `IslandRoot` provider wrapper** for islands on circuit-less-shell pages — if wave 2 ever needs
  one first, build it under that name and update this line (one component, not two).
- JS: `shell.setDockState`, `shell.setImportPulse`, `shell.refreshActiveNav` are public and
  stable; `html.nav-away` / `html.has-dock` / `html.focus-mode` are the class contract.
- Caching: pages opt into the `AnonShell` output-cache policy; anything personalized must not.

## 8. Risks

- **Visual parity** is the main risk surface: fixed-header offset, menu focus/keyboard behavior,
  sheet height/scroll, safe-area insets, the 960px seam. Mitigation: D3 same-classes + §5 matrix.
- **Two root components, one circuit**: scoped services (PageDockService, IUiNotificationHub
  subscriptions) are shared across roots on the same circuit — verify disposal on teardown
  (MainLayout's defensive-NRE comment, 502–505).
- **Cache poisoning**: a personalized fragment cached once is a data leak. The anon-only
  predicate is the guard; C4 explicitly tests signed-in → bypass.
- **Stray `Set-Cookie`** from any middleware silently kills CDN cacheability — audit headers.
- **resx churn**: target zero new keys; any new key → all nine locales in the same commit.

## 9. Owner decisions

Resolved (execute with these; they were confirmed in the 2026-07-14 follow-up):

- Anonymous mix persistence moves to a cookie; the one-time anon reset is accepted (D6).
- App-bar search on circuit-less pages = plain `/Charts` link in v1 (vanilla autocomplete
  endpoint is a possible later upgrade, out of scope).
- Import pulse renders only where Blazor scripts load (D7); pure-static pages show none.
- Straight cutover, no feature flag (D13). `_Layout.cshtml` retires (D14).

Open (proceed with the default; flag in the PR if it bites):

- **CDN product/TTL**: the app emits correct headers + a 300s origin TTL default; edge rules per
  D10 are owner infra. Default assumption: bypass-on-cookie is available.
- **Edge culture gap** (D10): cookieless non-English first hits may get the default-culture copy
  from the edge until a locale is picked. Accepted for v1.
