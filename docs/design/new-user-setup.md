# New-user setup — design

The step between signing in and having a home page: a dedicated `/Setup` card where a brand-new
account picks its username, language, country, visibility and mix. Workshopped with the owner
2026-08-02/03; visual mock approved.

**Status (2026-08-03).** Designed, not built. This doc is the build spec.

## Context

A new account today gets nothing. It lands on `/` — the home dashboard's empty state — with a
single **Create** button, and no part of the product ever mentions that it has a username or that
it is invisible to everyone.

That is a regression, not an oversight. The old `/Welcome` page was a post-signup card whose last
line read *"If you wish to change your username, check out your Account."* It was deleted on
2026-07-14 (`a9174544`) when the front door took the `/Welcome` route; the commit message says
plainly that "New accounts land on the dashboard, whose empty state is the onboarding now," and
[front-door.md](front-door.md) §Open questions parked the replacement: *"`/Welcome` fate — likely
folds into a first-login step of the home dashboard; owner to decide."* This is that decision,
resolved as its own page rather than a dashboard step.

Two specific defects fall out of having no setup step:

| | Where it comes from | What a new player gets |
|---|---|---|
| Username | OAuth `ClaimTypes.Name`, [LoginController.cs:80](../../ScoreTracker/ScoreTracker/Controllers/LoginController.cs) | Google and Facebook supply the account holder's **real name**. Discord supplies the Discord username. PIUGAME supplies the game tag — the only one that is right. A provider with no name claim yields the literal string `"Unknown Name"`. |
| Visibility | hardcoded `false`, [CreateUserHandler.cs:25](../../ScoreTracker/ScoreTracker.Identity/Application/CreateUserHandler.cs) | No World community, no leaderboards, no community rankings, no comparison with anyone — and no indication that a switch exists. |

So half the sign-in methods quietly stage a real name for eventual publication, and every new
account is socially inert by default. Both are one field away from being fine.

The wider bet: **front door → setup → build your home page** is a three-beat opening. Each beat
ends with the player having made a choice and seen the result. That sequence is what turns a
sign-in into a returning user.

## Goals

1. A new player never has to discover that a username or a visibility switch exists — both are
   put in front of them once, at the only moment they are thinking about their account.
2. The setup screen is a **step**, not a form: one card, five controls, a working default in every
   one of them, and a single forward button.
3. Nothing on the page can be lost. A reload, a dropped circuit, a back button, or a closed tab
   never costs the player a field they already filled in.
4. The player sees the site's own theme respond to a choice they made, before they leave the page.
5. It fires once, for new accounts, and never nags an existing player.

## Decisions

- **D1 — A page at `/Setup`, not a dialog on the dashboard.** The first shape considered was a
  dialog opening off the **Create** button. It was rejected: a dialog raised by a button labelled
  "Create" reads as an ambush, it shares a screen with a dashboard the player has no context for
  yet, and it makes account setup look like dashboard configuration. A dedicated route also gives
  the flow a nameable middle beat, which is the thing being sold.

- **D2 — Five fields, and nothing else.** Username, language, country, public, current mix.
  Everything here is either (a) wrong-by-default and invisible (username, public), or (b) cheap to
  ask once and expensive to discover later (language, country, mix). Score import was considered
  and cut: it is a whole flow, and the curated default dashboard already seeds an Import Scores
  widget on the board the player lands on in step 3.

- **D3 — The username carries its provenance.** The field ships prefilled with whatever the
  provider gave, under a chip reading *"filled in from Google"*, above helper text saying the name
  is what other players see. That chip is the entire mechanism: a prefilled field reads as already
  handled, a prefilled field labelled *this came from somewhere else* reads as needing a decision.
  PIUGAME signups see the same UI with a correct value already in it and simply move on.

- **D4 — Public ships off, framed by outcome, explained in one line.** Off is the current default
  and stays the default; pre-ticking a visibility consent is not on the table. The label is
  "Public profile" with a single adaptive sub-line — *"Off — only you can see your scores"* /
  *"On — your scores show on leaderboards and in communities"*. An earlier draft carried a live
  "how you'll appear" leaderboard-row preview; the owner cut it as over-explanation
  (2026-08-03) — public versus private is self-evident. The longer `Make Public Disclaimer 1/2`
  copy stays where it is, on `/Account`.

