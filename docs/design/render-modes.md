# Render modes — design & scoping stub

Stage 2 of the SSR/islands migration. **Scoping not finished** — §7 lists what's still unknown.
Everything in §1–§6 is established fact or a decided call, checked against the code at
`01904329` + the Stage 1 branch. Written to be picked up cold: line numbers, versions and
current-state inventory are here so a fresh session doesn't re-derive them.

Stage map and terminology live in [static-shell.md](static-shell.md). Read that first.

---

## 1. Why

The app targets **`net10.0`** but runs the **.NET 6-era Blazor Server hosting model**.

.NET 8 introduced render modes: components render **static SSR by default**, and
`@rendermode InteractiveServer` on a specific component makes *that component* an island.
Static SSR has a live `HttpContext`. That is the architecture this migration aims at — the
framework grew it and this app predates it.

Everything Stage 1 hand-builds — a static shell hosted from a cshtml layout, a root-component
parameter to carry the mix into the circuit, an `IslandRoot` provider wrapper — is a workaround
for the hosting model. Stage 2 removes the need for them. Stage 3 then becomes "drop
`@rendermode` from a page."

## 2. Current-state inventory

### Versions

| | |
|---|---|
| TFM | `net10.0` (`ScoreTracker.Web.csproj:4`) |
| Framework assets | `microsoft.aspnetcore.app.internal.assets/10.0.9` |
| MudBlazor | `8.15.0` (`ScoreTracker.Web.csproj:32`) — supports Blazor Web Apps |

### `Program.cs` — every line Stage 2 touches or must not break

| Line | Current | Stage 2 |
|---|---|---|
| 51 | `AddRazorPages(o => o.Conventions.AddPageRoute("/FrontDoor", "Login"))` | see §4 — the hack may disappear |
| 52–55 | `AddServerSideBlazor(o => o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromHours(1))` | → `AddRazorComponents().AddInteractiveServerComponents(o => o.DisconnectedCircuitRetentionPeriod = …)`. **Do not drop the retention period.** |
| 204–205 | `AddScoped<ICurrentUserAccessor, HttpContextUserAccessor>()`, `AddScoped<AmbientUserContext>()` | unchanged; see §7 Q3 |
| 209 | `AddSingleton<IUiNotificationHub, UiNotificationHub>()` | unchanged |
| 246–254 | Web scoped services (`IStringLocalizer<App>`, `ChartVideoDisplayer`, `ChartScoringLevels`, `PageDockService`, `IImportCredentialClientStore`, HomeDashboard `ChartCatalogCache`/`ByLevelDataSource`/`CommunityGlowReader`) | + Stage 1's `ShellModelFactory`; **`ShellContext` deletes** (§5) |
| 255–259 | `AddCookiePolicy(o => o.OnAppendCookie = … 30 days)` | ⚠ **`app.UseCookiePolicy()` is NEVER called** — the middleware doesn't run. Any cookie needs explicit `Expires`. |
| 261 | `var app = builder.Build();` | |
| 264–270 | security-headers middleware (`X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`) | unchanged; emits no `Set-Cookie` |
| 274 | `ApplyOrReportMigrationsAsync(AutoMigrate)` | unchanged |
| 277–280 | `UseRequestLocalization` — 9 cultures (`en-US, pt-BR, ko-KR, en-ZW, es-MX, es-ES, fr-FR, ja-JP, it-IT`), default `en-US` | unchanged |
| 282–287 | `UseExceptionHandler("/Error")` + `UseHsts()` (non-dev) | confirm `/Error` still resolves |
| 289–290 | `UseHttpsRedirection()`, `UseStaticFiles()` | unchanged |
| 295–322 | legacy tier-list 301 redirect middleware (`/ChartSkills`, `/PersonalizedTierList`, `/TierLists/Old`, `/TierLists?Difficulty=`) | unchanged; emits no `Set-Cookie` |
| 324–325 | `UseSwagger()`, `UseSwaggerUI()` | confirm ordering survives |
| 326–330 | `UseRouting()`, `UseCors()`, `UseAuthentication()`, `UseAuthorization()` | unchanged |
| 332 | `UseHangfireDashboard("/hangfire", …)` | confirm ordering survives |
| 375 | `app.UseEndpoints(e => e.MapControllers())` | unchanged (`api/*`, `dev/export/*`, login/logout/culture) |
| 378 | `app.MapDefaultEndpoints()` (Aspire) | unchanged |
| 381 | `app.MapRazorPages()` | stays (front door, Error, Privacy) — **but see §4** |
| 382 | `app.MapBlazorHub()` | → `MapRazorComponents<App>().AddInteractiveServerRenderMode()` |
| 383 | `app.MapFallbackToPage("/_Host")` | **deletes** — component routes become real endpoints |

