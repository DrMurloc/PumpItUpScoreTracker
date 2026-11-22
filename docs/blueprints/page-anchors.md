# Page anchors - blueprint

> A blueprint is a feature proven in Astrid, described for building here. It is not a spec: on the branch that
> builds the feature, its draft requirements become the feature's spec (docs/SPECS.md), and that PR deletes this file.
>
> This one is a code convention rather than a feature a player notices, so it carries no draft requirements. When it
> is first built, its rules move into [CLAUDE.md](../../CLAUDE.md)'s UI conventions and
> [UX-GUIDELINES.md](../UX-GUIDELINES.md), and that PR deletes this file. The help assistant
> ([help-assistant.md](help-assistant.md)) and the page intro ([page-intro.md](page-intro.md)) both build on it and
> cite it by section.

## 1. What it is, for PIU

A page names its controls and regions in its own Razor markup with three HTML data attributes, so code can point at a
control without knowing its CSS, its position or its label in any of the nine locales. A page intro rings a region by
its word, the help box tells AI which controls a page has by their words, and Show me rings the control a step names.

A player never sees an anchor. What they notice is that a ring lands on the right button after a redesign, in Korean as
well as English, because the word moved with the button in the same diff.

PIU has no anchors today: no Razor file carries any of the three attributes.

## 2. The three attributes

### 2.1 `data-tour="<word>"` - the anchor word

On a control a player operates, or on a region a page intro lights. The word is the control's address.

- **An element may carry several words,** separated by spaces, as a region that is also a control does
  (`data-tour="import-panel import-start"`). Code reads the attribute as a list.
- **Several elements may share a word** to make up one region, as a heading and its card do. Code rings what they cover
  together.
- **The word is written in the markup beside the control,** never in a registry, a manifest or a CSS class. Changing the
  control changes its address in the same diff.

Astrid uses the name `data-tour` because its guided tours came first; PIU keeps it so the tour, intro and help code port
without a rename.

### 2.2 `data-decision` - the decision mark

On a control whose press records a decision a player answers for. It takes no value. Show me stops at a marked control
with "Your call", says what to check and what each choice does, and never asks for the press
([help-assistant.md](help-assistant.md), HELP-017); a lesson ends at its first mark (section 7.4).

In PIU a decision is a press that:

| Kind | Examples |
|---|---|
| deletes or merges a player's data | Delete my data, Undo, merging two accounts |
| changes what others see of the player | profile visibility, a community membership, a rival invite |
| posts or publishes in the player's name | a chart comment, a tournament session submitted for approval |
| stores a credential | Remember my password on this device |
| grants or revokes access | a community role, a tool's API key, a ban |

A press that only reads, filters or starts the player's own import is not a decision: Show me rings it like any step.

Review catches a missing mark. Whether a press is accountable is a judgment, so no check infers it.

### 2.3 `data-tip="<word>"` - the tip dot

On the small dot a page intro leaves beside a stop once the intro ends, so a player can hover or focus it to read the
stop again. Its value is the anchor word of the stop it sits beside, never a word of its own; script places the dot at
that anchor's top-right corner, so it scrolls with the page. Page markup never writes `data-tip`: only the intro host
draws the dots, from its stops ([page-intro.md](page-intro.md)).

## 3. Naming anchor words

1. **Lower-case letters and single hyphens only,** in English whatever the locale: `import-start`, `tierlist-ranked-by`.
   An attribute is never localized and never goes through `L[...]`.
2. **Say what the control does or what the region holds,** never where it sits or how it looks: `import-deep-scan`, not
   `second-button` or `blue-chip`.
3. **Start with the feature's word,** the page or the shared component it lives in: `import-`, `pumbility-`,
   `tierlist-`, `chart-dialog-`. It keeps words unique without a lookup, and it tells a reader of a lesson which page a
   step is on.
4. **One Razor file declares each word, across the whole Web host.** A word always means one control.
5. **A shared component declares its word once,** in its own file, and carries it to every page that renders it. A page
   never re-declares a word its component already has.
6. **A control repeated on a page shares one word,** as each row's chart title does. Code rings the first one showing,
   or the item a step names by the name the row shows.
7. **A computed word comes from a fixed list in code,** never from data. A component that writes
   `data-tour="@TabWord(tab)"` keeps `internal static IReadOnlyList<string> ComputedAnchorWords` beside the code that
   computes it, holding every word it can produce. A chart id, a song title, a player's name or a community's slug is
   never part of a word.
