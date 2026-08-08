# UX Guidelines

The design philosophy and the rules that realize it — the UI sibling of [ARCHITECTURE.md](ARCHITECTURE.md). Same deal as there: philosophy first, then rules; the machine-enforceable subset is ratcheted by architecture tests and mirrored in [CLAUDE.md](../CLAUDE.md). Domain terms (Mix, Chart, Phoenix score, plate, judgment) are defined in [DOMAIN.md](DOMAIN.md).

Everything here was decided in the 2026-07 theme & shell workshop, calibrated against in-game and official-site captures of XX, Phoenix, and Phoenix 2.

---

## 1. The theme system

### One palette per mix, dark-only

The site is dark-only by design (the arcade is dark; the games are dark; the old "light" palette was a copy of the dark one). Each playable mix has one calibrated brand palette in [`MixThemes`](../ScoreTracker/ScoreTracker/Services/Theming/MixThemes.cs):

| Mix | Identity (from the games' own art) |
|---|---|
| **XX** | Violet-black ground, dueling magenta + cyan neon, XX-logo yellow accent |
| **Phoenix** | Cosmic blue ground, electric-blue chrome, fire and gold as accents |
| **Phoenix 2** | Acid green on teal space, electric magenta accent |

The palette record is the single source of truth: it builds the MudBlazor `MudTheme` **and** emits the `--mix-*` CSS custom properties, so Mud components and hand-styled markup can never drift apart.

**Resolution order**: the `/Account` "Site Theme" override → the currently selected mix → Phoenix. Theme resolves once at layout init; mix switching already forces a full reload.

### Two token tiers

1. **Brand tokens** (`--mix-bg`, `--mix-surface`, `--mix-primary`, `--mix-accent`, `--mix-ink`, `--mix-glow`, …) — what makes Phoenix feel like fire-over-cosmos and XX feel like neon. These change freely per mix.
2. **Semantic tokens** — colors that carry *data meaning*. Hue is tuned per mix; **meaning and ordering never change**:
   - `--rarity-*` — the rarity ramp (below)
   - `--diff-*` — the difficulty ramp (below)
   - `--plate-*` — plate colors on the official metal ladder (PG/UG ice-blue, SG/EG gold, TG/MG silver, FG/RG bronze, `--plate-none` for unplayed)
   - `--slot-*` — legacy slot colors, the pre-Exceed song-wheel vocabulary (Crazy red, Freestyle green, Nightmare purple, `--slot-neutral` for HDB/levelled co-ops). **Deliberately not the difficulty ramp**: old-scale levels don't translate to modern ones, and legacy chips render as CSS (never image bubbles) so the different look announces the different scale ([legacy-mixes design](design/legacy-mixes.md)).
   - **Grade colors** (`MixThemes.GradeHex(name)`) reuse the **plate ladder by tier** (owner, 2026-07-13, sampled from the Play Data art): SSS+/SSS = PG/UG ice-blue, SS/S = EG/SG gold, AAA+/AAA = MG/TG silver, AA/A = FG/RG copper, and everything **below A = the in-game sub-A green**. Grades still render as art in most places; these literal hexes exist for chart bars (the first text-rendered consumer). `MixThemes.PlateHex(shorthand)` is the plate sibling.
   - **Chart-type colors** (`MixThemes.ChartTypeHex(type)`): the difficulty-ball vocabulary — **red Single, green Double, gold Co-Op**. `MixThemes.UnpassedHex` is the broken-grade grey for unpassed / not-cleared / below-threshold segments.
   - `--judg-*` — the game's own **judgment** vocabulary: perfect ice-blue, great green, good amber, bad violet, miss red. Reached via `ThemeScales.JudgmentColor(judgment)`, or `MixThemes.JudgmentHex` for render targets that can't read custom properties (ApexCharts). **Mix-invariant, like the alert colors** — a MISS must read as a miss in every theme.
   - `--life-*` — the **lifebar's** own zones: the seven rainbow stops (`--life-r1`…`--life-r7`) plus the ready-made `--life-rainbow` gradient for the visible bar, `--life-overflow` for the cool chrome above 1000 that the cabinet never shows you, and `--life-danger`. Also mix-invariant; `ThemeScales.LifeRainbow/LifeOverflow/LifeDanger` are the accessors. Introduced with the [Life Calculator redesign](design/life-calculator-redesign.md).

Consumers never look up hues. C# code calls the [`ThemeScales`](../ScoreTracker/ScoreTracker/Services/Theming/ThemeScales.cs) façade; markup uses `var(--…)`. Both return token references, so components stay theme-blind.

### The two semantic ramps

PIU's own visual language converges across judgments, grades, and plates: **red means danger, blue means elite, and achievement climbs bronze → silver → gold → blue**. The ramps adopt that language instead of inventing one.

**Rarity** (how good is this relative to the population — percentiles, leaderboard positions). Starts at **neutral grey, never red**: a low percentile is *common*, not a failure. Bands name their color on purpose — show-don't-tell survives localization:

| Band | Percentile | Reads as |
|---|---|---|
| Common | bottom 25% | neutral grey |
| Silver | 25–50% | silver |
| Emerald | 50–75% | green |
| Gold | 75–90% | gold |
| Sapphire | 90–99% | ice blue |
| Prism | top 1% | near-white chrome + full glow |

Percentile semantics are the established `ScoreRankingRecord.Ranking` convention: fraction of the comparable population at or below you, 1.0 = first place. Because gold is inherently brighter than sapphire, hue luminance can't order the bands — the **glow treatment ramp** (`.rarity-glow-1..3`) does, and the player's standing always renders alongside the color.

⚠ **A percentile is stored one way and shown the other, so printing it needs the flip.** `1.0` is first place; "top 1%" is first place too — `TopShare` in `PhoenixRecap.razor` takes `1 − percentile` at the call site for exactly this reason. Prefer printing a **place** (`#6 of 94 peers`) where a rank is available: it carries the same fact, cannot be read backwards, and needs no threshold rule for the top of the board. The session score row and the Discord score card both do this, in the same words.

**Difficulty** (how hard is this chart relative to its level — tier lists). The familiar green→red heat, `TierListCategory` → `--diff-*`. Red is at home here exactly because the rarity ramp refuses it.

**Direction of change** (a chart moved between mixes — the `/MixChanges` diff) borrows two steps off that same ramp rather than inventing a third scale: warm `--diff-hard` means the game now calls it harder, cool `--diff-overrated` means easier. Deliberately **not** red/green — green already means "easy for its level" two paragraphs up, and up/down is polarity, not good/bad. Both directions print their arrow and numeric delta alongside the colour (rule 8), and a diverging encoding uses **one scale for both sides**: px-per-chart above and below the midline must match, or the smaller side is a lie.

**A folder is how players navigate a big list, and `FolderPicker` is that navigation.** A page holding hundreds of charts shows **one folder at a time** rather than every group stacked into a scroll (owner, 2026-07-26, on the mix diff). Where a host's data is sparse, it passes `IsEnabled` and the picker greys the folders it has nothing for — dimmed, never hidden, because the holes are the map of where the change landed — and its ◄ ► steppers skip straight to the next folder that has something. A type whose every folder is empty has its tab disabled, and the grid opens on one that isn't.

### The viewport ladder

One ladder, sitewide. A page picks the rungs it needs; it does not invent its own numbers.
Every rung below is MudBlazor's breakpoint scale or a device the site is actually field-tested
on — the tablet and fold widths are measured, not rounded guesses.

**Four classes, and the rungs between them** (owner, 2026-08-05). Think in classes, not device
model numbers — a class survives the next hardware refresh and a pinned device width does not:

| Class | Rung | The shapes in it |
|---|---|---|
| **Desktop** | **≥ 900** | Laptops, landscape tablets (iPad Air/10th gen land at **1180**, Pro 11" at **1194**), portrait 12.9" iPads (1024) |
| **Tablet** | **700 – 900** | Portrait tablets. Note the real range is **820 – 1032** and straddles the top rung: an iPad Air is 820 portrait, a Pro 13" is 1032 |
| **Fold unfolded** | **500 – 700** | ~700 wide, aspect ~0.84 — **taller than 1:1**. ⚠ A Z Fold 7 runs a little **above** 700, which puts it in the tablet class. That is the right outcome (it genuinely has tablet-class room) but it means 700 is the class *floor*, not the handset — measure a real one before any rule leans on where it lands |
| **Mobile** | **< 500** | Phones, and a fold's cover screen |

⚠ **`768` is dead as a tablet width.** It is the 2010-era iPad and no tablet in circulation
reports it. It survives in the [static-shell.md §5](design/static-shell.md) FT matrix as a record
of a test that already ran — don't pin new work to it.

The rungs the codebase already turns on, and what each one carries:

| Rung | Where it comes from | What moves |
|---|---|---|
| **1280** | Mud `Lg` | Tier Lists folds into Play ([static-shell.md §11.1](design/static-shell.md)). Picked over a tighter ~1150 so that landscape iPads (1180/1194) sit on the folded side rather than rendering every nav label in their tightest configuration |
| **960** | Mud `Md` | The shell's desktop/mobile switch |
| **760 / 600 / 500** | the boards | A board sheds one column at a time: song titles step aside at 760, long labels fall back to short forms at 500. **Figures never stack — every value keeps its own column** |
| **`(min-aspect-ratio: 1/1), (min-width: 700px)`** | the More sheet | Squarish-or-wide gets the icon grid, narrow-and-tall the drill-down. **The width floor carries both the portrait tablet and the fold** — both are taller than 1:1 and both fail the aspect half, so the floor is the only thing keeping either off the phone treatment. That is why it is 700 and not a rounder number |
| **`max-height: 520px`** | the session breakdown | The rule no width query can express — see below |

**Two shapes any width-only ladder gets wrong:**

- **Landscape phone, 844 × 390.** A width rule stacks the layout at 844px when *height* is the
  scarce axis — exactly backwards. `max-height: 520px` compresses the vertical budget and
  *un-stacks*. It keys on height rather than aspect on purpose, because a landscape tablet
  (1024 × 768) is also wider than 1:1 and must **not** compress.
- **Portrait tablet.** Wide in pixels, still short of horizontal room. The Rivals compare table
  is the worked example — `(max-aspect-ratio: 1/1), (max-width: 599.98px)` drops it to grade art
  and two score stacks. Prefer **grade art over a printed score** when a row runs out of room:
  the art is narrower than the number it replaces, and rule 8 is already satisfied by the score
  riding the tooltip.

⚠ **Never place a breakpoint within a scrollbar's width of a target device.** A media or
container query measures the **content box**, and a scrollbar takes 2px (overlay) to ~15px
(classic) off the nominal width — so a rule at 899 flips a 900px viewport one way on macOS and
the other on Windows. Leave a margin, and never sit a rung directly on a device's nominal width.

⚠ **A fold changes its viewport mid-session.** The shell renders once per document with no
circuit to re-render it, so anything that must survive an unfold has to be a media query —
CSS, never Razor ([static-shell.md §11](design/static-shell.md)).

---

## 2. The rules

**1. The answer lives above the fold.** Every page has one job; the answer to that job is visible without scrolling at 390×844 (a phone) and instantly at desktop. Filters, explanations, methodology, and history come *after* the answer, never before.

**2. Show, don't tell — the jacket is the identifier.** Players recognize song art faster than song names. Encode with the game's own vocabulary first: jacket art, red Single / green Double bubbles, grade art, plate metals, the two ramps. Text labels are the fallback; tooltips are the footnote. If a number can be a bar, bubble, or color, it is one — with the number still present (see rule 8).

**3. One concept, one component.** Difficulty is always `DifficultyBubble`; a grade is always `LetterGradeIcon` (or `GradeBandIcon` where the value is a world-first band string and the top rung is the perfect-game plate); a score is always `ScoreBreakdown`; a player is always `UserLabel`. A new visual concept means a new shared component in `Components/` — never a page-local restyle of an existing one.
   **A component owns its own narrow-width behavior.** `UserLabel` is the worked example: it renders its own `.user-label` flex row (flag pinned, name the only part allowed to shrink, `max-width:100%`) so *every* bounded caller truncates the name. Left to callers, each one re-derived it and the ones that didn't printed a bare "…" for anyone with a country flag — the label overflowed as a single atomic box and the container's ellipsis swallowed it whole. A caller's job is to bound the width; the component's job is to survive it.

**4. No raw color literals.** ⚙ *Ratcheted by `UiColorTokenTests`.* UI code under `Pages/`, `Components/`, and `Shared/` reads theme tokens, not hex strings or `Colors.*` constants. The allowlist is launch-day debt and only shrinks; each page overhaul burns down its own entries. (Exception by design: the Phoenix Recap deck is self-styled slide art and stays allowlisted.)

**5. Density is a setting, not a redesign.** Three sanctioned densities — **Comfortable** (cards), **Compact** (the jacket "sticker sheet"), **Table** (rows) — stored per user **per page** in UiSettings under `Density__<Page>` (e.g. `Density__TierLists`): players use different densities for different tasks, so the choice travels with the page, not the site. A collection page picks its default and honors the stored choice; it never invents a fourth mode. (Landed with the tier-list overhaul; the previously reserved `Universal__Density` key was retired unshipped.)
   **A leaderboard is compact rows, not a table, and the Official Leaderboards rankings board is the golden standard** (owner directive, official-leaderboards field test rounds 5–6). Every board of ranked entities — rankings, chart boards, weekly boards — renders one dense flex row per entry: place and week delta left, identity next, payload (rating, score, archetype chip) right-aligned in a tail, wrapping instead of side-scrolling, no density toggle and no per-row card. When a board genuinely needs table semantics — sortable columns, like a player's placements sheet — it keeps the `<table>` but wears the same skin: flat transparent surface, hairline separators (`--mix-ink` at ~12%), tight row rhythm, quiet uppercase headers. Never a card around a board, never heavier lines than the rankings rows. Real tables — numeric matrices like the What It Takes ladder — are exempt: they *are* tables and keep table styling. The skin itself is sitewide vocabulary in `wwwroot/css/site.css` — `.olb-rank-card` for a row, `.olb-board-table` for the table variant, `.olb-rank-tail` for the payload, `.olb-row-me` / `.olb-row-community` for the glows — so a new board wears the standard by using the classes, not by re-deriving them. 

**6. Filters are furniture.** The filter entry point and its active-filter chips (labeled, removable) sit in a content bar directly above the list they affect; the full panel lives in a drawer; at phone widths the bottom action bar keeps filters thumb-reachable. Filters never push the answer below the fold. The sticky toolbar is reserved for controls that change *what data* you're looking at — presentation controls (density, download, filters) travel with the content instead. (Amended in the tier-list overhaul field test; previously the filter row itself was sticky.)

**7. Design for +40% text.** Eight-plus locales; Portuguese and French run long, CJK runs dense. Every string goes through `L[…]`, new keys land in **every** locale in the same pass (glossaries: `LOCALIZATION-<locale>.md`), no fixed-width labels, no truncation without a tooltip. **Universal terms never translate**: in-game memes ("Why Don't You Get Up and Dance, Man?") and community proper nouns (Chabala, PIU Center, PG) keep their original value in every locale.

**8. Color is never the only channel.** Every color encoding pairs with a second signal: the rarity ramp's monotonic glow + the printed standing (a place where one exists, a percentile otherwise), pass/fail borders + icons, S/D bubbles + the S/D numeral. Verify new encodings under a colorblind simulator before shipping.
   **The grade ring** (`QualifierChip`, [qualifiers-overhaul.md](design/qualifiers-overhaul.md)) is the worked example of why: a jacket ringed in its letter grade's metal reads at a glance and costs a third of the width that grade art does, which is what lets a ten-chart top N fit a board row. But the ladder is **deliberately not injective** — `MixThemes.GradeColors` gives SS+/SS one metal, as it does S+/S, AA+/AA and A+/A — so the ring can never travel alone. The rating prints under every chip and the exact grade and score ride the `title`. Reuse `MixThemes.GradeVar`; never re-derive the mapping.

**9. Loading looks like the layout.** Skeletons match the shape of the content they become — never a lone centered spinner on a data page. Empty states name the action that fills them ("Import your scores to light this up"), not just the absence.

   **A wait the page cannot shorten gets the patience card** (`PatienceCard`, landed with the PUMBILITY projection cache). It stands in for a **whole region**, full width with its contents centred — not one slot inside a region beside a pulsing skeleton, which reads as a second thing still loading next to the thing that is telling you so (owner, 2026-08-08). It prints **two lines with two owners**, and the split is what makes it reusable: the page supplies the explanation, so it is always specific about what is being worked out and why it is slow, and the card supplies a flavour line drawn from a shared pool that says nothing about any page's work, so it can never be wrong somewhere it was not written for. The phrase is drawn **once per load** through `IRandomNumberGenerator` — one that rotates while you wait pulls the eye back to the thing you are waiting on. Adding a page to the pattern is one component and one sentence; adding a phrase is one key in nine locales, and the pool has no fixed size.

**10. Thumbs first on mobile.** Primary navigation and primary actions live in the bottom third at phone widths (the bottom nav is this rule applied to the shell). The top corners are for identity and context, not workflows. Action-heavy pages claim the bottom third through the **page dock** (`PageDock` component → shell slot above the bottom nav): scrolling down slides the nav away so the two bars only coexist at rest, and the nav's items never move or reflow. **Focus mode** (`PageDock FocusMode`) drops the shell chrome entirely for takeover tasks — kiosk-style flows like tournament drafts — and the page owes the user an explicit exit affordance in return. A page that registers no dock gets the unchanged shell. (Landed with the randomizer overhaul, [docs/design/randomizer-overhaul.md](design/randomizer-overhaul.md).)

**11. A destructive form arms itself, and its button names the damage.** Two patterns from the
delete-my-data pass, and both generalize. **The armed-form gate**: a destructive form renders
*visible but inert* until an explicit opt-in — never hidden behind an expander, because a hidden
form makes people hunt and hunting makes them determined; visible-and-inert lets someone read what
it offers and talk themselves out of it. **The blast-radius button**: a destructive confirm says
what is about to happen — *Delete my Phoenix scores* — rather than *Confirm*. The count and the
scope outperform another acknowledgement checkbox, and they fix the failure mode where nobody can
tell what a control is about to take. Derived values that are recomputed from what is being deleted
are stated as a consequence, never offered as a checkbox.

---

## 3. Enforcement

- **`UiColorTokenTests`** (ArchitectureTests) scans `Pages/`, `Components/`, `Shared/` for hex literals and `Colors.*` constants against a shrink-only allowlist. Exceeding an allowance fails; dropping *below* one also fails until you lower the entry — that's the ratchet.
- Adding a color: if it's brand, it belongs in a `MixPalette`; if it carries data meaning, it belongs in a semantic token group with a `ThemeScales` accessor. If it's neither, question it.
- The remaining rules are review discipline today. Candidates for future ratchets: `L[…]` coverage scanning, skeleton-presence checks on data pages.

## 4. Home dashboard widgets

The widget home page ([design doc](design/HomePageWidgets/README.md)) adds a vocabulary with its own rules:

- **The host owns the chrome.** `WidgetHost` renders the card frame, title, edit controls, per-cell
  `ErrorBoundary`, and the unknown-type fallback. Widget components render **bodies only** — never
  their own card, title bar, or error handling.
- **Lifecycle states are mandatory** (design doc §2.3): fixed-footprint skeleton, empty state with a
  setup CTA (an empty widget is an onboarding surface, not a blank box), isolated errors, and configs
  that tolerate old blobs forever. The board never gates as a whole.
- **Sizes are presets** on the 4-column grid (`1x1`, `2x1`, `1x2`, `2x2` …), declared per widget type.
  No freeform resize. Mobile derives a single column from auto-flow order; drag is desktop-only and
  the arrows are the accessible path everywhere.
- **Mix resolution cascades**: widget config override → page default → current mix. Widgets declare
  `SupportedMixes` and clamp.
- **Config blobs are public API** (D19): camelCase via `WidgetConfigJson`, exported/imported and
  described by the capability schema, pinned by `Tests.Api` goldens. Changing a config record's shape
  or a `TypeId` is breaking-change review.
- **Widgets reload only when their identity inputs change** (instance id, config, effective mix) —
  edit-mode mutations elsewhere on the board must not refetch every widget.
- Chart series colors resolve through literal-hex accessors on `MixThemes` (ApexCharts can't read CSS
  vars), all mirroring `DifficultyHex`: **chart type** = `ChartTypeHex` (red Single / green Double / gold
  Co-Op, the ball vocabulary — the By-Level Breakdown distribution lines encode S/D by this color, the
  stat by line emphasis); **grade / plate bars** = `GradeHex` / `PlateHex` (identity colors, *not* the
  rarity ramp); **rarity / completion tiers** = `RarityHex(mix, band)`; **qualitative** = `SeriesHex(i)`.
  The Competitive Level graph still uses the per-mix `MixPalette` chart pair — a candidate to unify onto
  `ChartTypeHex` so red/green S/D reads the same everywhere.
- **Chart rows/cards in widgets open `ChartDetailsDialog` on click** (browse mode only — edit mode
  owns clicks for arranging). Every catalog widget inherits this rule.
- **Per-chart leaderboards use the shared `LeaderboardDialog`** (every entry, your own row glowing
  in place — the `MaxPlaces` cap was retired in the challenges-hub overhaul; the dialog scrolls):
  the caller passes the entries and a sort direction, so an inverted board — Daily Step's weekly
  **Limbo Day**, where the lowest *passing* score wins — ranks ascending without a second component.
  Rows wear the **trust ladder** when the caller tracks provenance: ✔ officially imported > 📷 photo
  attached (the icon opens the proof) > nothing for a bare self-report (weekly-charts-overhaul.md M5).
- **Drag is swap, not insertion**: dropping widget A on widget B trades their places; bystanders
  never move. The arrows remain the accessible and mobile reorder path.
- **Quiet scrolling**: widget inner scrollers use `dash-scroll` — no scrollbar at rest, a thin themed
  thumb on hover, and edge fades as the "there's more" affordance (scroll-aware where the browser
  supports scroll-driven animations, static bottom fade elsewhere). Touch keeps native overlays.
- **Graphs start from `ApexChartTheming.BaseOptions` + its `WrapperClass`** on the container: frozen
  canvas, display face, palette fore color, whisper-grid, dark theme, themed tooltips. Charts layer
  their specifics (strokes, fills, axes) on top — never rebuild the base by hand.
- **Community-scoped feeds reuse the shipped vocabulary**: the Community Highlights widget renders each
  big win with the Discord card's own caption emoji (👑 pumbility, 📊 peers, 🆕 folder, 🏅 title, 💎 rare
  PG) and colors the "% have it" rarity through the rarity ramp — the on-site feed and the Discord cards
  read as one system. Persisted win data is structured, never pre-rendered text: the row localizes every
  caption (a UI string never rides the DB payload).

## 5. Challenge boards (Weekly Charts + Daily Step)

The `/WeeklyCharts` challenges hub ([design](design/weekly-charts-overhaul.md)) is the first page
rebuilt as **static SSR + one island**, and it adds a small vocabulary of its own:

- **The page is a static region** (static-shell.md rules): `--mix-*` and semantic tokens only, no
  `--mud-*`; every number is printed alongside its color; no row depends on a Mud popover. The one
  interactive root is `ChallengeDialogHost` — Record, the shared board and chart-details dialogs, and
  the admin rotate — reached from static `data-challenge-*` controls through `challenge-board.js`.
- **State that changes *what data* you see is URL state**, so it is shareable and crawlable: the week
  (`?week=`), the monthly type (`?type=`), the suggested filter (`?suggested=all`), the pool
  (`?pool=1`). Presentation travels separately — density via `Density__WeeklyCharts` (rule 5), swapped
  in JS and persisted through `POST /Preferences/Set`.
- **Chart identity opens `ChartDetailsDialog` from everywhere** — every density and the Daily strip —
  but the jacket/name stay real `/Chart/{id}` anchors so a crawler follows the internal-link mesh; the
  island upgrades the click.
- **The trust ladder** (✔ imported · 📷 photo proof · blank) is the shared board vocabulary — see the
  `LeaderboardDialog` note in §4. It pairs an icon with a printed score, never color alone (rule 8).
- **Manual competitive entries are score + plate, a pass** — no broken, no plated-broken (those are
  personal-recording concerns). Photos are optional proof, not a gate; the enforcement lever for
  suspected cheaters is stated in the Record dialog, not yet built.

## 6. Ladder rails (Titles)

The `/Titles` overhaul ([design](design/titles-overhaul.md)) adds one piece of vocabulary, and it
generalizes to any progression the game already models as a ladder:

- **A ladder is one rail, not N rows.** Where a collection's items form a progression, the page
  draws the progression — one row per ladder, one pip per rung — rather than one row per item.
  213 titles become 47 rails. The rung carries its own state; there is no continuous fill line
  behind the pips, because ladders legitimately have holes (Expert Lv.9 is reachable without
  Lv.1).
- **A rung's fill measures from the rung below it**, never from zero, matching `CompletionFloor`.
- **Filtering fades, it does not remove.** A rail keeps its whole ladder when filtered; the rungs
  that do not match drop to 16% opacity in place. A ladder with holes punched through it stops
  reading as a ladder, and where your earned rungs sit on the climb is the thing worth seeing.
- **What we cannot compute says so.** A dashed edge plus an `official` tag marks anything whose
  progress only the official piugame import knows, and such an item never renders a partial bar —
  a 0% bar against a requirement that does not exist is a lie, not an empty state. The drawer
  explains it in words.
- **Rarity of a thing is the percentile of people who lack it**, so it rides the shipped rarity
  ramp (`ThemeScales.BandFor(1 - share)`) instead of a second inverted set of cutoffs, and the
  percentage always prints beside the colour (rule 8).

## 7. Maker-facing surfaces (Community Tools)

`/Developers` and its console and debug pages are the site's first screens whose audience is not a
player ([design](design/api-v2-community-tools.md)). Two rules follow, and they bind anything
maker-facing that comes after.

- **A maker-facing surface may use maker vocabulary; player-facing copy never says "webhook."**
  On `/Developers` the word is correct, precise and shorter than any paraphrase. On
  `/CommunityTools` — the directory a player browses — the same mechanism is "this tool gets your
  scores when you import." Vocabulary follows the reader, not the implementation. The one place
  this bites is the session-mode warning, which is player-facing and must describe the *consequence*
  ("it can act as you on piugame.com, including deleting your account there"), never the mechanism.
- **A secret is shown once, and the screen says so before it is generated.** The reveal panel
  carries the copy button, the warning that this is the only time, and no way back to it. Blurring
  a value that is still in the DOM, or a "reveal" toggle on a stored secret, is worse than useless:
  it implies the secret is retrievable and teaches makers not to save it. What is stored is a hash,
  the UI shows a four-character suffix so a maker can tell two keys apart, and losing a key means
  minting a new one.

Two existing rules do most of the rest of the work here. Rule 8 (a colour never carries meaning
alone) is why a delivery's status is a word next to its chip. Rule 4 (say what you cannot compute)
is why a delivery whose body has aged out of the 7-day window renders **"Body expired"** rather
than a disabled button with no explanation.