**No `UseResponseCompression` anywhere.** Rides on Azure App Service's IIS dynamic compression.
See static-shell.md §10 — Stage 1 settles this.

### Blazor hosting files

**`Pages/_Host.cshtml`** (20 lines) — `@page`, `Layout = "_Layout"`, an **unconditional
culture-cookie append** (10–15), `<component type="typeof(App)" render-mode="Server" />` (19).
**Dies in Stage 2.**

**`Pages/_Layout.cshtml`** (62 lines) — **dies in Stage 1** (replaced by `_SiteLayout.cshtml`),
which then dies in Stage 2. Its contents must survive into `App.razor`'s head/body:
- 6: `<html lang="en">` — **hardcoded**; Stage 1 fixes to `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName`
- 11–14: Roboto font link, `_content/MudBlazor/MudBlazor.min.css`, `/css/site.css`, `/css/charts.css` (all `asp-append-version`)
- 15: `<component type="typeof(HeadOutlet)" render-mode="Server" />` — see §7 Q2
- 16–22: Clarity snippet (tag id `d2n3fzq6v8`)
- 23–39: ApexCharts override `<style>` — **contains 4 hex literals**, invisible to `UiColorTokenTests` (it scans `.razor`/`.cs`, not `.cshtml`). Porting them into a `.razor` root makes them visible → tokenize or allowance.
- 45–54: `blazor-error-ui` markup with `<environment>` tag helpers — ⚠ **`<environment>` is an MVC tag helper and does not exist in `.razor`.** Needs `IWebHostEnvironment` injection instead.
- 56–60: scripts — `blazor.server.js` (→ **`blazor.web.js`**), `MudBlazor.min.js`, `page-dock.js`, `dashboard-grid.js`, `credential-storage.js` (+ Stage 1's `nav.js`)

**`App.razor`** (13 lines) — currently just `<Router AppAssembly="@typeof(App).Assembly">` with
`RouteView DefaultLayout="@typeof(MainLayout)"`, `FocusOnNavigate Selector="h1"`, and a
`NotFound` `LayoutView`. In Blazor Web App, **`App.razor` becomes the HTML document root** and
the Router moves to a new **`Routes.razor`**. That's a rename + split, and `typeof(App).Assembly`
must follow.

**`Pages/FrontDoor.cshtml`** (381 lines) — `@page "/"`, two branches (`Model.ShowApp` →
`Layout = "_Layout"` + `<component typeof(App)>` at 28; else `Layout = null` + a full static
HTML document). Unconditional culture-cookie append at 16–19. See §4.

**`Pages/Error.cshtml`, `Pages/Privacy.cshtml`** — standalone (own DOCTYPE, no layout). Not
touched by Stage 1. `Privacy.cshtml` carries 7 hex literals and a stray `<PageTitle>` tag that
renders as literal unknown-element markup (pre-existing; not this migration's problem).

**No `_ViewImports.cshtml` / `_ViewStart.cshtml` exist.** Each cshtml declares its own
`@using`/`@addTagHelper`/`@inject`.

### `<component>` call sites (all become `@rendermode`)

| Site | Today |
|---|---|
| `_Host.cshtml:19` | `typeof(App)` @ `Server` (+ Stage 1's `param-CurrentMix`) |
| `FrontDoor.cshtml:28` | `typeof(App)` @ `Server` (+ Stage 1's `param-CurrentMix`) |
| `_Layout.cshtml:15` | `typeof(HeadOutlet)` @ `Server` |
| Stage 1 adds | `Shell` @ `Static`; `ShellImportPulse`, `AppBarSearch` @ `Server` |

⚠ **Two different `RenderMode` types.** The cshtml tag helper's
`Microsoft.AspNetCore.Mvc.Rendering.RenderMode` (`Static`/`Server`/`ServerPrerendered`) is the
**legacy** enum — that's what Stage 1 uses. Blazor Web App uses
`Microsoft.AspNetCore.Components.Web.RenderMode` (`InteractiveServer`, …) via `@rendermode`.
Same word, different types, different eras. Don't conflate them when reading old code.

## 3. Decided

- **Prerendering stays off, permanently.** Blazor Web App's `InteractiveServer` **prerenders by
  default** — the exact app-wide flip that broke the site before. It's a constructor argument
  now, not a fate:

  ```razor
  <Routes @rendermode="new InteractiveServerRenderMode(prerender: false)" />
  ```

  Nothing else is acceptable. Consequence, already absorbed by the chart-details session: no
  `PersistentComponentState`, no double-render machinery, anywhere.

- **The target shape:**

  ```razor
  @* App.razor — the document root, statically rendered *@
  <!DOCTYPE html>
  <html lang="@CultureInfo.CurrentUICulture.TwoLetterISOLanguageName">
  <head>
      …fonts/Mud/site/charts CSS, theme <style>, static <title>, Clarity, ApexCharts block…
      <HeadOutlet @rendermode="…" />        @* see §7 Q2 *@
  </head>
  <body class="@MixThemes.CssClassFor(themeMix)">
      <Shell />                                                              @* static *@
      <main class="shell-main">
          <Routes @rendermode="new InteractiveServerRenderMode(prerender: false)" />
      </main>
      …blazor-error-ui, blazor.web.js, MudBlazor.min.js, nav.js, page-dock.js, …
  </body>
  </html>
  ```

  Stage 1's shell components move here **unchanged** — that portability is why Stage 1 builds
  them as static `.razor` rather than cshtml markup (static-shell.md D2).

- **Zero intended behavior change.** Every page stays interactive via `<Routes>`. Nothing goes
  static in this stage. QA is light — but not zero (§7 Q1).

- **MudBlazor stays.** Islands get full MudBlazor as today. Static regions get Mud's *display*
  components (`MudIcon`, `MudText`, `MudPaper`, `MudAvatar`) plus the repo's own vocabulary
  (`DifficultyBubble`, `LetterGradeIcon`, `ScoreBreakdown`, `UserLabel`). The line is **display
  vs. interaction**, not "does it render" — in a static region `OnAfterRenderAsync` never runs,
  so a `MudButton` looks perfect and does nothing.

- **Stage 1 debt this retires:** `ShellContext` + the `param-CurrentMix` root parameter (static
  SSR reads `HttpContext` directly, so `UiSettingsAccessor.GetSelectedMix()`'s anon path reads
  the cookie without ceremony); `_SiteLayout.cshtml`; `_Host.cshtml` and its culture-cookie
  write. `ShellModelFactory` may survive for its `IMemoryCache` layer — decide at scoping.

## 4. ⚠ Known blocker: the `/` route conflict

**Two files declare `@page "/"`:**

- `Pages/FrontDoor.cshtml:1` — the Razor Page
- `Pages/HomeDashboard.razor:1` — the Blazor component (`:2` also declares `/Home`)

**Today this works by route priority, not by design.** `MapRazorPages()` (381) registers `/` as
a real endpoint; `MapFallbackToPage("/_Host")` (383) is a **fallback** — the lowest possible
priority. So `/` resolves to FrontDoor. `HomeDashboard`'s `@page "/"` only ever resolves
*client-side*, inside the Blazor Router, after `_Host` boots the app — which happens only for
signed-in visitors via FrontDoor's `ShowApp` branch. The "dispatcher" ([front-door.md](front-door.md))
depends on the Blazor route table being client-side-only.

**Blazor Web App breaks that.** `MapRazorComponents<App>()` registers **real server-side
endpoints for every `@page` route**. So `/` gets registered by both `MapRazorPages()` (FrontDoor)
and `MapRazorComponents<App>()` (HomeDashboard) → **`AmbiguousMatchException`**.

Options (decide at scoping):

1. **Front door becomes a static component.** `@page "/"` + `@page "/Login"` on a static-rendered
   component that reads `HttpContext`, does the dispatch, and renders either the front-door
   markup or the dashboard. This is the natural Blazor Web App shape, and it dissolves the
   `AddPageRoute("/FrontDoor", "Login")` hack — Program.cs:49–50's comment says the hack exists
   only because "a Razor Page can declare only one route," which stops being true. **Cost:** it
   pulls the front door into Stage 2, against "zero behavior change." Output should be identical
   (it's already a static HTML document with no circuit), but it's a real rewrite of a 381-line
   page and it owns the sign-in flow.
2. **Drop `@page "/"` from HomeDashboard**, keep `/Home`, and have FrontDoor's ShowApp branch
   render the dashboard some other way. Smaller, but leaves the dispatcher's plumbing odd.
3. **Route priority / explicit `Order`.** Cheapest, most fragile; fights the framework.

Leaning 1. It is the only option that leaves the routing legible afterwards.

## 5. Owned by this stage

- **Culture cookie: set only when missing.** `_Host.cshtml` (10–15) and `FrontDoor.cshtml`
  (16–19) append the culture cookie on **every** response. An unconditional `Set-Cookie` makes
  every response uncacheable. Change to append only when the request carried no culture cookie
  or its value differs from the resolved culture; explicit changes already go through
  `/Culture/Set`. Lands here rather than Stage 1 because `_Host.cshtml` dies here — writing it
  in Stage 1 means writing it into a corpse. **Prerequisite for the output caching that lands at
  Stage 3.**
  Verify: `curl -sI` an anon page twice → **no `.AspNetCore.Culture` `Set-Cookie`** on the second.

  Confirmed live 2026-07-14 — a cookieless GET of `/TierLists` returns
  `Set-Cookie: .AspNetCore.Culture=c%3Den-US%7Cuic%3Den-US; path=/`. It also returns
  `Cache-Control: no-cache, no-store, max-age=0` and **two Azure ARR affinity cookies**; see
  static-shell.md D18 — fixing the culture cookie is necessary but **not sufficient** for a
  cacheable anonymous response.
- The hosting swap (§2 table, lines 51–55, 381–383).
- Every `<component>` call site → `@rendermode` (§2).
- `App.razor` → `App.razor` (document root) + `Routes.razor` (the Router).
- `blazor.server.js` → `blazor.web.js`.
- The `/` route conflict (§4).
- `<environment>` tag helper → `IWebHostEnvironment` in the error-UI markup (§2).

## 6. Verified against shipped source

Facts established during Stage 1 scoping that Stage 2 inherits. Re-check before contradicting.

- **Every `--mud-*` var is circuit-only.** `MudBlazor.min.css` has **zero** `--mud-palette-*`
  declarations; `--mud-elevation-4` and `--mud-zindex-appbar` are absent too. `MudThemeProvider`
  emits them at runtime. The shipped stylesheet styles `body` entirely from them, so any static
  region that uses `var(--mud-…)` FOUCs to black-on-white-serif. **Static regions are
  `--mix-*`-only.** (static-shell.md D5/§9.)
- **`MudIcon` renders statically** — `.mud-icon-root` / `.mud-icon-size-small` are in the static
  stylesheet, not the palette vars.
- **`ZIndex.AppBar` = 1300** (Drawer 1100, Popover 1200, AppBar 1300, Dialog 1400, Snackbar 1500,
  Tooltip 1600). `MixThemes` overrides none.
- **A static `<title>` is safe.** `getAndRemoveExistingTitle()` removes any `<title>` not
  preceded by a Blazor marker comment on boot and returns its text as the fallback. It does
  **not** remove `<meta>`.
- **Blazor's link interception is a global document listener** — qualification is
  `(!target||"_self"===target) && hasAttribute("href") && !hasAttribute("download")`, then
  `isWithinBaseUriSpace` → `preventDefault()` + internal routing. Catches anchors outside the
  component tree. → static-shell.md D8's `target="_top"` rule on `/Mix/Set`, `/Culture/Set`,
  `/Login`, `/Logout`.
- **Blazor navigation goes through `history.pushState`/`replaceState`.**
- **`/Login` is a Razor Page route** (`AddPageRoute("/FrontDoor", "Login")`, Program.cs:51);
  **`/Logout` is MVC-only** (`LogoutController`, `[Route("[controller]")]` + `[HttpGet("")]`).
  Neither is a Blazor route. Both are linked from the shell today (MainLayout 244, 250) and both
  work — so MudBlazor's `Href` dodges interception by a mechanism not yet identified.
- **8 pages emit `<HeadContent>` SEO metadata no crawler has ever seen** — `ChartDetails`,
  `ChartSkills` (incl. the daily-regenerated folder share card), `PersonalizedBreakdown`,
  `ChartRandomizer`, `LifeCalculator`, `PhoenixCalculator`, `PhoenixToXXCalculator`. All route
  through `HeadOutlet` → the circuit. Production HTML has no `<title>` at all. **This is the
  payoff, and it lands at Stage 3.**
- **`AmbientUserContext` is a background-job seam, not a render seam.**
  `Accessors/HttpContextUserAccessor.cs:22` memoizes because HttpContext goes null once the
  *request* completes and background-driven re-renders then see null. It is **not** evidence that
  static SSR can't read HttpContext — static SSR runs inside a live request.

## 7. Open questions — what scoping must answer

1. ~~**Enhanced navigation.**~~ **ANSWERED — it can be turned off with one attribute.** From
   `blazor.web.js` (net10):

   ```js
   const t = e.closest("[data-enhance-nav]");
   if (t) { const e = t.getAttribute("data-enhance-nav");
            return "" === e || "true" === e.toLowerCase(); }   // false → NOT enhanced
   return !0;                                                   // default: enhanced
   ```

   `closest()` walks **up** the tree, so the attribute is *inherited*, not per-link:
   `<body data-enhance-nav="false">` disables it app-wide. **Stage 2 ships with it off**, which
   makes "zero intended behaviour change" literally true — `blazor.web.js` swaps in but
   navigation keeps working exactly as it does today. Enhanced nav then becomes its own opt-in
   change, with its own field test, and the interactions that worried us (`page-dock.js`'s
   scroll listeners and `html.nav-away`, **ApexCharts re-initialisation**, Clarity,
   `dashboard-grid.js`, `credential-storage.js`) get examined then rather than as a rider.
   A narrower lever exists if we later want enhanced nav without DOM patching:
   `Blazor.start({ ssr: { disableDomPreservation: true } })`.

   ⚠ Still open when we do opt in: whether enhanced nav adds a second interception path
   alongside static-shell.md D8's `target="_top"` rule.
2. **`HeadOutlet`'s render mode.** If static, it only collects head content from static
   components — the 54 pages' `<PageTitle>` and 8 pages' `<HeadContent>` stop working. If
   `InteractiveServer(prerender: false)`, it renders nothing server-side and appears when the
   circuit connects — i.e. today's behavior exactly, which is why the **static `<title>` in
   `App.razor`'s head is what serves crawlers.** Confirm this composes (static title + interactive
   HeadOutlet) the same way it does under Stage 1.
3. **`HttpContext` in static SSR vs. the `AmbientUserContext` memo.** Confirm the memo still
   behaves for both worlds and that `SetScopedUser` (the background-job path) is unaffected.
4. ~~**MudBlazor provider placement.**~~ **ANSWERED BY STAGE 1 — and `IslandRoot` is dead.**
   The providers do **not** live in MainLayout. There is one `MudPopoverProvider` per circuit (it
   subscribes a section outlet by a fixed id, so a second throws *"already a subscriber"*), and
   roots initialise in **document order** — so a provider in MainLayout, the last root, is behind
   every island. They mount as `Components/MudProviders.razor`, ahead of anything that asks, with
   the theme as a parameter because the shell already resolved the mix from the request.

   **Stage 2 must reproduce this order in `App.razor`.** The rule is not "providers first" — it
   is **"providers ahead of every consumer"**. `HeadOutlet` legitimately precedes them; it asks
   for nothing. Shipped order in `_SiteLayout.cshtml`:

   | | root | why the position matters |
   |---|---|---|
   | 1 | `HeadOutlet` | none — asks for no provider |
   | 2 | **`MudProviders`** | must precede every island below |
   | 3 | `AppBarSearch` (island) | its autocomplete opens a popover |
   | 4 | `ShellImportPulse` (island) | no Mud, position irrelevant |
   | 5 | `App` → MainLayout | the app's own dialogs/popovers |

   **Order is guaranteed, and the guarantee is identical in both hosting models.** From
   `blazor.web.js` (net10) — and `blazor.server.js` uses the same mechanism:

   ```js
   case "server": return K(e, "server").sort((e, t) => e.sequence - t.sequence)
   ...
   const { descriptor: t, sequence: n } = e;
   if (void 0 === n) throw new Error("sequence must be defined when using a descriptor.");
   ```

   `sequence` is a **required** field on every server component marker, emitted by the server in
   render order, and the client sorts by it before creating roots. So root activation order is
   server render order is document order — by explicit sort, not by DOM-walk luck. Putting
   `<MudProviders @rendermode="…" />` above the shell in `App.razor` behaves exactly as
   `_SiteLayout.cshtml` does today. **The hoist carries over unchanged, and the reason it works
   is a framework contract rather than an observation.**
5. **Auth.** `[Authorize]`, the `DefaultAuthentication` cookie scheme, `ExternalAuthentication`,
   `ApiToken` Basic auth, and the DevAuth backdoor (`/Login/Dev`, `/Login/Dev/Bootstrap`) under
   the new endpoint mapping.
6. **Razor Pages coexistence** beyond the `/` conflict — `Error.cshtml`, `Privacy.cshtml`.
7. **Endpoint ordering** — Hangfire dashboard (`/hangfire`), Swagger, `MapControllers` (`api/*`,
   `dev/export/*`), `MapDefaultEndpoints` (Aspire). Confirm all survive.
8. **`ScoreTracker.Tests.E2E`** boots the real app via `WebApplicationFactory.UseKestrel`
   (`E2EAppFixture`) with WireMock + Testcontainers. Confirm the harness survives.
   `FrontDoorDispatcherTests` is the most exposed — it asserts the dispatcher's two worlds, and
   §4 may change how they're built.
9. **`DisconnectedCircuitRetentionPeriod = 1h`** — confirm it maps cleanly onto
   `AddInteractiveServerComponents`.

## 8. Status

**Scoping not started.** §1–§6 are established; §7 is the work. The Stage 1 spec's value came
entirely from auditing claims against shipped source rather than trusting a plan — this doc has
not had that pass, and §4 is what one hour of looking already turned up. Expect more.
