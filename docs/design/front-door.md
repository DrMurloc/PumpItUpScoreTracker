# The front door — design

The logged-out experience: one page replacing the bare `/Login` card, doubling as the landing
page for newcomers. Decided in the 2026-07-12 front-door workshop (visual mock iterated with
the owner through seven rounds; final state reflected here).

**Status (2026-07-13).** The front-door page (hero, sign-in card, showcase, stewardship, browse)
is built and live at `/`+`/Login` (build-plan C1–C6). The scope then merged with the home-page
go-live: WhatShouldIPlay is deleted, the home dashboard is `/` for signed-in visitors, the
per-page widget cap is 10, and "Create" seeds the curated default (see
[HomePageWidgets/README.md](HomePageWidgets/README.md) §3). The **server-side `/` dispatcher** (this
doc's D2 context 3) is implemented on the `FrontDoor` Razor Page and verified by
`FrontDoorDispatcherTests` (E2E). Still pending: the sign-in dialog (D2 context 2), returnUrl
through the login flow (D4), and the SEO head/meta/OG/`robots.txt`/sitemap pass.

**Context.** Today a logged-out visitor hitting `/` is hard-bounced to `/Login` — a bare card
with three provider buttons and an "Account Creation?" dialog, zero explanation of what the
site is. Meanwhile the logged-out shell already exposes the public surfaces (tier lists,
charts, titles, rankings, calculators), and the tier lists alone carry 28% of traffic. With
the Phoenix 2 wave incoming, the front door is the highest-leverage unshipped page on the
site. The owner's customizable home page (PR #142) owns logged-in `/`; this design owns the
logged-out path.

## Goals

1. A newcomer understands what the site is, and a returning player can sign in, **both
   without scrolling** (390×844 and desktop).
2. The pitch is the site's own live data — real counts, real boards — never marketing
   adjectives.
3. No link on the page lands on a login wall.
4. Sign-in is equally frictionless everywhere else on the site, without dragging users
   through the landing content.
5. The community-stewardship values are stated: private by default, nothing resets between
   mixes, export anytime, public API.

## Decisions

- **D1 — One door, not two.** Logged-out `/` renders the front door; `/Login` is the same
  page. No separate marketing page: it would add a click for newcomers, serve nobody twice,
  and go stale. Auth-required bounces land here with the sign-in card in view.
- **D2 — The sign-in card is one shared component with three contexts.**
  1. *Front-door hero* — the right column (first scroll position on phones).
  2. *In-place dialog* — the app-bar "Sign in" on any public page opens the card over the
     current page (bottom sheet at phone widths, per UX rule 10). No navigation; after auth
     you're back where you were — that's expected behavior and is deliberately **not**
     announced in the UI. The dialog's fineprint gains a "Take the tour →" link to the front
     door for the curious.
  3. *Gated bounce* — pages that require login redirect to the front door with the card in
     view plus a returnUrl (you can't overlay a page the visitor isn't allowed to see).
- **D3 — Flat provider trio.** Continue with Discord / Google / PIUGAME — equal visual
  weight, that order. Discord and Google are the preferred route; PIUGAME exists for players
  who don't want SSO and sits last with no hero treatment. Its trust line ("credentials go
  directly to piugame.com — never stored or logged") renders as fineprint under the stack.
  The "Account Creation?" dialog is retired: one sentence above the buttons ("Signing in
  creates your account automatically — no forms, no site password") plus fineprint that
  teaches the many-to-one link model ([login-overhaul.md](login-overhaul.md)).
- **D4 — returnUrl, finally.** The eight hard-gated pages today call a bare
  `NavigateTo("/Login")` and the destination is lost (post-auth everyone lands on `/Charts`
  or `/Welcome`). This overhaul threads a returnUrl through the OAuth state / PIUGAME login
  flow so deep-link bounces resume where they were headed.
- **D5 — Link policy.** Every link on the page lands on a public surface (`/TierLists`,
  `/WeeklyCharts`, `/Titles`, `/PlayerRankings`, calculators, `/Privacy`, Swagger), a
  sign-in entry (`/Login/{Provider}`, `/PiuGameLogin`, the card anchor), or an in-page
  anchor. Never a login wall. Feature pitches whose destination is gated (score import)
  point their CTA at the sign-in card instead.
- **D6 — The data is the pitch.** All numbers live, from the owning verticals:
  - Stat row: total scores tracked (Phoenix + legacy models, 1M+), players, countries
    represented, communities (`IsRegional = 0` only).
  - Activity pulse: scores recorded per day over the last 30 days from the ScoreEventJournal
    (`Source <> 'backfill'`). **Bars clip at ~p90 of the window** — feature-drop spikes
    (33.7k on Recap day vs a ~1–2k baseline) render as maxed bars instead of flattening the
    chart; the exact 30-day total is printed beside it.
  - Showcase (2×2): data-backed tier lists (snapshot via existing tier-list queries; the
    "backed by real pass/score data" framing is the point), this week's board with rotation
    countdown and **anonymous** per-chart leaders (D9), one-click import ending in a
    session-highlights line, and the Pumbility/titles climb card.
  - Mix timeline: all 31 registered mixes as chips, 1st Dance Floor (1999) → Phoenix 2,
    rendered from the Mix table in SortOrder; the three themed mixes wear their own brand
    hues. Decorative, not links (revisit later).
- **D7 — Anonymous-page cost is ~zero.** Every landing query is cached in-process with a
  daily-ish TTL. The page must not add per-hit DB load (see the 2026-07-10 DB incident).
- **D8 — Sequencing.** The "More than a tracker" section presents the widget home page and
  the randomizer's tournament mode + live view sharing as **live features, present tense**
  (owner call: no "coming soon" at rollout). Therefore this page ships after: (a) the home
  dashboard is publicly listed (shell merged 2026-07-12, currently unlisted pre-release),
  and (b) randomizer tournament management + live share land. The tournaments/qualifiers
  tile wears an "expanding soon" tag by design (qualifiers pages and March of Murlocs are
  both growing).
- **D9 — Anonymous leaders.** The weekly-board card shows scores, not player names.
- **D10 — Stewardship section ("New mix. Same you.").** Three claims plus the mix timeline:
  private by default (scores feed the aggregate analysis behind the tier lists but are never
  shown publicly without opt-in), your data is yours (full score export, any time), built
  for toolmakers (public API + Swagger). The mix-transition claim is framed positively —
  "when a new mix arrives, nothing here resets" — deliberately without referencing anyone
  else's resets.

## Page anatomy

Top bar (existing logged-out shell; "Sign in" where the avatar goes) → hero (eyebrow /
headline / pitch line; sign-in card right; stat row + 30-day pulse under the copy) →
"See where you stand" showcase 2×2 → "More than a tracker" 2×2 (home widgets · run real
events · tournaments & qualifiers · Discord wired in) → "New mix. Same you." stewardship
(mix timeline band + three tiles) → "No account needed to look around" browse row → footer
(free/ad-free line, Privacy, API link, locale list).

On phones the hero stacks copy → card → stats, keeping both goals of G1 inside the first
viewport-and-a-bit; primary actions stay thumb-reachable.

## Technical shape

Presentation-first. No schema changes, no migrations, no new tables, no bus messages, no
scheduled jobs, no domain logic.

- **Web**: the front-door Razor page (absorbing `Login.razor`), a shared `SignInCard`
  component (one concept, one component), the MainLayout dialog hookup, logged-out routing
  for `/`, returnUrl in the login controller flow, localization keys in all nine locales.
- **Read-only stat queries** (the only non-Web code): thin count/aggregate passthroughs in
  their owning verticals, following the existing query/handler/port pattern — ScoreLedger
  (score totals + 30-day journal buckets), user + country counts (Identity-owned `User`
  table), Communities non-regional count (check whether the existing public-communities
  query already serves this before adding one). Weekly board and tier-list cards reuse
  existing contracts; the mix list is `MixEnum`/Mix-table data that already loads.
- **Brand tokens**: provider colors (Discord, Google, PIUGAME) become a mix-invariant token
  group emitted by `MixThemes` (the `--skillcat-*` precedent), so the page ships with zero
  color literals and `Login.razor`'s `UiColorTokenTests` allowlist entries are deleted.
- **Caching**: `IMemoryCache` in the new handlers (permitted in Application and verticals),
  daily TTL per D7.

## SEO (owner-requested, first-class)

The site renders with `render-mode="Server"` — no prerendering, so crawlers and
link-unfurlers receive an empty shell. That suppresses indexing and, more immediately for
this community, **breaks Discord link embeds** (Discord's crawler runs no JS). An app-wide
`ServerPrerendered` flip is **off the table** — it was tried before and broke everything.

**The front door is therefore a real (non-Blazor) Razor Page.** ASP.NET already routes MVC
and Razor Pages ahead of the Blazor fallback in this app; the page renders complete HTML
server-side with zero JS and no SignalR circuit:

- `IMediator` injection works from a Razor Page exactly as it does in the controllers
  (dispatch-via-mediator rule unchanged); all stat queries are cache-backed (D7).
- Theming: the page calls `MixThemes.CssVariablesFor(...)` — the same single source that
  feeds the Blazor shell — so the tokens can't drift.
- Localization: `IStringLocalizer<App>` (`L[…]`) works in cshtml; same resx keys.
- Full head control natively: meta description, OpenGraph/Twitter tags, JSON-LD
  (`WebSite` + `Organization`), canonical URL.
- Crawl surface: add `robots.txt` (allow, sitemap pointer); add the front door and missing
  public pages (`/Titles`, `/WeeklyCharts`, `/PlayerRankings`) to the existing sitemap;
  site-wide generic OG defaults go in `_Layout` (already server-rendered even in Server
  mode) so every shared app link unfurls with at least the site card.
- Pattern for later, out of scope here: `_Host.cshtml` can compute route-specific OG tags
  server-side (e.g. chart name for `/Chart/{id}`) without prerendering — the same
  static-head trick applied to Blazor-served routes.
- **Known limitation, accepted**: culture is cookie-based → one URL per page → no hreflang;
  search engines index the default culture only. Per-locale URLs are future work.

**The `/` dispatcher** falls out naturally: a Razor Page owns the root route (relieving
`_Host.cshtml`, which keeps only the fallback); its handler renders the front-door partial
for anonymous visitors and the Blazor host markup (the same `<component
type="typeof(App)">` tag `_Host` uses) for authenticated ones — server-side, no redirect,
no flash. The WSIP→dashboard cutover later changes nothing about the dispatcher.

**Accepted duplication**: the sign-in card exists twice — static markup in the front-door
partial, and the Blazor `SignInCard` used by the in-app dialog. It's ~40 lines; each copy
carries a comment pointing at its twin.

## Build plan

Checkpoint commits, suites green at each. FT = owner field-test checkpoint.

| # | Commit | Notes |
|---|---|---|
| C1 | ScoreLedger stats query | totals (Phoenix + legacy) + 30-day journal day-buckets, `IMemoryCache` daily TTL, component tests |
| C2 | Player/country + community counts | user table count query; Communities count (reuse existing public-communities query if it serves); cached + tests |
| C3 | Brand tokens | `--brand-discord/-google/-piugame` emitted by `MixThemes.CssVariablesFor` (mix-invariant, `--skillcat-*` precedent); `UiColorTokenTests` expectations |
| C4 | Front-door Razor Page shell | `_FrontDoor.cshtml` partial + `/Login` route (real Razor Page, zero JS, full head); hero (copy, card, stats, p90-clipped pulse); DevAuth-populate and already-logged-in redirects preserved server-side; new keys ×9 locales |
| C5 | Showcase 2×2 live | tier-list snapshot + weekly board (countdown, anonymous leaders) on real cached queries; import/climb cards static-visual |
| C6 | Lower page | more-than-a-tracker 2×2, stewardship (mix timeline + three tiles), browse row, footer — **FT1: page complete at /Login** |
| C7 | `/` dispatcher | root Razor Page: anonymous → front-door partial, authenticated → Blazor host markup; `_Host` drops its root route, keeps fallback; culture-cookie behavior preserved |
| C8 | Retire `Login.razor` | Blazor login page deleted; its `UiColorTokenTests` allowlist entries burned; "Account Creation?" strings retired into card microcopy |
| C9 | Sign-in dialog | Blazor `SignInCard` component + app-bar hookup: opens in place on public pages, bottom sheet at phone widths (static twin documented) |
| C10 | returnUrl | all eight gated-page redirects, OAuth state + PIUGAME flow carry-through, post-auth resume — **FT2: flows** |
| C11 | Head + crawl surface | front-door meta/OG/JSON-LD/canonical, site-wide OG defaults in `_Layout`, `robots.txt`, sitemap additions — **FT3: view-source is real HTML; Discord unfurl works** |
| C12 | E2E | update PIUGAME-login selectors; new facts: anonymous `/` serves the front door, authenticated `/` serves the app, dialog sign-in path |
| C13 | Docs | ARCHITECTURE.md (pages table + login-flow paragraph + front-door link), UX-GUIDELINES if the dialog pattern earns a rule, this doc synced |

## Open questions

- **`/Welcome` fate** — the post-first-login page is the same era as the old `/Login`.
  Likely folds into a first-login step of the home dashboard rather than this work; owner to
  decide.
- **Logged-out theme at Phoenix 2 launch** — anonymous visitors currently resolve to the
  Phoenix palette; flipping the logged-out default to Phoenix 2 when the game releases is a
  one-line change in the resolution default. Decide at launch.
