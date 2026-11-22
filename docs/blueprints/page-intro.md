# Page intro - blueprint

> A blueprint is a feature proven in Astrid, described for building here. It is not a spec: on the branch that
> builds the feature, its draft requirements become the feature's spec (docs/SPECS.md), and that PR deletes this file.

## What it is, for PIU

Some PIU pages assume a player already knows an idea that most new players have never met: a top-50 pool, what
one chart is worth, PUMBILITY peers, a tier list's bands. Today the page either explains itself in a sentence
above its answer or leaves the player to work it out.

The page intro gives that explanation once, the first time a player opens the page, and then gets out of the way:

1. **The intro card.** Once the page has drawn its answer, a card comes up over it, dimming the page. It names
   the page and says in one line what it is for. It lists what the tour will show, and offers **Skip** or
   **Show me around**. Where several pages share one intro, such as the four PUMBILITY pages, **Next** first
   fades the card into a short *How it works*: the whole process in a few numbered steps.
2. **The tour.** Show me around walks the page itself, one stop at a time. Each stop spotlights one part of the
   page and shows a callout with a counter (2 of 5), a title and a few lines, and Back, Next or Done, and Skip.
3. **Tips.** A tour finished with Done leaves a small pulsing dot on each stop. Pointing at a dot, or tapping
   it, shows that stop's words again, until the player presses **Hide these tips** or leaves the page.

Whichever way it ends, the first visit is over for good. **Replay** on the Account page offers it again, one row
per intro. The intro stands between the player and the page only until they press a button, and it never comes
up over a loading page.

## How it works in Astrid

**The parts.** These live in Astrid's Web host, under `Components/Onboarding/`:
- **`Tour`, `TourStep`**: one tour as data. Its route, its stops in order (each an anchor word, a title and
  words) and the one setting its end marks completed. Every tour's copy is a list of strings in code, so it is
  unit-tested like any other string.
- **`TourState`**: the tour on screen, scoped to one circuit. It knows the pending tour, the active one, the stop
  index, the tips left behind, and a "placed" signal the intro card waits on.
- **`TourHost`**: draws the dim, the spotlight, the callout and the tip dots. It sits once in the shared layout.
- **The intro card per page**, for example `<Area>Intro.razor(.cs)`. It is a plain overlay, deliberately
  not a MudDialog: MudDialog's dim left at once while the tour's dim was still fading in, so the page flashed
  lit between the two. The card raises one pick, Skip or Show me around, and the page decides what follows.
- **`PageFade` / `PageFadeOverlay`**: a cover over the main content that carries a person from one first-visit
  screen to the next, and from a card into a tour on another page of the same layout.
- **`wwwroot/js/tour.js`**: everything geometric. It finds the stop's `[data-tour~="word"]` elements, scrolls
  them into view, draws one spotlight around them, and places the callout under, above, beside or pinned to the
  window's foot. It keeps keyboard focus in the callout, lets wheel and touch scrolling through the dim, and
  waits 5 seconds for an anchor that has not shown.
- **The pure card rules**, in the Web application project's `Findings/Intro/`. Which tour to pick and the
  card's words are worked out from facts handed in, and tested without a browser.

**The data.** Each intro has one per-person UI setting, `<Area>__IntroCompleted`, which reads as completed when
it holds `true` (case and spaces ignored). It goes through Astrid's per-person settings port (`IUserUiSettings`:
get, set, clear) and is listed in `Theming/UiSettingKeys.cs`. The Account page's settings component lists one
Replay row per intro. Replay clears that one key and opens the page, and opens it anyway when the clear fails.

**The flow.** The page draws its answer, then reads the key. When the key is unset, the card comes up. Skip or
Escape writes the key. Show me around asks `TourState` for the tour. The card keeps its dim until the first stop
is placed, or for at most 3 seconds, so the page is never seen lit between the two dims. Done, Skip, Escape or
leaving the page writes the key. A reload or a script failure ends the tour without writing it, so the card
comes back next time. Done leaves tips. A tour never presses, saves or sends anything on the page.

**The doctrine.** Astrid's UI rules hold the pattern in plain words:
- **The first visit:** the pattern above, with a one-audience variant - an intro card over the page itself, then
  one tour - which is the one PIU needs.