8. **Renaming a word is a change to everything that names it.** The lesson and intro checks fail on a step whose word no
   file declares, so a rename and its steps move together.

## 4. What counts as a control, and its label

Port Astrid's reading as it stands (MudBlazor 8 is the same library):

| Kind | Razor that makes it |
|---|---|
| Button | `MudButton`, `MudIconButton`, `MudFab`, `MudToggleIconButton` with no `Href`; `button`; `MudExpansionPanel`'s header |
| Menu, menu item | `MudMenu`; `MudMenuItem` |
| Tab | `MudTabPanel`, its word on a label inside its `TabContent` (a word on the panel itself lands on the panel's body, not its header) |
| Field | `MudTextField`, `MudNumericField`, the pickers, `MudCheckBox`, `MudSwitch`, `MudRadioGroup`, `MudToggleGroup`, `MudSlider`, `MudTablePager`, `input` other than hidden, `textarea` |
| Select | `MudSelect`, `MudAutocomplete`, `select` |
| Link | those buttons with an `Href`, `a` with an `href`, `MudLink`, `MudNavLink`, `NavLink` |
| Click handler | any other element with `@onclick`; any other Mud component with `OnClick`, `OnClose` or `CloseIconClicked`; `MudTable` with `OnRowClick` |

A select's items, tooltips, chips without a click, our own components' event parameters, and pointer, focus and key
handlers are not controls. A PIU component that wraps controls, such as `RangeSlider` or `ArmedActionSelector`, has its
controls read in its own file.

**One PIU change: a label written as `@L["..."]` reads as its resource key.** Astrid's reader keeps a control's literal
text and drops every Razor expression, which would leave almost every PIU control with no label, since PIU writes every
string through `L[...]`. So the reader takes a lone `@L["Import"]` as the key `Import`, and the map keeps the key. Most
PIU keys are their own English copy ([CLAUDE.md](../../CLAUDE.md), Localization); a few are identifiers, such as
`Import Lede`, so whoever shows a label resolves the key through the resources: the failing check in section 7 through
`en-US` to suggest a word, and the help box through the player's own locale before AI sees it
([help-assistant.md](help-assistant.md), How it lands in PIU). The reader stays pure: it never opens a resource file.

## 5. The anchor map

The anchor map lists every word the Web host's Razor files declare: the word, the file that declares it, the kind and
label of the first control carrying it (none for a region), whether it carries the decision mark, and whether a list's
items share it. Astrid's `Web/Anchors/` folder holds it in four small types (`AnchorReader`, `AnchorMap`,
`AnchorWord`, `RazorControl`).

### 5.1 Read from the Razor source

The reader is pure functions over one file's text: the same text always reads the same, from disk or from the build. It
is a heuristic, not a parser. It walks tags by hand, never reads Razor or HTML comments or `@code` blocks, skips C#
expressions with their strings intact, and knows a loop body or a row template, so it can tell a list's repeated rows.
Its own tests pin what it reads, on synthetic Razor.

**At test time,** the checks in section 7 read every `.razor` file under the Web project through the repo's
`SourceTree` walk, so a worktree under `.claude/worktrees/` scans the same as the main checkout.

### 5.2 Read from the build

A feature that needs the map at run time - the help box - reads it once at startup from the Razor files the Web build
embeds as resources, by the same pure functions. Nothing is generated and nothing is committed: no manifest to merge.
A page intro needs only the test-time read.

### 5.3 What the browser may report

Script may report which words are on the page and showing and usable, and nothing else: never a word the page shows,
such as a player's name, a chart's comment or a community's description. Text a page renders is player-written or comes
from the official PIU site, so it never leaves the browser through an anchor ([help-assistant.md](help-assistant.md),
HELP-007).

## 6. How it lands in PIU

- **Where it lives.** The reader and the map are Web code, in a new `ScoreTracker/ScoreTracker/Anchors/` folder, as they
  read the Web host's own markup. `CODE-MAP.md` gets the folder in the same PR.
- **Where its checks live.** `ScoreTracker.Tests` must not reference Web, and the checks call Web types (the map, the
  computed-word lists, and later the lessons and intros). They go in a new `ScoreTracker.Tests.Components/ArchitectureTests/`
  folder: ratchets, in the fast suite, no Docker. CLAUDE.md's test table gets the row in the same PR. Section 10 asks the
  owner to confirm.
