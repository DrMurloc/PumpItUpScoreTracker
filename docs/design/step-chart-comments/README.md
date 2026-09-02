# Step chart comments

Status: **owner-workshopped 2026-09-01 (two rounds) and built as PR #309; round 3 (2026-09-02) folds in the field-test redlines, D13–D17.** A comment may point at
one second of the step chart. The strip draws a mark there, a sticky panel at the bottom of the
strip reads and writes the comment on every host, and the Comments tab wears a time chip that
jumps back to the strip.

Companion specs: [chart-comments/README.md](../chart-comments/README.md) — the comment system
this rides on (audiences, notes, votes, moderation, translation), whose rules are all unchanged
here — and [step-chart-failure-map.md](../step-chart-failure-map.md), the strip itself, whose
rail, minimap and layout rules this extends. **[mock.html](mock.html) is the UI reference** (round
3): desktop page section, phone reading and writing, both dialog tabs, at true widths and on the
real Phoenix tokens.

> **Part 2 of this document is scaffolding and gets deleted when the feature ships.** Part 1, the
> requirements, survives. Anything in Part 2 that turns out to be a durable architectural fact
> belongs in `ARCHITECTURE.md` or `CLAUDE.md` instead.

---

# Part 1 — Feature requirements

## 1. The thesis

**An anchor is an attribute of a comment, not a new kind of comment.** A comment gains one
optional number — `AnchorAt`, chart seconds, the same clock the failure rail's pins use. Nothing
else about a comment changes: the audience rail, personal notes, votes, replies, moderation,
translation, the 500-character plain-text body and the purge all keep working as shipped, because
the row is the same row. The step chart is a *lens* over the comments that carry a second; the
Comments tab shows every comment and marks the anchored ones with a chip that jumps to the strip.

## 2. Decisions

All owner-ruled across the two rounds of 2026-09-01.

- **D1 — Times only.** A comment points at one second. Sections, ranges, stripes and any
  range-selection gesture were in round 1 and are gone: "it kind of muddies up the UX for low
  gain." Two comments on the same second share one bubble with a count.
- **D2 — Double-click places.** Double-click on desktop, double-tap on touch, on the arrow row you
  mean. No toolbar button, no "click Comment, then click the chart" two-step. The strip's scroll
  box takes `touch-action: manipulation`, which keeps pan and pinch and removes double-tap zoom.
- **D3 — Snap to the nearest arrow row** within ~0.12 s of the pick, so two people who mean the
  same quad land on the same second and stack; outside that reach, the exact second. The snap is
  the client's: the module has the rows, the vertical never reads a step payload, and the server
  stores what it is sent. A stack is exact equality of what two clients snapped to from the same
  payload.
- **D4 — One sticky panel, every host.** The reading and writing surface is a panel pinned to the
  bottom of the strip's frame on the chart page, on a phone and in the dialog's Steps tab — one
  component. There is no comment column beside the strip. The strip scrolls under the panel and
  gets that much extra runway at its end.
- **D5 — The panel follows the strip.** It shows the comment nearest the middle of the viewport;
  ‹ › walk the chart's comments in order and scroll the strip along; tapping a mark centres it.
  One rule for scroll, tap and stepper alike. After a stepper press or a mark tap the panel holds
  that comment until the next wheel or touch scroll.
- **D6 — Open thread goes to the Comments tab.** In the dialog that is a tab switch with the
  comment focused (the `FocusCommentId` path the moderation queue uses). On the chart page, which
  has no comments of its own, it opens the chart details dialog on the Comments tab, focused — the
  `/WeeklyCharts` pattern of a static page opening dialogs, zero new surfaces. A Comments section
  on the page stays a separate, later slice.
- **D7 — Your notes overlay every scope,** in gold, the way your own runs mark the rail in every
  view. The vertical merges them: the marks read for an audience is that audience's anchored roots
  plus the viewer's own anchored notes, never anybody else's.