- **Onboarding:** anchors are words in `data-tour`, never CSS classes; stop copy is data; an intro two pages share
  lives in the layout they share.
- **Two rules about words on a page:** no explanatory prose on a working surface, and introduce a concept in the
  intro and never redefine it in a tooltip.

**Tests in Astrid:**
- Unit tests over each tour's data and `TourState`.
- bUnit over each card and the host.
- One Playwright fixture per first visit, for what only a browser shows: the dims handing over with no lit
  frame, the spotlight landing on each anchor, and focus staying in the callout.

The anchor contract, the anchor map and its ratchets are written up for PIU in **page-anchors.md**. This
blueprint builds on them and does not restate them.

## How it lands in PIU

**Placement: the Web host only. No vertical, no MediatR message, no port, no table.**
- **Where the files go.** The components sit in `ScoreTracker.Web` under `Components/Onboarding/`:
  - `PageIntro.razor`: the card, the tour and the tips for one page;
  - `Tour.cs`: the model;
  - `PageIntros.cs`: the registry of every intro, its key, its route and its Replay label.
- **Why no vertical.** The feature is presentation state about the player's own screen, which CLAUDE.md's layer
  rules give to Web, and it has no domain rule.
- **How state is read and written.** Through `IUiSettingsAccessor`, the sanctioned path, which already sends
  Identity's existing get, save and clear commands for a signed-in player.
- **Storage.** The settings land in `scores.UserSettings`, which Identity owns and account deletion already
  purges. No migration and no purge-manifest change.

**No shared layout to lean on.** This is the largest difference from Astrid. In PIU every navigation is a full
document load (enhanced navigation is off), and each page declares its own circuit. So:
- There is no `TourHost` in a layout and no fade between pages: a tour can only run on the page it started on.
- `PageIntro` is placed by the page itself, inside its own markup. A page passes it the intro and a `Ready` flag
  that turns true once its answer has drawn.
- Several pages that share an intro place it in the component they share. For PUMBILITY that is
  `PumbilitySectionFrame`, which already carries the number, the bar and the tabs for all four pages.
- A page that has gone static SSR under the SSR ladder places the same component as an island. Its `Ready` is
  its first render, since its content is already in the markup.

**The card is never in the served HTML.** `PageIntro` decides only in `OnAfterRenderAsync(firstRender)`, for two
reasons. An anonymous visitor's settings are browser storage, which needs JS interop and fails during prerender.
And deciding after render keeps the card out of what crawlers and the no-script reader see.

**State: UiSettings keys.**
- **The key.** One key per intro, `<Area>__IntroCompleted`, holding `true`. This follows the `<Area>__<Name>`
  shape of `Density__<Page>` and `Pumbility__Pool`.
- **Shared intros share a key.** The PUMBILITY section uses one key, `Pumbility__IntroCompleted`, for all four
  pages, so whichever the player opens first ends the first visit for the section.
- **Reading the key.** Read it as completed when it is `true`, ignoring case and surrounding spaces. A key that
  cannot be read shows no card, because the first visit must never stand between a player and the page.
- **Signed-in players** keep the key on their account, so it follows them across devices.
- **Anonymous visitors** go through the same accessor, which falls back to `ProtectedLocalStorage`. Their first
  visit is remembered in that browser only. Storage that cannot be decrypted, for example after a key
  rotation, reads as unreadable and shows no card.
- **Signing in** reads the account's key, not the browser's.
- **Never a "new account" test.** `Universal__SetupCompleted` warns against reading an absent key as "this
  account is new", and the intro key never asks that question. An absent key only means "this player has not
  had this intro", which is equally true of a veteran on the day an intro ships.

**Copy: every word through `L[...]`, in all nine locales, in DOMAIN.md's words.**
- **What gets translated.** The card's lead line, the How it works steps, each stop's title and words, Skip,
  Show me around, Back, Next, Done, the counter `{0} of {1}`, Tip, Hide these tips and each Replay row.
- **Where the copy lives.** A tour's stops hold their English text in code, and PIU's English text is its
  resource key, so that text is the key.