- **D5 — Mix defaults to Phoenix 2 and renders as the tier-list view switch.** The three primary
  mixes (`IsPrimary()`) only — `MudButtonGroup` with `OverrideStyles="false"`, the selected one
  `Variant.Filled` and the rest `Variant.Outlined`, `Color.Primary` at `Size.Small`, exactly as
  `ChartSkills.razor`'s `tier-views-group` does it. No per-mix descriptions: the names are proper
  nouns and a one-line gloss of what XX is would be noise to the audience that has one. The helper
  line says only what a new player cannot know — *"Switch it any time from the mix pill in the top
  bar."*

- **D6 — Every field saves on change. Continue means "I'm done", not "save".** This is the
  load-bearing decision. Each control writes through on commit — username and country and public
  via `UpdateUserCommand`, language and mix via `IUiSettingsAccessor.SetSetting` — exactly as
  `/Account` already behaves. Consequences: the page has no unsaved state to lose (Goal 3), a
  player who abandons halfway still keeps the fields they fixed, and the language reload in D8
  becomes free rather than destructive.

- **D7 — Every write is confirmed by a snackbar.** Auto-save that says nothing reads as a form
  that did not submit. `ISnackbar` at `Severity.Success`, MudBlazor's own timing. The green is
  `MixPalette.Success`, a const shared by every palette, so these read identically whatever mix is
  selected — which matters because the mix can change under them (D9).

- **D8 — Language applies on change, and is the one field with no snackbar.** A live circuit
  cannot change its own culture: `IStringLocalizer` resolves off `CultureInfo.CurrentUICulture`,
  the localization middleware sets that per *request*, and a circuit's copy is fixed at circuit
  start. So the language field navigates through `/Culture/Set?culture=…&redirectUrl=/Setup`, and
  D6 is what makes that safe. It gets no snackbar because the page returning in the new language
  *is* the confirmation — and a toast reading "language saved" in the language just left would be
  the wrong string in the wrong language at the wrong moment.

- **D9 — The mix re-themes the page live, with no reload.** `MixThemes.CssVariablesFor(mix)`
  *generates* the `--mix-*` block as a string; [App.razor:28](../../ScoreTracker/ScoreTracker/App.razor)
  merely emits it into the head. `/Setup` emits its own copy from page state —
  `<style>@((MarkupString)MixThemes.CssVariablesFor(_mix))</style>` — which appears later in the
  document, wins at equal specificity, and re-renders when `_mix` changes. Same single source, no
  duplicated CSS, one line.

  **The constraint this imposes:** it only covers `--mix-*`. `--mud-palette-*` is emitted by
  `MudThemeProvider`, which mounts as its own root at circuit start
  ([static-shell.md](static-shell.md)), and a page cannot re-parameterize a different root — so a
  MudBlazor component in this page's own markup would keep the *previous* mix's colours while
  everything around it flipped. **`/Setup` is therefore hand-styled against `--mix-*` only, like
  the front door.** Snackbars are unaffected: they render in the providers' root, and their green
  is mix-invariant by construction (D7).

- **D10 — Continue routes through `/Mix/Set`.** Mix selection lives in two places: the
  `Universal__CurrentMix` UiSetting (which `ShellModelFactory.ResolveMix` reads *first*) and the
  `CurrentMix` cookie (the anonymous fallback). A circuit can write the former and not the latter.
  So the page saves the UiSetting on click — correct immediately for a signed-in player, and
  `UiSettingSavedCacheEviction` already drops the shell cache — and **Continue** navigates to
  `/Mix/Set?mix=<chosen>&redirectUrl=/`, which writes the cookie and lands them home in one hop
  using an endpoint that already exists.

- **D11 — Fires once, for new accounts only.** `LoginController` sends new users to `/Setup`
  instead of `/`; a `Universal__SetupCompleted` UiSetting written on **Continue** stops it
  re-firing. No site-wide guard — a player who navigates away is not chased back. Existing
  accounts are explicitly out of scope (owner, 2026-08-03): they never see this page, whether or
  not they ever built a dashboard. A player who abandons setup without pressing Continue gets it
  again next sign-in, which is correct — they did not finish, and finishing is one click.

- **D12 — No skip link.** Every field lands with a working default, so Continue-with-nothing-
  touched already *is* the skip. A second control that does the same thing would only imply the
  page is a chore.