- **Nothing in the database, nothing in `UiSettings`.** Anchors are markup.
- **The nine locales are untouched.** Words are English and never pass through resources.

## 7. The checks that hold it

Each lands with zero violations, as CLAUDE.md requires of a new ratchet. Astrid's "every control has an anchor" ratchet
landed with a shrink-only inventory of 298 unanchored controls across 85 files; PIU's rule forbids such a list, so PIU's
version covers only files that have opted in, which today is none.

| # | Check | Fails when | Lands at zero because |
|---|---|---|---|
| 7.1 | A file that carries a word anchors every control | a Razor file declares at least one `data-tour` word and has a control with none. The failure names the line and suggests a word from the label (`add data-tour="import-start"`) | no file carries a word yet; a file opts in when its page gets an intro or help |
| 7.2 | One file per word | a word is declared in two Razor files. A permanent exemption, with its reason, lives in its own set | no words yet |
| 7.3 | A computed word is listed | a file computes a word in markup and its component has no `ComputedAnchorWords`, or computes one the list lacks | no words yet |
| 7.4 | A step's word is declared, and a lesson ends at its first mark | an intro stop or a lesson step names a word no file declares; a lesson goes on past a step whose control carries `data-decision` | no steps yet; held by the lesson and intro checks in the two features |
| 7.5 | A tip names a word | a `data-tip` value is no declared word | the intro host is the only writer |

Each check also carries a reach fact, as PIU's other source-reading ratchets do: a synthetic file proves the scan still
catches what it guards, and once the first page is annotated, a floor on the words read proves the scan still reaches
the tree.

A missing decision mark is held by review alone (section 2.2).

## 8. What annotating costs

**A page is annotated when it first gets an intro or help, never all at once.** Annotating means a word on each of its
controls - its own file and each component on its main jobs' path - and a decision mark where one applies. Check 7.1
then holds every control that file gains afterwards, with a suggested word in the failure, so the cost after day one is
one attribute per new control.

For scale, on 2026-11-22 PIU had about 300 Razor files and roughly 1,150 control tags by a plain tag count (the reader's
count will differ). Astrid annotated 79 words across 30 files before its first help pages shipped, out of 355 controls
in 97 files; a PIU page is bigger:

| Page | Its own file's control tags |
|---|---|
| `/UploadPhoenixScores` | 43 |
| the home Import widget and its config panel | 19 |
| `/Setup` | 9 |
| `/Charts` | 54 |
| the home dashboard | 52 |
| `/ChartRandomizer` | 114 |

The PUMBILITY pages draw everything through shared components, so annotating them is mostly annotating those components
once.

The work is mechanical, and the failing check writes most of it: it lists each control with the word its label makes.
The judgment is in the decision marks and in naming the region words an intro lights.

## 9. Build order

1. The reader and its pinning tests on synthetic Razor, the `@L["..."]` label rule among them.
2. The map, and checks 7.1 to 7.3 with their reach facts, landing at zero.
3. The first page's annotation, in the same PR as the first intro or the first help lessons that need it, never alone.
4. The embedded-Razor read at startup (5.2), with the help box's first slice.
5. Checks 7.4 and 7.5 with the lessons and intros that give them something to hold.

## 10. Open decisions for the owner

1. **Where the anchor and lesson ratchets live.** Recommended: a new `ScoreTracker.Tests.Components/ArchitectureTests/`
   folder, because `ScoreTracker.Tests` must not reference Web and the checks read Web types. The alternative is to
   compile the reader's file into `ScoreTracker.Tests` as a linked source, which keeps every ratchet in one project but
   cannot reach the lessons and intros.
2. **The feature-word prefix (rule 3.3).** Recommended as a rule, not advice: it makes uniqueness something a person can
   see in a diff, and every step reads with its page.

## 11. Not ported

- **Astrid's "every control has an anchor" inventory.** A shrink-only list of offenders is what PIU's land-at-zero rule
  forbids; the opt-in file rule (7.1) replaces it.
- **"Every anchored file is under a spec's `paths`."** PIU specs carry no `paths` ([SPECS.md](../SPECS.md), Paths). A
  word reaches its spec through the route its page is on, and the route through the spec's `claims.routes`
  ([help-assistant.md](help-assistant.md), How it lands in PIU).
- **A new `data-anchor` attribute name.** Astrid kept `data-tour` to avoid churning its tour engine; PIU has no engine
  yet, but keeping the name keeps the ported scripts and tests word for word.