- **How it is rendered.** `PageIntro` renders `L[step.Title]` and `L[step.Words]`, so the copy stays data and is
  testable, as Astrid's is.
- **Adding keys.** New keys land in every resx in alphabetical position, under `ResxKeysAreStoredAlphabetically`
  and `LocalizationKeyTests`, Murloc included. They follow each `LOCALIZATION-<locale>.md` glossary.
  `PUMBILITY`, `Perfect Game` and the judgment names stay as the glossaries keep them.
- **Fills.** The only fill v1 needs is the mix name, which a stop puts in through a `{0}` placeholder. This
  replaces Astrid's `{team}` fill.
- **Layout room.** Design for +40% text (UX rule 7): the callout grows to fit its words and never truncates.

**Colors: MixThemes tokens only.**
- **One stylesheet, scanned at zero.** The intro's styles live in their own `wwwroot/css/page-intro.css`, linked
  through `@Assets`. That file is added to `UiColorTokenTests`' scan at a zero allowance, because
  `site.css` is not scanned today.
- **Which tokens:**
  - the dim: `color-mix` on `--mix-bg`;
  - the card, callout and tip: `--mix-surface` and `--mix-ink`;
  - the spotlight ring: `--mix-primary`, the ring a card's hover already uses (UX rule 3);
  - the tip dot: `--mix-accent` with `--mix-glow`.
- **Never the data ramps.** Rarity, difficulty, plate, slot, judgment and lifebar colors mean something on the
  page under the dim, so the intro never uses them.
- **Never dashed edges.** On PIU's chart cards a dashed border is owner-locked state language (To-Do, another
  mix's pass).
- **Dark-only** like every surface. Reduced motion follows the media query `site.css` already uses.

**The shell and the phone.**
- **The dim covers the static shell too.** The header and the bottom nav are server-rendered HTML outside the
  circuit, so the dim is a fixed, window-sized layer above them, as Astrid's covers its header and bottom bar.
- **Thumbs first (UX rule 10).** On a phone the card's buttons sit in the bottom third, and a callout that
  fits neither under nor over its stop pins to the window's foot, above where the bottom nav and any page dock
  sit at rest.
- **The reconnect card stays on top.** When the circuit drops mid-tour, the reconnect card must show above the
  tour's dim: undimmed, it is the only thing saying the buttons are dead.
- **The script owns the geometry.** `tour.js` follows the finger and the scroll with no round trip, in the
  spirit of UX rules 12 and 13. It ships as an ES module through `JSRuntime` import, which `<ImportMap />`
  already versions.
- **Stop copy sits in C#** and travels to the script as text.

**Doctrine: where it goes in `docs/UX-GUIDELINES.md`.**
- **A new numbered rule 15, "A concept is introduced once, in its page's intro."** A page's intro and tour are
  where its new ideas are explained. The surface then uses the same name, and a tooltip never defines it again
  in other words. This sharpens rule 2 ("tooltips are the footnote") rather than replacing it. It also fits rule
  3's bubble lesson: a fact about a chart belongs in the chart dialog, and a concept belongs in the intro, not a
  popover.
- **A new section after section 7, "The first visit (page intros)",** holding this feature's mechanics:
  - the card waits for the answer, never over a patience card (rule 9) or a skeleton;
  - one key per intro;
  - the tour takes no action;
  - tips last the visit;
  - Replay lives on Account.
- **Rule 1 holds unchanged.** The answer is already drawn under the card, so the card adds no explanation
  above the fold. It sits over the answer until one press.
- **Astrid's "no explanatory prose on a working surface" is not adopted wholesale.** That is an open decision
  below.
- **CLAUDE.md's UI conventions** get one bullet for the machine-held parts: `L[...]` for every word, the key
  shape, `page-intro.css` at zero literals, and anchors per page-anchors.md.
- **CODE-MAP.md** gets `Components/Onboarding/`.

**Replay: the Account page.**
- **Where the rows go.** A **First visits** block at the foot of the Profile tab, not a fifth tab, with one row
  per entry in `PageIntros`: the intro's name and a Replay button.