- **D13 — returnUrl is dropped for new accounts.** Already today's behaviour
  (`isNewUser ? "/" : returnUrl`), and it stays: a brand-new account has nowhere meaningful to
  resume to, and step 3 is the payoff the whole flow is built around.

- **D14 — Anonymous culture matching gets a downward mapping first (C0, landed).** Every entry in
  `SupportedCultures` is a *specific* tag and `RequestLocalizationMiddleware` only falls back
  **upward** (`es-CL` → `es` → invariant), so a visitor sending a bare `es`/`ja`/`fr` or a region
  we carry no catalogue for (`es-CL`, `es-PE`, `es-419`, `pt-PT`, `fr-CA`) resolved to English no
  matter what their browser asked for. Chile, Peru and Argentina are a large share of the
  playerbase and none of them could ever match. `SupportedCultures.ResolveClosest` supplies the
  missing downward half — exact tags win and are never re-regioned, otherwise the primary subtag
  picks the catalogue — and a `CustomRequestCultureProvider` appended **after** the three stock
  providers applies it only when they found nothing, so `?culture=`, the saved cookie and an exact
  header match all still win. **es → es-ES** (owner, 2026-08-03). Murloc is deliberately absent
  from the table: `en-ZW` is reachable only by asking for it exactly.

  It is pure string work — no `CultureInfo` is constructed — so a malformed, wildcard or unknown
  tag returns null and the default culture applies exactly as before. That is the owner's stated
  bar: no error page on a culture nobody tested a browser in. Pinned by
  `SupportedCulturesTests` (45 cases: the table, casing, unplaceable input, Murloc-is-never-a-
  fallback, and a guard that every produced code is itself supported) and by 11 `/Welcome` facts
  in `NonComponentEndpointTests` driving real malformed `Accept-Language` headers through the
  actual pipeline.

  This is independent of the rest of the design and shipped first — the language picker on
  `/Setup` should be a preference, not a workaround for a front door the visitor could not read.

## Technical scope

**Presentation only.** Every new line lands in `ScoreTracker.Web`. No vertical gains a type, no
port is added, no entity or migration is involved, and `Domain` / `Application` / `Data` are
untouched.

### Onion layers

| Layer | Change |
|---|---|
| `SharedKernel` | none |
| `Domain` | none — no new port, no new model |
| `Application` | none |
| `Data` | none — **no migration**, no schema change |
| Verticals | none — every contract this page needs already exists (see below) |
| `Web` | all of it: one new page, three redirect edits, resx keys, one theme touch-up |
| `CompositionRoot` | none — no new registration |

### What the page consumes, all pre-existing

| Need | Existing contract |
|---|---|
| Username, country, public | `Identity.Contracts.Commands.UpdateUserCommand(newName, newIsPublic, newCountry)` |
| Language, mix, the setup flag | `IUiSettingsAccessor.SetSetting` → `Identity.SaveUserUiSettingCommand` |
| Reading them back after the language reload | `Identity.GetUserUiSettingsQuery` |
| Country list | `IUserRepository.GetCountries()` — the same call `/Account`'s `ProfilePanel` makes |
| World community join/leave on the public toggle | `UserUpdatedEvent` → `Communities.CommunitySaga`, already wired |
| Claims refresh after a name/public change | `/Logout/Refresh` via `js/helpers.js`, the pattern `ProfilePanel.UpdateUser()` already uses |
| Culture switch | `CultureController` → `/Culture/Set` |
| Mix cookie | `MixController` → `/Mix/Set` |

Because the setup flag is a `UiSettings` row on an existing table, Identity's `UserOwned` purge
manifest already covers it — `AccountPurgeCoverageTests` needs nothing.

### Files

| File | Change |
|---|---|
| `Pages/Setup.razor` | **new.** `@page "/Setup"`, `@rendermode RenderModes.Interactive`, hand-styled against `--mix-*` (D9), page-scoped `<style>` block |
| `Controllers/LoginController.cs` | three redirects: OAuth `isNewUser` → `/Setup`; PIUGAME `resolution.IsNew` → `/Setup`; the PIUGAME tag-match merge's `returnUrl` → `/Setup`. Plus `DevLoginBootstrap`, so the dev path exercises the real flow |
| `Resources/App.*.resx` | new keys in **all nine** locales, inserted in alphabetical position (`ResxKeysAreStoredAlphabetically`), per-locale glossaries, Murloc from its own alphabet |
| `Services/Theming/MixThemes.cs` | one line: `SuccessContrastText`, see Open questions |

