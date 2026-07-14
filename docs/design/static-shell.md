# The static shell — design (wave 2 of the SEO/islands plan)

Decided in the 2026-07-14 chart-details workshop (owner + Claude). This is **wave 2** of a
three-wave plan, pulled forward to run **first** because the chart-page overhaul (wave 1,
separate branch off this one) builds on top of it:

- **Wave 2 (this doc)**: the site shell (top nav, bottom nav, More sheet, mix pill, theme)
  becomes server-rendered HTML on every page. Blazor pages boot into the content region
  under it. Anonymous responses become CDN-cacheable.
- **Wave 1 (next, branches off this)**: `/Chart/{id}` becomes a Razor Page with Blazor
  islands — real HTML for crawlers, vanity URLs, verdict sentences, similarity shelf
  (see the chart-details design doc when it lands).
- **Wave 3+**: other pages migrate to Razor+islands page-by-page where SEO/speed justify it.

**Context.** Today `_Host.cshtml` serves an empty shell (`render-mode="Server"`, no
prerendering anywhere — confirmed by curling production: 7.9KB, no `<title>`, no nav, no
content). Crawlers and Discord unfurlers get nothing; humans stare at a blank page until
the circuit renders. The front door ([front-door.md](front-door.md)) already proved the
pattern: a real Razor page with a static topbar, `MixThemes.CssVariablesFor(...)` for
theming, `L[…]` in cshtml, `IMediator` from a Razor Page. This doc generalizes that shell
to the whole site.

**Why the shell is prerenderable (audit of MainLayout, 2026-07-14).** The shell's
dynamic surface is almost entirely per-request-resolvable data, not live state:

| Shell element | Varies by | Server-resolvable? |
|---|---|---|
| Nav item visibility | `IsLoggedIn`, `LegacyMixGate`, current mix | Yes (claims + UiSettings) |
| Theme tokens + Mud palette | mix + `/Account` override | Yes (same `MixThemes` calls) |
| Avatar, gamer tag, user links | UiSettings + claims | Yes |
| Highlighted tournaments in Compete menu | DB (`GetAllTournamentsQuery`) | Yes (cache in-process, daily-ish TTL) |
| "My Recap" link | `GetPlayerRecapQuery` | Yes (cache per user) |
| Import-status pulse dot | live bus events via `IUiNotificationHub` | **No — the one live pixel.** Becomes a tiny island. |
| App-bar chart search (`ChartSelector`) | interactive autocomplete | **No — circuit-bound.** Island where a circuit exists. |
| `Rating` live subscription | — | **Dead code**: subscribed, never rendered. Delete. |
| B1G ONE popup | — | **Dead code**: cutoff passed 2024-10-15. Delete. |

## Goals

1. Every page paints a real shell (nav, theme, wordmark) in the initial HTML — before any
   circuit exists. Blazor pages keep working exactly as today inside it.
2. Anonymous page responses carry cache headers and no `Set-Cookie`, so a CDN can serve
   them from the edge (bypass-on-auth-cookie rule).
3. One source of truth for the shell — the front door's hand-rolled topbar unifies onto it.
4. The shell contract is stable enough for wave 1 to build against while this branch is
   still in flight.

## Decisions

- **D1 — Cutover, not duplication.** `MainLayout` stops rendering chrome entirely; the
  static shell partial is the only nav implementation. A "static skeleton that Blazor
  replaces on connect" was rejected: duplicated markup drifts, and the replace flashes.
- **D2 — Rendered per request, split by auth.** Anonymous requests render the anon shell
  (default mix, sign-in affordance) and are cacheable. Authenticated requests bypass the
  CDN (cookie rule) and render the personalized shell at origin. No client-side identity
  hydration, no flash-of-wrong-shell: whoever rendered the page knew who you were.
- **D3 — Reuse the existing CSS vocabulary.** `site.css` already defines
  `.top-nav-btn`, `.mega-panel`, `.mix-pill`, `.bottom-nav`, `.bn`, `.more-sheet`,
  `.nav-avatar-wrap`, `.nav-import-pulse`, the wordmark, etc. The static shell emits the
  same classes as plain HTML — visual parity by construction, not by re-styling.
- **D4 — Menus go vanilla.** Desktop mega-menus, the avatar menu, the mix menu, and the
  mobile More sheet become CSS + a small `nav.js` (click-to-toggle, outside-click close,
  Escape, one-open-at-a-time). No MudBlazor outside circuits. `MudHidden` breakpoints
  become CSS media queries (same cutoffs).