- **What Replay does.** It clears that one key and navigates to the page, a full load, where the card comes up.
  When the clear fails, it navigates anyway.
- **Anonymous visitors.** `/Account` is signed-in only, so they have no Replay (see the open decisions).

**Tests.**
- **bUnit (`Tests.Components`)** holds the rules:
  - the card comes up only when the key is unset or not `true`, and only once `Ready` is true;
  - an unreadable key shows nothing;
  - Skip and Escape write the key, and the card goes even when the save fails;
  - Show me around hands over to the tour and never writes the key itself;
  - each ending of the tour writes or does not write the key, as the requirements give;
  - the Account rows render one per registered intro, and Replay clears only its own key.
- **How bUnit sees the script.** The JS interop calls are asserted through bUnit's own JS interop mock, already
  set to Loose in `ComponentTestBase`. Event tests await `ClickAsync` (`BunitEventDispatchTests`), and stubs go
  on the base's existing mocks (the service seam).
- **The timing lives in the script.** The 3-second hold and the 400 ms fade run in `tour.js`, so no C# test
  waits on time and no new clock package is needed.
- **One Playwright journey earns its place (`Tests.E2E`).** The engine's failure mode, a dim nobody can click
  past, locks the whole page, and only a real browser shows it. The journey runs on PUMBILITY with a seeded
  player:
  - a first visit shows the card over the drawn page;
  - Show me around hands over with no lit frame between the two dims;
  - each stop's spotlight lands and focus stays in the callout;
  - a click on the dim reaches nothing;
  - Done leaves tips;
  - Skip ends the first visit for good;
  - a reload mid-tour starts it over;
  - Replay brings it back.

  Later intros reuse the engine and get bUnit only, unless they add script behavior.

## The checks that hold it

Each lands at zero violations.

- **The anchor ratchets**, per page-anchors.md. Every stop's anchor is a word its page's Razor carries, and
  every word is declared once.
- **`PageIntroCopyTests`** (in `Tests.Components`, which can see the Web types). Every card line, How it works
  step and stop title and words in `PageIntros` is a key in `App.en-US.resx`. `LocalizationKeyTests` then
  carries it to the other eight locales.
  - Why it is needed: a stop written without a key would render English in every locale, and English would
    look perfect.
- **`PageIntroKeysTests`** (same project):
  - every intro's key matches `<Area>__IntroCompleted`;
  - no two intros share a key unless they share a registry entry;
  - every intro's route is a routable `@page`;
  - every intro has a Replay label.
- **`UiColorTokenTests`** extends its scan to `wwwroot/css/page-intro.css`, with no allowance.
- **The existing ratchets** cover the rest unchanged: `StaticAssetVersioningTests` for the stylesheet link and
  the module, `StylesheetStructureTests`, `BunitEventDispatchTests` and `LiveValueWriteBackTests`.

## Draft requirements

Two specs. **`docs/specs/shell/page-intro.md`** (prefix `INTRO`) holds what every intro shares.
**`docs/specs/player-progress/pumbility-intro.md`** (prefix `PMBINTRO`) holds the first page's.

### Requirement: INTRO-001 A first visit shows the intro card once the page has drawn its answer
When a player opens a page that has an intro, the page MUST show the intro card over itself only after it has
drawn its answer, or said why it has none, and exactly when the table gives.

| The intro's setting | The intro card |
|---|---|
| unset, or anything but `true` | comes up |
| `true`, in any letter case and with any surrounding spaces | does not come up |
| cannot be read | does not come up |

#### Scenario: the card waits for the answer
- **GIVEN** Sam Example has never had the PUMBILITY intro, and the PUMBILITY page takes 4 seconds to work out
  Sam's projections
- **WHEN** Sam opens the PUMBILITY page
- **THEN** the patience card shows for those 4 seconds with no intro card
- **AND** the intro card then comes up over Sam's number and the page under it

### Requirement: INTRO-002 What the intro card holds
The intro card MUST show the page's name, one line saying what the page is for, a list of what its tour shows,
and Skip and Show me around, all in the player's language.