### Ratchets that will police this for free

`RenderModeDeclarationTests` (the page must declare its circuit), `UiColorTokenTests` (no colour
literals — the mix group takes its brand hues via `MixThemes.PaletteFor(mix).Primary` in an inline
style, the trick `FrontDoor.cshtml`'s `MixChipStyle` already uses),
`ResxKeysAreStoredAlphabetically` and `LocalizationKeyTests` (the new keys, including the
case-collision rule).

### Tests

- **`ScoreTracker.Tests.Components`** (bUnit — the lowest rung that catches these, per the owner's
  granularity directive): defaults on arrival; each field dispatching its command on change and
  not on render; the empty-username guard; the public toggle's two states; the mix group's
  selected state; Continue writing the flag and navigating to `/Mix/Set`.
- **`ScoreTracker.Tests.E2E`**: `HomeDashboardTests.CreatesTheCuratedDefaultAndDragReordersPersistently`
  clicks **Create** immediately after sign-in and will now meet `/Setup` first — its seed needs the
  flag pre-set, or the flow needs the extra step. `PiuGameLoginTests` asserts the post-signup
  landing page and moves to `/Setup`. One new fact worth having: a new account lands on `/Setup`,
  Continue lands it on the dashboard.

## Build plan

Checkpoint commits, suites green at each. FT = owner field-test checkpoint.

| # | Commit | Notes |
|---|---|---|
| C0 | **Anonymous culture fallback** ✅ **landed** | `SupportedCultures.ResolveClosest` + `CustomRequestCultureProvider` appended after the stock three (D14); 45 unit cases, 11 `/Welcome` E2E facts. Independent of everything below and shippable on its own |
| C1 | The page, static | `Setup.razor` with all five controls, hand-styled, defaults wired, no persistence yet. Page-scoped `<style>` + live re-theme (D9) |
| C2 | Save-on-change + snackbars | `UpdateUserCommand` / `SetSetting` per field, claims refresh, `ISnackbar` confirmations (D6/D7); `SuccessContrastText` |
| C3 | Language + mix navigation | `/Culture/Set` round-trip preserving state (D8), Continue through `/Mix/Set` (D10) |
| C4 | Entry and exit | `LoginController` redirects ×4, the `Universal__SetupCompleted` flag, once-only behaviour (D11) — **FT1: sign up on a fresh account and walk the three beats** |
| C5 | Localization | new keys ×9 locales, alphabetical insertion, glossaries, Murloc |
| C6 | Tests | bUnit suite; E2E seed/flow updates + the new landing fact |
| C7 | Docs | this doc synced, ARCHITECTURE.md pages table + login-flow paragraph, [front-door.md](front-door.md)'s `/Welcome` fate question closed |

## Open questions

- **`Severity.Success` contrast.** `MixThemes.Build()` sets `Success = MixPalette.Success`
  (`#6EDE7F`) but never `SuccessContrastText`, so the snackbar label takes MudBlazor's default.
  Worth eyeballing before success toasts become first-run furniture — this is the first screen
  where they carry real weight. One line in `Build()` if it reads badly.
- **Typography under a long locale — mostly already answered.** The front door is fully localized
  (88 `L[…]` calls) and `UseRequestLocalization` runs the default provider chain, which ends in
  `AcceptLanguageHeaderRequestCultureProvider` — so a browser sending an exactly-supported locale
  already gets `/Welcome` in its own language, cookie or no cookie. Its `<h1>` is the same
  uppercase display face at a *larger* clamp than this page's, shipping to all nine locales today,
  which retires the headline risk. What is genuinely new here is the three-label step rail; it
  wraps by design, so this is a C5 eyeball, not a design constraint.

- **`en-ZW` is reachable by accident.** Murloc is in `SupportedCultures`, so a browser sending
  `en-ZW` has always matched it exactly and rendered the joke locale — that predates C0 and C0
  deliberately did not change it (only exact requests reach Murloc; nothing falls back into it).
  Zimbabwean traffic is presumably nil and the outcome is funny rather than harmful, so this is
  the owner's call, not a defect to fix unasked.
