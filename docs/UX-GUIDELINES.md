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
   - Grade tokens are deliberately deferred: grades render as images today, and Phoenix 2 ships a different grade set (art pending). They land with their first text-rendered consumer.

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

Percentile semantics are the established `ScoreRankingRecord.Ranking` convention: fraction of the comparable population at or below you, 1.0 = first place. Because gold is inherently brighter than sapphire, hue luminance can't order the bands — the **glow treatment ramp** (`.rarity-glow-1..3`) does, and the percentile number always renders alongside the color.

**Difficulty** (how hard is this chart relative to its level — tier lists). The familiar green→red heat, `TierListCategory` → `--diff-*`. Red is at home here exactly because the rarity ramp refuses it.

---

## 2. The rules

**1. The answer lives above the fold.** Every page has one job; the answer to that job is visible without scrolling at 390×844 (a phone) and instantly at desktop. Filters, explanations, methodology, and history come *after* the answer, never before.

**2. Show, don't tell — the jacket is the identifier.** Players recognize song art faster than song names. Encode with the game's own vocabulary first: jacket art, red Single / green Double bubbles, grade art, plate metals, the two ramps. Text labels are the fallback; tooltips are the footnote. If a number can be a bar, bubble, or color, it is one — with the number still present (see rule 8).

**3. One concept, one component.** Difficulty is always `DifficultyBubble`; a grade is always `LetterGradeIcon`; a score is always `ScoreBreakdown`; a player is always `UserLabel`. A new visual concept means a new shared component in `Components/` — never a page-local restyle of an existing one.

**4. No raw color literals.** ⚙ *Ratcheted by `UiColorTokenTests`.* UI code under `Pages/`, `Components/`, and `Shared/` reads theme tokens, not hex strings or `Colors.*` constants. The allowlist is launch-day debt and only shrinks; each page overhaul burns down its own entries. (Exception by design: the Phoenix Recap deck is self-styled slide art and stays allowlisted.)

**5. Density is a setting, not a redesign.** Three sanctioned densities — **Comfortable** (cards), **Compact** (the jacket "sticker sheet"), **Table** (rows) — stored per user **per page** in UiSettings under `Density__<Page>` (e.g. `Density__TierLists`): players use different densities for different tasks, so the choice travels with the page, not the site. A collection page picks its default and honors the stored choice; it never invents a fourth mode. (Landed with the tier-list overhaul; the previously reserved `Universal__Density` key was retired unshipped.)

**6. Filters are furniture.** The filter entry point and its active-filter chips (labeled, removable) sit in a content bar directly above the list they affect; the full panel lives in a drawer; at phone widths the bottom action bar keeps filters thumb-reachable. Filters never push the answer below the fold. The sticky toolbar is reserved for controls that change *what data* you're looking at — presentation controls (density, download, filters) travel with the content instead. (Amended in the tier-list overhaul field test; previously the filter row itself was sticky.)

**7. Design for +40% text.** Eight-plus locales; Portuguese and French run long, CJK runs dense. Every string goes through `L[…]`, new keys land in **every** locale in the same pass (glossaries: `LOCALIZATION-<locale>.md`), no fixed-width labels, no truncation without a tooltip. **Universal terms never translate**: in-game memes ("Why Don't You Get Up and Dance, Man?") and community proper nouns (Chabala, PIU Center, PG) keep their original value in every locale.

**8. Color is never the only channel.** Every color encoding pairs with a second signal: the rarity ramp's monotonic glow + the printed percentile, pass/fail borders + icons, S/D bubbles + the S/D numeral. Verify new encodings under a colorblind simulator before shipping.

**9. Loading looks like the layout.** Skeletons match the shape of the content they become — never a lone centered spinner on a data page. Empty states name the action that fills them ("Import your scores to light this up"), not just the absence.

**10. Thumbs first on mobile.** Primary navigation and primary actions live in the bottom third at phone widths (the bottom nav is this rule applied to the shell). The top corners are for identity and context, not workflows.

---

## 3. Enforcement

- **`UiColorTokenTests`** (ArchitectureTests) scans `Pages/`, `Components/`, `Shared/` for hex literals and `Colors.*` constants against a shrink-only allowlist. Exceeding an allowance fails; dropping *below* one also fails until you lower the entry — that's the ratchet.
- Adding a color: if it's brand, it belongs in a `MixPalette`; if it carries data meaning, it belongs in a semantic token group with a `ThemeScales` accessor. If it's neither, question it.
- The remaining rules are review discipline today. Candidates for future ratchets: `L[…]` coverage scanning, skeleton-presence checks on data pages.

## 4. Home dashboard widgets

The widget home page ([design doc](design/home-page-widgets.md)) adds a vocabulary with its own rules:

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
- Chart series colors come from the `MixPalette` chart pair (`--chart-singles`/`--chart-doubles`,
  or `PaletteFor(mix).ChartSingles` for render targets that can't read CSS vars). Era/mix distinction
  rides line *style* (dashed), never a third hue.
- **Chart rows/cards in widgets open `ChartDetailsDialog` on click** (browse mode only — edit mode
  owns clicks for arranging). Every catalog widget inherits this rule.
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