#### Scenario: Sam reads the card
- **GIVEN** Sam Example reads the site in Korean and has never had the PUMBILITY intro
- **WHEN** the intro card comes up over the PUMBILITY page
- **THEN** its name, its line, its list and both buttons read in Korean, with PUMBILITY written as the Korean
  glossary keeps it

### Requirement: INTRO-003 An intro several pages share fades into How it works
Where an intro is shared by several pages, the intro card MUST offer Next in place of Show me around, and Next
MUST fade the card's contents into How it works, its numbered steps followed by Back, Skip and Show me around,
without leaving or reloading the page.

#### Scenario: from the card to How it works
- **GIVEN** the intro card is up over Sam Example's first visit to PUMBILITY Breakdown
- **WHEN** Sam presses Next
- **THEN** the card fades to How it works, its steps numbered from 1, over the same page at the same scroll

### Requirement: INTRO-004 The page under the card is out of reach
While the intro card is up, the page MUST keep everything outside the card out of reach as the table gives.

| Reached by | While the card is up |
|---|---|
| a pointer or a tap | the whole window outside the card, the header and the bottom nav included, is dimmed, and a click there reaches nothing |
| the keyboard | focus starts on the card's main button, and Tab and Shift+Tab stay among the card's buttons |
| a screen reader | the card is a modal dialog named by its heading and described by its line |

#### Scenario: tapping the dimmed pool picker
- **GIVEN** the intro card is up over Sam Example's first visit to PUMBILITY
- **WHEN** Sam taps Doubles in the dimmed pool picker
- **THEN** the pool stays as it was and the card stays up

### Requirement: INTRO-005 What ends the first visit
A page MUST end a player's first visit, setting the intro's setting to `true`, exactly when the table gives,
and MUST take the card or the tour down even when that save fails.

| What happens | The first visit |
|---|---|
| Skip, or Escape, while the card is up | ends at once, and no tour starts |
| Show me around | goes on until the tour ends |
| the tour ends with Done, Skip or Escape, or the player leaves the page during the tour | ends |
| the player leaves or reloads the page while the card is up | does not end |
| the page reloads during the tour, or the tour breaks (see INTRO-013) | does not end |

#### Scenario: the save fails
- **GIVEN** the intro card is up over Sam Example's first visit, and Sam's settings reject every save
- **WHEN** Sam presses Skip
- **THEN** the card goes
- **AND** the card comes up again on Sam's next visit

### Requirement: INTRO-006 A visitor who is not signed in keeps the first visit in this browser
When a visitor who is not signed in ends a first visit, the site MUST remember it in that browser only, and a
signed-in player's first visit MUST be read from their account, whatever the browser holds.

#### Scenario: signing in on a browser that skipped
- **GIVEN** Riley Example skipped the tier lists intro before signing in, and Riley's account has never had it
- **WHEN** Riley signs in and opens the tier lists
- **THEN** the intro card comes up

### Requirement: INTRO-007 Show me around starts the tour on the page as it stands
When a player presses Show me around, the page MUST start its tour on the first stop, without leaving or
reloading the page, keeping the mix, the pool, the filters and the scroll it had.

#### Scenario: the pool the card was over
- **GIVEN** the intro card is up over Sam Example's PUMBILITY page, scoped to the Singles pool
- **WHEN** Sam presses Show me around
- **THEN** the first stop shows, counting 1 of 5, and the page is still scoped to Singles

### Requirement: INTRO-008 The card gives way once the tour is in place
When a player presses Show me around, the page MUST keep the intro card and its dim up until the tour's first
stop is in place, the tour has ended or 3 seconds have passed, whichever comes first, and then fade them out.

#### Scenario: a slow first stop
- **GIVEN** Sam Example pressed Show me around, and the first stop's spot shows 4 seconds later
- **WHEN** 3 seconds have passed
- **THEN** the card and its dim fade out, and the first stop appears a second later

### Requirement: INTRO-009 What a stop's callout holds
At each stop, the tour MUST show a callout holding the counter, as the stop's number of the tour's stops, then the
stop's title and words, and Skip on every stop, Back on every stop but the first, Done on the last and Next on
the rest.

#### Scenario: the last stop
- **GIVEN** Sam Example is on stop 4 of a five-stop tour
- **WHEN** Sam presses Next
- **THEN** the callout counts 5 of 5 and offers Skip, Back and Done