- **D8 — Marks on the right, inboard of the scrollbar.** Comment marks are a third column on the
  annotation rail beside the death pins on wide layouts, sitting inside the rail's width rather
  than at its edge. On narrow layouts — where the pins already moved into the time gutter and the
  rail's width went back to the arrows — the marks get a slim lane past the strip's edge that
  stops 10 px short of the box edge, where a phone's overlay scrollbar draws. Nothing sits under
  the thumb.
- **D9 — No counts in the legend.** Neither "Comments · 7" nor the counts the legend already
  carried ("Life Bar Break · 22", "Walk off · 1", "Your runs · 1"): "it muddies up the view, I never
  noticed them." Every legend entry is a label. The Unplaced entry, whose count was its only
  content, goes with them.
- **D10 — Both hosts in one slice.** The page section and the dialog tab land together; the panel
  is one component and the module is one file.
- **D11 — Replies inherit; edits keep; notes may carry.** A reply stores no second of its own and
  reads its root's. An edit keeps the second in this slice: the edit composer has no strip to move
  it on. A personal note may carry a second and renders gold.
- **D12 — Cross-mix, like the comment.** One `ChartId` spans mixes and the steps do not change.
  A comment posted from the Phoenix 2 strip stores that strip's row second and sits at the same
  second on Phoenix 1; a re-step drifts it the way it already drifts the words. No validation
  against the chart's length crosses the vertical boundary: the aggregate's rule is zero to an
  hour, and ChartComments keeps knowing nothing about charts.

Field-test redlines on PR #309 (owner, 2026-09-02):

- **D13 — The gesture line and the scope chip live in a bar under the section chips.** The
  panel's head had carried the scope menu and a ＋, and an empty scope has no head — which
  trapped the reader in it. The bar is always there: a muted line naming the gesture on the
  left ("Double-tap a spot to leave a comment", in the pointer's or the finger's words), a
  dense chip on the right that opens the same scope list the tab's rail offers, with a ✓ on the
  current one. The ＋ is gone: the gesture is the way in. Signed out, the bar does not render.
- **D14 — The composer's ✕ is the panel's top-right corner**, where the ＋ used to be — the
  spot the eye had learned. It replaces the foot's Cancel: one way out.
- **D15 — The composer's head is the time chip, the scope chip and the ✕.** "Posting to
  Public as ERRLENA" overflowed a phone's panel and is retired on this surface: the scope chip
  says the scope, a signed-in reader knows who they are, and switching the chip while writing
  changes where the comment goes — the bar's chip and the composer's are one setting, so the
  strip follows too. Notes turns the chip gold, the placeholder into "Note on 0:33…" and the
  button into Save note. The Comments tab keeps its sentence; it has the width.
- **D16 — The panel spans the strip and the whole-chart map.** Strip-wide it read thin on every
  screen. The module lays it over the frame's bottom edge from the strip box's left to the
  map's right, on every host; the last of the map sits under it, and that is accepted.
- **D17 — The empty panel is one line** — "Nothing here yet." — since the gesture moved to the
  bar. The panel is no longer inside the viewer: the component renders the bar in flow under
  the chips and the panel positioned against the strip's root, which the module lays out.

## 3. The vocabulary

| Mark | Reads |
|---|---|
| **Spot** | A speech bubble on the annotation rail at the comment's second, with a tick into the strip so the eye lands on the arrows it means. Ink at rest. |
| **Stack ×N** | Comments on the same second share one bubble with a count — the pin rule, reused. The panel pages through them. |
| **Your note** | Gold and hollow, like your runs on the rail. Only you see it, whatever scope you are reading. |
| **Selected** | The bubble turns primary and a hairline crosses the strip at its second: "this row, this comment". |
| **Composing** | A hollow + bubble and a primary hairline mark the second being written about, until post or cancel. |
| **On the map** | Every comment is a dot on the whole-chart map's left edge; deaths keep the right edge. |
| **Time chip** | `0:33`, on a comment row in the Comments tab and as the panel's title. Same chrome as a section chip, with a short bar where the section chip has a dot. |