- **D5 — Mix pill = endpoint + reload.** Mix switching already forces a full page reload
  (established behavior), so the static pill is a menu of links to a small endpoint that
  persists the selection (UiSettings for signed-in; cookie for anonymous — verify the
  accessor's anon fallback and align) and redirects back to the referrer.
- **D6 — Circuit-bound shell bits become the shell-live island.** One small root component
  (import pulse overlay; anything live added later) mounted into a fixed slot in the
  static shell on pages that have a circuit. The app-bar `ChartSelector` becomes an island
  on Blazor pages (which already have a circuit); on circuit-less Razor pages it renders
  as a plain link to `/Charts` until wave 3 gives those pages islands of their own.
  Delete the dead `Rating` subscription and the expired B1G ONE popup while gutting.
- **D7 — PageDock unchanged.** Dock content stays Blazor (rendered by pages);
  `page-dock.js` is already vanilla and keeps owning scroll behavior. The static bottom
  nav carries the same markup/classes; the `has-dock` / `focus-mode` layout classes get
  toggled by a small JS bridge instead of Blazor re-render.
- **D8 — Bottom-nav active state**: rendered server-side from the request path; a few
  lines in `nav.js` re-evaluate it on Blazor SPA navigations (URL change listener) so
  in-app navigation keeps the highlight honest without a circuit round-trip.
- **D9 — Culture cookie: set only when missing.** `_Host.cshtml` (and the FrontDoor
  mirror) currently append the culture cookie on **every** response — an unconditional
  `Set-Cookie` makes every page uncacheable at the CDN. Emit it only when absent or stale.
- **D10 — Cache policy.** Anonymous responses: `Cache-Control` + origin output caching
  keyed on (path, culture, anon-mix); TTL aligned with the shell's cached queries. CDN
  rule (owner infra): cache only when no auth cookie is present. Authenticated: no store.
- **D11 — Blazor-side layout keeps its jobs.** The gutted `MainLayout` retains: the Mud
  providers (`MudThemeProvider/Popover/Dialog/Snackbar`), `ChartVideoDisplay`, the
  Phoenix Recap one-time popup, the legacy-mix body-hold + `RedirectIfGated` logic, and
  the PageDock slot. Providers must stay inside the Blazor root; they render no chrome.
- **D12 — LegacyMixGate applies server-side** to nav filtering (same helper, same rules);
  the *page*-level gating stays in the Blazor layout (D11).

## The shell contract (what wave 1 codes against)

- `Pages/Shared/_SiteLayout.cshtml` — the one full-page layout: `<head>` (charset,
  viewport, CSS links, theme `<style>` from `MixThemes.CssVariablesFor`, a `Head` render
  section for page-owned title/meta/OG/JSON-LD), the shell header/bottom-nav markup,
  `@RenderBody()` as the content region, and script includes gated by a layout flag
  (`IncludeBlazorScripts`) so circuit-less pages ship zero framework JS.
- `ShellViewModel` built by a per-request `ShellModelFactory` (Web service): current user
  (claims), display name + gamer tag + avatar (UiSettings), current mix + theme mix,
  gated-mix flag, highlighted tournaments (cached), has-recap (cached), active-nav hint.
- `_Host.cshtml` adopts `_SiteLayout` (shell + Blazor component region +
  `IncludeBlazorScripts = true`). The front door migrates its hand-rolled topbar onto the
  same partial (its lower-page content is unchanged).
- Razor pages (wave 1+) use `_SiteLayout` directly, own their `Head` section, and mount
  islands via `<component type="..." render-mode="Server" />` where needed.

## Build plan

Checkpoint commits, suites green at each. FT = owner field-test.

| # | Commit | Notes |
|---|---|---|
| C1 | `_SiteLayout` + `_Shell` partial + `ShellModelFactory` + `nav.js` | Front door becomes the first consumer (its topbar unifies onto the shell). New keys ×9 locales if any new strings appear (aim for zero — reuse existing nav keys) |
| C2 | `_Host` adoption + `MainLayout` gutting | Blazor pages render under the static shell; providers/dock/gate logic retained; content top-offset CSS replaces `MudAppBar` spacing; delete dead Rating sub + B1G ONE popup — **FT1: every page, 3 breakpoints, logged in + out, XX/Phoenix/gated mix** |
| C3 | Mix endpoint + shell-live island + selector island | pill switches mix via endpoint; import pulse works on Blazor pages; app-bar search island on Blazor pages, `/Charts` link elsewhere |
| C4 | Culture cookie fix + cache headers + output caching | verify `curl -I` anon: no `Set-Cookie`, real `Cache-Control`; authenticated: `no-store` — **FT2: CDN dry-run** |
| C5 | Tests + docs | E2E nav selector updates; new E2E facts (anon page serves shell HTML pre-circuit); ARCHITECTURE.md shell paragraph, UX-GUIDELINES shell note, CLAUDE.md if conventions shift; this doc synced |

## Risks / watch-list

- **Visual parity** is the main risk surface: fixed-header content offset (MudAppBar used
  to own it), menu focus/hover behavior, More-sheet height/scroll, safe-area insets on
  phones. Mitigated by D3 (same classes) and FT1's breakpoint sweep.
- **Two root components, one circuit**: the shell-live island and the app root share the
  circuit's scoped DI (that's how PageDock state and `IUiNotificationHub` subscriptions
  keep working). Verify disposal on circuit teardown (the MainLayout NRE comment).
- **Output caching vs cookies**: any stray `Set-Cookie` (culture, auth refresh, Hangfire?)
  silently kills cacheability — C4 must audit response headers, not assume.
- **E2E selectors** lean on MudBlazor DOM for nav interactions today; C5 rewrites them
  against the static markup (stable classes, add `data-testid` where helpful).

## Out of scope

- The chart page itself (wave 1 — separate doc/branch built on this one).
- Migrating any other Blazor page to Razor+islands (wave 3+).
- CDN provisioning/config (owner infra; D10 defines what the app must emit).
- Sitemap/robots/OG work beyond what the front door already did (wave 1 carries the
  chart-page SEO surface).