### Requirement: INTRO-010 Where the spotlight and the callout sit
At each stop, the tour MUST bring what the stop points at into view, light it as one area, dim the rest of the
window, and keep the callout beside it as the page scrolls, the window resizes or a phone rotates, as the table
gives.

| What | Where |
|---|---|
| what the stop points at | centred in the window, or from its top when it stands taller than six tenths of the window |
| the callout | under it when that fits, else over it, else beside it on a window at least 1,024 pixels wide, else pinned to the window's foot above the bottom nav |
| the page's end | blank room half the window's height while the tour runs, gone when it ends |

#### Scenario: a phone with no room
- **GIVEN** Sam Example is touring PUMBILITY on a 390 by 844 phone
- **WHEN** the tour reaches a stop that fills most of the window
- **THEN** the callout is pinned to the window's foot, above where the bottom nav sits at rest

### Requirement: INTRO-011 The page under a tour is out of reach but still scrolls
While a tour runs, a click or tap outside the callout MUST reach nothing on the page, the lit part included, while
a wheel, trackpad or finger scroll MUST still scroll the page under the dim, the spotlight and callout following.

#### Scenario: tapping the lit tabs
- **GIVEN** Sam Example is on the stop that lights PUMBILITY's tabs
- **WHEN** Sam taps PUMBILITY Breakdown in the lit area
- **THEN** the page stays where it is, and the tour stays on that stop

### Requirement: INTRO-012 A stop waits 5 seconds for what it points at
If nothing a stop points at shows within 5 seconds of the tour reaching it, then the tour MUST step past it: to the
next stop, or as Done does on the last.

#### Scenario: a spot that never shows
- **GIVEN** Sam Example's page does not show what stop 3 points at
- **WHEN** 5 seconds pass on stop 3
- **THEN** the tour moves to stop 4

### Requirement: INTRO-013 A tour takes no action, and a tour that breaks ends cleanly
A tour MUST NOT press, save, import or send anything, nor change any setting but its own intro's, and a tour whose
script fails, or whose page reloads, MUST end at once, leaving no dim and no tips and the first visit not ended.

#### Scenario: the script fails
- **GIVEN** Sam Example is on stop 2, and the tour's script fails as it shows the next stop
- **WHEN** Sam presses Next
- **THEN** the dim and the callout leave the page, and no tip dots show
- **AND** the intro card comes up again on Sam's next visit

### Requirement: INTRO-014 Leaving the page skips the tour
When the player moves to another page while a tour runs, the tour MUST end as Skip ends it, and MUST NOT end when
only the address's query changes on the tour's own page.

#### Scenario: the browser's Back
- **GIVEN** Sam Example is on stop 2 of the PUMBILITY tour, having come from the home dashboard
- **WHEN** Sam presses the browser's Back
- **THEN** the home dashboard opens, and Sam's next visit to PUMBILITY shows no intro card

### Requirement: INTRO-015 Done leaves tip dots
When a tour ends with Done, the page MUST show a pulsing tip dot at the top-right corner of what each stop pointed
at, while that spot shows, and a tour ended any other way MUST leave no tip dots.

#### Scenario: Done or Skip
- **GIVEN** Sam Example is on the last stop of the PUMBILITY tour
- **WHEN** Sam presses <ending>
- **THEN** the page shows <dots>

| ending | dots |
|---|---|
| Done | a tip dot on each stop's spot |
| Skip | no tip dots |

### Requirement: INTRO-016 A tip shows its stop again until the tips are hidden
A tip dot MUST open its stop's title and words, headed Tip, with one button, Hide these tips, when pointed at,
focused or tapped, one tip open at a time, and every dot MUST go when the player presses Hide these tips, leaves
the page, reloads it or starts a tour.

#### Scenario: hiding the tips
- **GIVEN** Sam Example finished the PUMBILITY tour with Done, and the tip on Your bar is open
- **WHEN** Sam presses Hide these tips
- **THEN** every tip dot leaves the page