The bar, under the section chips (D13): the gesture line on the left, the scope chip on the
right. The panel, three states:

- **Browse** — ‹ › steppers, the time chip, the stack pager (`1/2`) when there is one, author and
  age, two lines of the body (tap to unfold), ▲ vote, reply count, *Open thread ›*.
- **Compose** — the time chip, the scope chip (live), the ✕ top-right (D14/D15); the one-line
  composer that grows; the counter near the cap and Comment (Save note on Notes). The rules card
  and the consent flow render in place exactly as the tab's do. Signed out: the panel is
  read-only and the compose gesture yields a *Sign in to comment on 0:33* line instead.
- **Empty** — "Nothing here yet." (D17).

## 4. Deliberately out of scope

- **Ranges and sections** (D1). If a span ever earns its way back it is additive — a second
  column — and the mock's round 1 recorded what it would look like.
- **Moving a second on edit** (D11). Additive: an `AnchorAt` on the edit command and a strip in the
  edit composer.
- **A Comments section on the chart page** (D6). The panel is the page's first comment surface;
  the whole thread stays in the dialog for now.
- **Community scopes on the strip beyond the menu**: the strip shows the chosen scope plus your
  notes. A community comment's chip in the tab still switches to Steps and selects its second, and
  the module draws a transient hairline there even when the mark is not in the loaded scope.
- **API exposure.** Comments are not on `api/v1` or `api/v2`, anchored or not.

---
---

# Part 2 — Technical scope

> **Delete this entire part when the feature ships.**

## 5. Verticals

**Two touched: ChartComments and Web.** ChartComments owns everything comment-shaped, including
the anchor. Web owns the strip module, the panel and the two hosts. `ScoreTracker.Data` receives
only the migration file. Catalog, ScoreLedger, Translations, Communities, Application and the API
surface are untouched.

## 6. Layers