### Requirement: INTRO-017 Intros keep still under reduced motion
Where the player's device asks for reduced motion, the card's fade, the scroll to each stop, the spotlight's moves
and the tip dots' pulse MUST happen at once, with no animation.

#### Scenario: still tips
- **GIVEN** Sam Example's device asks for reduced motion
- **WHEN** Sam presses Done on the PUMBILITY tour
- **THEN** the tip dots show without pulsing

### Requirement: INTRO-018 The intro stays in the mix's colors
The intro card, the dim, the callout, the spotlight and the tip dots MUST take their colors from the mix the page
is themed in, and MUST NOT use a grade, plate, difficulty, rarity, judgment or lifebar color.

#### Scenario: an XX-themed page
- **GIVEN** Riley Example's theme override is XX
- **WHEN** the intro card comes up over Riley's tier lists
- **THEN** the card, the dim and the spotlight wear XX's palette

### Requirement: INTRO-019 Replay starts a first visit over
When a signed-in player presses Replay in a row of First visits on the Account page, the Account page MUST clear
that intro's setting alone and open its page, and MUST open the page all the same when the setting can't be
cleared.

#### Scenario: replaying one intro
- **GIVEN** Sam Example has completed the PUMBILITY and tier lists intros
- **WHEN** Sam presses Replay in PUMBILITY intro and tour
- **THEN** the PUMBILITY page opens with the intro card over it
- **AND** the tier lists intro stays completed

### Requirement: PMBINTRO-001 The PUMBILITY pages share one first visit
The PUMBILITY pages (Play, PUMBILITY Breakdown, Hardmode and Phoenix 1) MUST share one intro and one setting,
`Pumbility__IntroCompleted`, showing the card on whichever of them the player opens first.

#### Scenario: Breakdown first
- **GIVEN** Sam Example has never had the PUMBILITY intro, and skips it on PUMBILITY Breakdown
- **WHEN** Sam opens Play
- **THEN** no intro card comes up

### Requirement: PMBINTRO-002 How it works follows the mix's own rules
The PUMBILITY intro's How it works MUST describe the rules of the mix the page is on, as the table gives.

| The mix | How it works says |
|---|---|
| Phoenix | each chart you pass is worth points from its level and your grade; your 50 best make one pool; the pool's total is your PUMBILITY |
| Phoenix 2 and the RISE mixes | each chart you pass is worth points from its level, your grade and your plate; your 50 best, Singles and Doubles together, make your pool, and Singles and Doubles each keep a pool of their own too; the total is your PUMBILITY, and the titles climb with it |

#### Scenario: Phoenix says nothing about plates
- **GIVEN** Riley Example's selected mix is Phoenix
- **WHEN** Riley reads How it works on the PUMBILITY intro
- **THEN** no step mentions plates or separate Singles and Doubles pools

### Requirement: PMBINTRO-003 The PUMBILITY tour's stops
The PUMBILITY tour MUST stop, in order, at the number, the pool picker where the mix has more than one pool,
Your bar, the section's tabs, and one stop for the page it runs on, the same for every player.

#### Scenario: a mix with one pool
- **GIVEN** Riley Example's selected mix is Phoenix, which has one pool
- **WHEN** Riley starts the tour on Play
- **THEN** the tour counts 4 stops: the number, Your bar, the tabs, and Play's peers

## Build order

1. **The spec change.** Commit the two specs from the drafts above and add their lines in
   `docs/specs/README.md`, then wait for the owner's review. This file is deleted in the same PR.
2. **Anchors first.** page-anchors.md's contract, anchor map and ratchets must exist. Then mark PUMBILITY's
   frame, hero, pool picker, bar, tabs and each page's own region.
3. **The model and the copy.** Add `Tour`, `TourStep`, `PageIntros`, and the keys in all nine resx files in
   alphabetical order. Add `PageIntroCopyTests` and `PageIntroKeysTests`.
4. **The card.** `PageIntro` reads the key after first render, waits for `Ready`, shows the card, and
   handles Skip, Escape and Next into How it works, as a plain overlay. Add `page-intro.css` with its
   `UiColorTokenTests` scan, and bUnit tests.
5. **The tour.** Port `tour.js` (the spotlight, placement, focus trap, scroll-through, 5-second wait and
   3-second hold). Add the callout and the endings, with bUnit tests on the interop calls.
6. **Tips.** Dots after Done, one open tip, and Hide these tips.
7. **Replay.** The First visits block on Account's Profile tab, rendered from `PageIntros`, with bUnit tests.
8. **Wire PUMBILITY.** Place `PageIntro` in `PumbilitySectionFrame`, with `Ready` on the frame's page record.
   Add the Playwright journey.
9. **Docs in the same PR.** UX-GUIDELINES.md (rule 15 and the new section), the CLAUDE.md UI bullet, and
   CODE-MAP.md.

Each step is its own commit. The card works and is tested before the script exists, and the script lands
before any page uses it.

## Candidate first pages

| Page | What it assumes a new player lacks | Fit |
|---|---|---|
| **PUMBILITY** (`/Pumbility` and its tabs) | a top-50 pool, what one chart is worth, your bar, PUMBILITY peers, separate Singles and Doubles pools on Phoenix 2 | **build first** |
| Tier lists (`/TierLists`) | bands built from community scores, the PUMBILITY lens, My Scores grouping, density | second: public, so it proves the anonymous path |
| Communities | creating vs joining, invites, a community's board | later |
| The import flow | piugame sign-in, what an import reads | not an intro: a task flow where a card would stand between a new player and their first import; `/Setup` and rule 9's empty states serve it |

**Build PUMBILITY first.** It carries the heaviest load of ideas a new player has never met, and its page already
explains itself in on-surface lines that an intro could carry instead. It is signed-in only, so the first build
needs only the account path: the browser-storage path can wait for the tier lists. Its four pages share one
frame, which proves the shared intro and its How it works once. All four are already interactive. And the
section already links the formula from its own eyebrow ("How it works", on the calculator), so the intro tells
the story while the calculator stays the reference.

## Open decisions for the owner

1. **Show a new intro to every existing player once, or only to new ones?** Recommendation: everyone, once.
   Skip is one press, and PIU has no reliable "new account" signal: `Universal__SetupCompleted` is absent on
   every account older than `/Setup`.
2. **Adopt Astrid's "no explanatory prose on a working surface"?** Recommendation: not wholesale. Adopt rule 15
   (introduce once, never redefine in a tooltip) now. Keep the existing lines, such as Your bar's "Nothing
   counts until it beats this", and decide per page as its intro lands whether a line moves into the intro.
3. **Replay for visitors who are not signed in.** `/Account` is signed-in only. Recommendation: none in v1, so
   they get the intro once per browser. Revisit when the help assistant lands, since its help box can offer
   the tour on any page.
4. **One Playwright journey for the engine.** UX test ladder: Playwright only for critical journeys.
   Recommendation: one, on PUMBILITY, because a dim nobody can click past locks the page and only a browser
   shows it. Later intros get bUnit only.
5. **Tip dots on touch phones.** The difficulty bubble lost its tooltip because hover tips got noisy and stuck
   open on touch. Recommendation: keep the dots for the visit only, opening on tap, and drop them in the field
   test if they read as noise.

## Not ported

- **The roster greeting, the role views and "Not quite right?"** Astrid greets a person by name, role and team,
  and picks a tour per audience. PIU has one audience per page (players), so every intro is the one-audience
  variant: one card, one tour.
- **Cross-page tours and the page fade between pages.** Astrid's "Tour the X page" and its fade under a
  shared layout ride one circuit across a navigation. PIU's every navigation is a full document load, so a
  tour runs on its own page, and the only fade is inside the card, into How it works.
- **The scripted demo.** Astrid plays a fake AI answer at one stop, with Next and Back held while it plays.
  No PIU page needs one yet, and it is the most fragile part of the engine.
- **Examples drawn on the page before something has happened.** Astrid fills empty spots with marked fake
  data. A PUMBILITY intro for a player with no scores is tempting, but the marking Astrid uses, a dashed edge,
  is already owner-locked state language on PIU's cards. It waits for its own design.
- **The help box's Show me ring, the lesson inventories and the control budget.** These belong to the help
  assistant and to page-anchors.md, not to this feature.