| Layer | Change |
|---|---|
| **Domain** (ChartComments) | `Comment` gains a nullable `AnchorAt` (decimal chart seconds), validated in the aggregate: zero to 3,600, else `CommentNotAllowedException`. `Comment.Post` takes it; `Comment.Reply` never does (a reply reads its root's); `Edit` leaves it; a note may carry one. `CommentState` carries it for rehydration. |
| **Contracts** (ChartComments) | `PostCommentCommand` gains an optional `AnchorAt` (additive; existing callers unchanged). `CommentRecord` gains `AnchorAt` (root only, null on replies and stubs). New query `GetChartCommentMarksQuery(ChartId, Audience, ReaderLocale, PreferredLocale)` → `IReadOnlyList<CommentRecord>`: anchored, undeleted roots for the scope **plus the viewer's own anchored notes**, ascending by second, unpaged, replies attached, through the same translation display resolution `GetChartCommentsQuery` applies. |
| **Application** (ChartComments) | The post handler passes the anchor through; the marks handler is a read that reuses the page handler's projection. Sagas, moderation, translation, purge, archive unchanged. |
| **Infrastructure** (ChartComments + Data) | `AnchorAt decimal(9,3) NULL` on `CommentEntity` and on `CommentArchiveEntity` (a deleted club's comments keep their seconds); `CommentRow` carries it; the repository gains `GetAnchoredForChart`, which is the `Visible` gate plus `AnchorAt IS NOT NULL` plus the viewer's own notes. Migration `AddChartCommentAnchor`, scaffolded from Data with the CompositionRoot startup project. No new index: rows per chart are few and the `(ChartId, Audience, CommunityId)` index already serves. |
| **Presentation — the module** (`wwwroot/js/step-chart.js`) | Draws marks (bubble, stack count, tick), the selected and composing hairlines, and map dots from a list the panel hands it. Double-click, and a hand-rolled double-tap for touch, snap to the nearest row and raise a pick. The follow rule (nearest to the viewport middle, rAF-throttled, held after a stepper or a tap until the next wheel/touchmove). The narrow marks lane. Interop surface: `bindPanel(panelElement, dotNetRef)` (finds the strip root by `closest('[data-stepchart]')`, waits for the mount), `setMarks`, `selectMark`, `setPick`, `clearPick`, `scrollToSecond`; events to .NET: `OnPick(t)`, `OnFollow(id)`, `OnSelect(id)`. Legend entries lose their counts (D9). No new tokens: marks read `--mix-ink`, `--mix-primary`, `--step-you`. |
| **Presentation — the panel** | `StepChartCommentPanel` (Web `Components/ChartComments/`), one component for all hosts: the bar in flow under the chips (gesture line + scope chip, D13) and the panel the module positions over the frame (D16). Owns the data (dispatches the marks query), the three states, the scope (one setting for bar and composer, D15), the composer (reusing `CommentComposer` without its Cancel, `CommentRulesCard`, `LinkInterstitial`, `CommentTextView`), the vote, and the interop binding. Gated exactly as the tab: `IsAdmin || ChartComments:Enabled`. A refused post shows the domain sentence in the snackbar. |
| **Presentation — hosts** | Page: the component is an island between the static strip's chips and its viewer (the record panel's precedent for an island in static markup), rendered only when the gate allows; *Open thread* opens `ChartDetailsDialog` with `InitialTab = Comments` and `FocusCommentId`. Dialog: the panel is a child of `ChartStepsTab`; *Open in Comments* raises to the dialog, which switches tabs and focuses. Comments tab: `CommentRow` renders the time chip on anchored roots; its click raises to the dialog, which switches to Steps and asks the tab to select the second once the module has mounted. |
| **Docs** | This document, the schema rows, the ChartComments sentence in ARCHITECTURE, the chart-comments README's scope bullet. |

## 7. Ratchets that bite if forgotten

| Miss | Consequence |
|---|---|
| `[ExcludeFromCodeCoverage]` on the new query record | Coverage noise on a record |
| The new query in `Contracts/Queries/`, named `*Query`, implementing `IQuery<T>` | `MessageTaxonomyTests` |
| The notes merge in the **repository**, and a decoy-account row in `ChartCommentAudienceTests` | A mocked handler cannot catch a stranger's anchored note leaking into another reader's marks |
| `AnchorAt` on the archive entity and in `ArchiveCommunity` | A club's deletion silently drops every second |
| Resx keys in all nine locales, alphabetical, Murloc alphabet, no case collisions | `LocalizationKeyTests`, `ResxKeysAreStoredAlphabetically`, `MurlocValuesUseOnlyTheMurlocAlphabet` |
| No hex or `Colors.*` in the panel | `UiColorTokenTests` |
| A refused post rendered as the domain sentence, never the exception | `DiagnosticExposureTests` |
| bUnit events awaited (`ClickAsync`) | `BunitEventDispatchTests`, and the intermittent pre-event assertion it exists to stop |

## 8. Build order

Docs first, locales last, one PR, every commit green on the fast suites:

1. `docs(design)` — this document, the mock, the schema rows, the pointers
2. `feat(comments)` — `AnchorAt` on the aggregate + tests
3. `feat(comments)` — the entity columns + migration `AddChartCommentAnchor` + round trip
4. `feat(comments)` — the marks read: contracts, handler, repository, decoy leak test
5. `fix(web)` — the legend drops its counts (D9)
6. `feat(web)` — the module: marks, double-click, snap, follow, interop, the narrow lane
7. `feat(web)` — `StepChartCommentPanel`
8. `feat(web)` — the two hosts and the handoffs
9. `feat(web)` — the time chip in the Comments tab
10. `i18n` — nine locales

## 9. Post-deploy

Nothing. The migration rides the pipeline's bundle; the comments flag is unchanged, so until it
flips the marks and the panel are the site admin's alone, exactly like the tab.
