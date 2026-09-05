# Folder Level Progression

**Status: built.** Supersedes the 2026-07-10 workshop capture. Decisions below are owner-locked
unless marked open; §7.7 lists the commits that landed them.

**Post-deploy:** press **Backfill Folder Levels** on the admin dashboard once. Until it runs,
folders only appear for players who have imported since the deploy (§7.6).

A folder carries **two numbers**: how much of it you've completed, and the grade you hold across that
much of it. Together they replace Phoenix 1's Paragon levels, which have no Phoenix 2 equivalent.

```
S22 · 92% complete · A+
```

Read it as one sentence: *A+ or better on 80% of S22.*

---

## 1. Problem

Phoenix 1's per-folder display leans on level-based titles (Intermediate/Advanced/Expert Lv.X).
Phoenix 2's 272-title set is not level-based, so the tier-list page's folder progress strip has no P2
successor. Two properties made the old system unsatisfying even in P1:

- **It regressed on content.** Folders gain charts constantly. Measured on real Phoenix 1 data,
  folders grew **45–130% during the mix**: S20 78→135, S22 47→97, S23 29→56, S24 10→23.
- **It only existed for players who had already completed a title.** Paragon is a modifier on a
  finished title, so most players never saw one.

### 1.1 The one mistake to avoid

The tempting design is a single number: `sum(every chart in the folder) / folderSize`, with unplayed
counting as zero, resolved to a letter grade. **It does not work, and it must not be re-proposed.**
It was tested against DrMurloc's real Phoenix 1 singles and failed on two counts.

**It reads coverage, not skill.** The formula factors to `completion × averageOfPlayed`. Across his
folders `averageOfPlayed` spans 865k–996k (1.15×) while completion spans 22%–100% (4.5×), so
completion dominates and the letter is a completion readout in disguise:

| | S13 | S15 | S17 | S19 | S20 | S22 | S23 | S24 |
|---|---|---|---|---|---|---|---|---|
| completion | 22% | 40% | 97% | **66%** | 95% | 93% | 84% | **100%** |
| average of played | 993k | 988k | 991k | **981k** | 974k | 934k | 920k | **889k** |
| single-number grade | **F** | **F** | AAA+ | **B** | AA | A+ | A | **A+** |

S19 — where he plays at a 981k average over 111 charts — grades **B**. S24, at 889k, grades **A+**.
Sitewide the strongest player on the site would show **F in 11 of 26 singles folders**, with Level 1
and Level 2 as his best-graded folders.

**It de-levels on content.** Same scores, folder roster at Phoenix launch vs today: nine of ten
folders regressed, S16 by seven rungs (SSS → AA+), S20 by three (AAA+ → AA). When the denominator is
total charts, a folder that doubles halves your number.

**The fix is not a cleverer formula — it is refusing to multiply the two axes together.** Keep them
as two numbers and both become honest and monotone in the ways that matter.

---

## 2. The model

### 2.1 Two numbers

| | Definition |
|---|---|
| **Completion** | `played / folderSize`, as a percent. Falls when the folder gains charts — correct and legible: the folder got bigger, you've completed less of it. |
| **Folder grade** | the score at the **completion tier's position**, reading the folder best-first. "80% · AAA" means *AAA or better on 80% of the folder*. |

The grade is a **held** grade, not an average: it is the worst score inside the tier, so it is the
grade the whole tier is carried at. It is also the colour under the tick on the spectrum, which is
what makes the bar and the letter the same claim rather than two numbers sitting near each other.

**This is a completionist measure first, and it costs a letter.** Climbing a tier reaches deeper into
the folder, so the grade can go *down* when completion goes up — DrMurloc's D19 reads SSS at 20%, SS+
at 40%, S+ at 60%, and AAA at 80%. That is the trade the folder is asking for, and it is the point:
depth is the achievement, and the letter says what the depth cost.

Below 20% there is no tier, so there is no grade — nothing has been held yet.

### 2.2 Completion tiers

Five tiers, used for milestones, glow, and the community bar:

| Tier | Meaning |
|---|---|
| 20% / 40% | early progress — bar length carries it, no glow |
| 60% | `rarity-glow-1` |
| 80% | `rarity-glow-2` |
| 100% — **Folder Lamp** | `rarity-glow-3` + a ring |

### 2.3 Vocabulary

Talk about **completion percent** and **folder grade**. **Never "boards", "deep", or "N-deep"** — none
of it means anything to a player. **"Folder Lamp"** is ubiquitous in the community and is the term for
100%.

### 2.4 Folders

A folder is a **(mix, chart type, level)** triple. S18, D18 and CO-OP 3 are three folders with three
levels, never merged. Co-op folders key on player count (2–5); the logic is identical.

Folders too small to be meaningful still work — the percent is just coarse. CO-OP 5 holds one chart,
so it is 0% or 100%. Display `passed / total` alongside the percent wherever room allows.

### 2.5 Per mix

Everything is per-mix by construction — rosters from `ChartMix`, grades from `LetterGradeFor(mix)`.
**P1 floors are looser below AAA** (A 750k vs P2's 800k, A+ 825k vs 900k, AA 900k vs 920k, AA+ 925k
vs 940k); AAA and up are identical. The same tier score therefore grades higher in P1 — 934,609 is
AA+ in P1 and AA in P2. Correct, not drift.

Build mix-agnostic from the start: P1 has ~1,562 users with records against P2's ~19, so P1 is where
this reaches an audience on day one.

### 2.6 What it reads like

DrMurloc, Phoenix 1 — measured, not illustrative:

| | S16 | S17 | S18 | S19 | S20 | S21 | S22 | S23 | S24 |
|---|---|---|---|---|---|---|---|---|---|
| completion | 93% | 96% | 97% | **66%** | 94% | 94% | 92% | 83% | **100%** |
| tier | 80 | 80 | 80 | **60** | 80 | 80 | 80 | 80 | **100** |
| grade | SS+ | SS+ | S+ | AAA | AAA | AA+ | A+ | A+ | A+ |

| | D19 | D20 | D21 | D22 | D23 | D24 | D25 |
|---|---|---|---|---|---|---|---|
| completion | 86% | **61%** | 66% | 72% | 76% | 44% | 21% |
| tier | 80 | 60 | 60 | 60 | 60 | 40 | 20 |
| grade | AAA | AAA | AA+ | AA+ | AA | A+ | A+ |

Two things fall out of the tier row. S19 is his one singles gap — 66% keeps it on the 60 tier while
its neighbours sit on 80, and it reads a *better* grade for it, which is exactly the trade being
made. And his doubles are shallower throughout: D22 holds AA+ at 60% where S21 holds AA+ at 80%, so
the same letter is a different achievement on each side.

---

## 3. Display

### 3.1 The chip — hue is grade, glow is completion

Two channels over two systems the site already ships, with no color collision:

- **Hue** — `MixThemes.GradeColors`, the grade-metal ladder (SSS+ ice-blue → AA copper → sub-A green).
- **Glow** — `.rarity-glow-1/2/3`, whose shadows deliberately omit a color so they take
  `currentColor`, per §2.2's tiers.

The completion percent rides under the letter.

### 3.2 The spectrum bar

Charts sorted best-first, each segment wearing its own grade metal, unplayed capped in
`UnpassedGradeHex` grey. The filled length **is** the completion percent. Tier ticks sit at 20 / 40 /
60 / 80% of the track.

**There is no 100% marker.** The track's own end already *is* 100%, so a flag there draws a finish
line on top of the finish line — it reads as a stray artifact hanging off the bar, and on a vertical
column it reads as a progress cap it never was. A lamped folder glows instead, in its own grade metal — lamped at AA burns copper, lamped at
SSS+ burns ice-blue, so the glow says how well it was lamped and not only that it was.

Same data the By-Level Breakdown widget's **Grade Distribution** preset already computes (grades
stacked per folder with an unplayed cap), rendered horizontally for a single folder.

### 3.3 Restraint on the tier-list page

**The bar sells the story — the copy stays out of its way.** One line above it:

```
93% complete · AA+
```

No difficulty ball (you are already on the S22 page, so it is redundant), no chart counts, no average
score, no "N from a lamp". The segments, the tier ticks and the lamp flag carry all of it. Counts
belong in a tooltip if anywhere.

Elsewhere the ball is required, because those surfaces show many folders at once and nothing else
names which folder a row is.

---

## 4. Persistence — `PlayerFolderLevel`

A stored projection in **PlayerProgress**. This is a correctness requirement, not an optimization:
detecting a *grade improvement* or a *tier crossing* needs the previous values, and nothing in the
score journal carries them.

| Column | Notes |
|---|---|
| `UserId`, `MixId`, `ChartType`, `Level` | composite PK; `Level` holds player count for co-op |
| `Size`, `Played` | folder roster size, and charts with a passed score |
| `TierScore` | the score at the completion tier's position — what the grade reads off. Stored rather than derived because a milestone diff needs the previous grade and a row cannot rebuild a sorted score list. 0 below the first tier |
| `AverageScore` | mean across played charts — a display number (tooltips), not the grade |
| `UpdatedAt` | |

The stored row *is* the previous state at diff time, so tier and grade both derive from it — no
separate history needed. Written by `HighlightCaptureSaga` in the pass where it already has
`FolderSizes`, `FolderClears` and `Bests`. Read by every surface through a
`PlayerProgress/Contracts/Queries` read model — never a cross-vertical SQL join. Sizing is trivial:
~1,562 P1 users × ~40 folders ≈ 62k rows.

Gets a row in [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md) when it lands.

---

## 5. Milestones

### 5.1 Kinds

Folder milestones already exist, but
[`CaptureFolderLamps`](../../ScoreTracker/ScoreTracker.PlayerProgress/Application/HighlightCaptureSaga.cs)
hard-returns unless `clears == size`, so today they fire only at 100%.

| Kind | Disposition |
|---|---|
| `FolderProgress` | **New, and the only new kind.** Carries the folder plus whichever of tier / grade changed. `Detail = "S22\|60\|80\|AAA\|AA+"`, with empty slots where a half didn't move. |
| `FolderPassLamp` | **Keep** — the 100% crossing, already wired through Discord, the widget and the policy. `FolderProgress` at 100% and this fire together; the card renders one line. |
| `ParagonLevelGain` | **Retire.** |
| `FolderGradeLamp`, `FolderPlateLamp` | **Keep** — "every chart at SS or better" is a different fact from an average, and both already fire only at 100%. |

One kind, not five. Everything the reader needs — which folder, what moved — is in the detail.

### 5.2 Retiring Paragon

Tier crossings are a strict superset: Paragon fired only for players who had already completed a
title. Four call sites:

- `MilestoneKind.ParagonLevelGain` (the enum member)
- `TitleSaga.cs` — two `PlayerMilestoneWrite`s (~lines 211, 355)
- `CommunitySaga.cs` — the paragon line block (~line 608) and `ParagonEmoji`
- `MilestoneStrip.razor` — the `KindLabel` and `Description` branches

Milestones persist by name, so retiring the producer leaves captured history readable. `ParagonLevel`
itself stays — still Phoenix 1 title vocabulary.

### 5.3 Seed-silently — the first-import rule

**There is no initial-import suppression anywhere in `HighlightCaptureSaga`.** Harmless today because
folder milestones only fire at 100% clear. Under tiers, a first PIUGAME import of ~2,800 scores would
cross every tier and every grade boundary at once — hundreds of milestones and a Discord card that is
a wall.

**Rule: if there is no prior `PlayerFolderLevel` row for the folder, write the row and emit no
milestone.** First observation seeds silently. No batch-size magic number, self-healing, and a
returning player's *next* import behaves correctly. Pin it with a DomainTest.

### 5.4 The card line

One shape, for every folder milestone:

```
<difficulty bubble> 60%→80% at AAA→AA+
```

Show a half only if it moved: a tier crossing with no grade change drops the grade half, a grade
improvement inside the same tier drops the percent half. A lamp appends 🎉.

| Situation | Line |
|---|---|
| tier and grade both moved | `🔴22  60%→80% at AAA→AA+` |
| tier only | `🔴22  60%→80% at AA+` |
| grade only | `🔴22  80% at AAA→AA+` |
| lamp | `🔴24  80%→100% at A+ 🎉` |

The bubble is the guild's difficulty emoji (`#DIFFICULTY|S22#` → `<:piu_…:snowflake>`, resolved in
`DiscordBotClient.ReplaceEmojiTokens`) — the real bubble art. **Never** recolour it by letter grade.

### 5.5 Who sees what

Discord notifications and the Community Highlights widget are different surfaces with different bars.

| Surface | Rule |
|---|---|
| Session highlights (`MilestoneStrip`) | Everything — every tier crossing, every grade improvement. |
| **Community Discord notifications** | Everything, the full spectrum, under the existing 4000-char budget. |
| **Community Highlights widget** (homepage) | The bar stays high: tiers **60 / 80 / 100** only, and grade improvements only where the new grade is **S or better**. |

The widget cutoffs live in `CommunityHighlightPolicy` as consts alongside the existing ones — pure,
DomainTest-pinned, tunable without touching plumbing:

```csharp
/// <summary>Only deep completion reads as a community win — 20/40% is personal progress.</summary>
public const int FolderTierMinPercent = 60;

/// <summary>A folder grade improvement counts only from S upward.</summary>
public const PhoenixLetterGrade FolderGradeMin = PhoenixLetterGrade.S;
```

A new `WinKind.FolderProgress` renders through the widget's existing chart-less path, which already
draws a real `DifficultyBubble` from a folder label (see `ParseFolder`). `MaxWinsPerEvent = 4` caps
the rest.

---

## 6. Surfaces

All of them render the same primitive at different sizes: sorted grade segments, grey unplayed cap,
tier ticks, grade chip. One concept, one component.

| # | Surface | Shape |
|---|---|---|
| 1 | **Home widget** | A `folder-levels` widget for folders you pick. Each size holds a fixed count — **1x1 one, 2x1 two, 2x2 four, 2x3 seven** (two wide, up to three deep) — because a bigger cell should show *more folders*, not the same folders with a scrollbar. Picking sits behind a **Select Folders** popover — `FolderGrid` is a popover control like a date picker, never inline chrome — in the multi-toggle mode the randomizer already drives. A fresh widget fills itself with folders around your competitive level, alternating singles and doubles, each type walking its own level. Separately, By-Level Breakdown gains a **Clear Progress by Grade** preset — the same view at all-folders scale. It is a preset rather than a colour field on Clear Progress because the `LetterGrade` metric with `IncludeUnplayed` already expresses it exactly, and a second way to say the same thing would widen the public config vocabulary (D19) for nothing. |
| 2 | **Tier list page** | The full bar for the folder you are on, under the one-line pointer that replaced the PUMBILITY title track on 2026-09-05 ("what is my next title" is the PUMBILITY page's job now); this answers "how far through this folder am I, and how well". |
| 3 | **Community player** | `CommunityPlayer.razor`'s Folder Completion strip, **split into two graphs — singles and doubles** — since a folder level is per type. Track height keeps following real folder size; the type-hue fill becomes the spectrum. |
| 4 | **Community Discord** | §5.4's line. |
| 5 | **Community Highlights widget** | §5.5's narrowed set, as a bubble + one caption. |

---

## 7. Implementation scope

Two verticals carry the work — **PlayerProgress** owns the projection and the milestones,
**Communities** owns community significance — plus Web for every pixel. `Application`, `Domain` and
`SharedKernel` are untouched: grade resolution already exists (`LetterGradeFor(mix)`), and everything
new is vertical-local.

### 7.1 PlayerProgress — the owner

| Layer | Work |
|---|---|
| `Contracts/` | `FolderLevelRecord` DTO; `GetPlayerFolderLevelsQuery` (all folders for a user+mix) and a single-folder read for the tier-list page; `FolderCompletionTier` (the 20/40/60/80/100 ladder — public because Web renders glow from it); `MilestoneKind.FolderProgress` added, `ParagonLevelGain` removed. |
| `Domain/` | `FolderLevelCalculator` — internal and pure: roster + bests → `(Size, Played, AverageScore)`, and old-row vs new-row → milestone writes. All the interesting logic, all unit-testable with no I/O. |
| `Application/` | `HighlightCaptureSaga` — the load-bearing change (see 7.4). `TitleSaga` — delete two `ParagonLevelGain` writes. One query handler. |
| `Infrastructure/` | `PlayerFolderLevelEntity` (internal) + `EFPlayerFolderLevelRepository`. |
| `Wiring/` | One line in `AddPlayerProgress()`; the entity joins `PlayerProgressModelContribution`. **No CompositionRoot change** — that contribution is already in `VerticalModelContributions.All()`. |

### 7.2 Communities

| Layer | Work |
|---|---|
| `Domain/` | `CommunityHighlightPolicy` — `FolderTierMinPercent` / `FolderGradeMin` consts, a `ClassifyMilestone` branch, a priority const. |
| `Contracts/` | `WinKind.FolderProgress`. ⚠ `SignificantWin` has no field for a grade, and `Rank` is the only free int. Carrying "80% at AA+" needs a new field, which changes the persisted JSON payload → **bump `CommunityHighlightSchema.CurrentVersion` to 2** and confirm older rows degrade as intended. |
| `Application/` | `CommunitySaga` — delete the paragon block and `ParagonEmoji`, add the one-line renderer from §5.4. |

### 7.3 Web

New shared components (one concept, one component): **`FolderSpectrum`** (the bar — segments, ticks,
lamp flag) and **`FolderLevelChip`** (grade + glow + percent). Every consumer below composes those two.

- `Pages/TierLists/` — the §3.3 header.
- `Pages/Communities/CommunityPlayer.razor` — split into singles and doubles graphs; spectrum fill.
- `Components/HomeWidgets/FolderLevelsWidget` + config panel + config type + `WidgetRegistry` entry
  (TypeId `folder-levels`, sizes 1x1/2x1/2x2/4x2, `RefreshOnScoreImport: true`). Must satisfy
  `WidgetRenderContractTests`' five-param render contract.
- `Components/HomeWidgets/CommunityHighlightsWidget` — the new `WinKind` in both list and card paths.
- `Components/Sessions/MilestoneStrip.razor` — new branch, Paragon branches deleted.
- `wwwroot/css/site.css` — spectrum classes. Tokens only; `UiColorTokenTests` forbids literals.
- Localization — new UI + Discord strings in **all nine locales** in the same pass.

### 7.4 The one risky change

`HighlightCaptureSaga.CaptureFolderLamps` currently early-returns unless `clears == size`, so folder
work only runs for a completed folder. Removing that guard puts this code on the hot path for
**every folder touched by every import**. Three things ride on getting it right:

1. **Seed-silently** (§5.3) — without it the first import of an account emits hundreds of milestones.
2. **Write volume** — one upsert per touched folder per batch, not per chart.
3. It already has `FolderSizes`, `FolderClears`, `Charts` and `Bests` in `CaptureData`, so no new
   reads are needed — keep it that way.

### 7.5 Tests

`DomainTests/FolderLevelCalculatorTests` (tier maths, grade maths, tiny folders, co-op) ·
`ApplicationTests/HighlightCaptureSagaTests` extended (tier crossing, grade gain, **seed-silently**) ·
`DomainTests/CommunityHighlightPolicyTests` (the 60% / S bar) · `ApplicationTests/CommunitySagaTests`
(the four line shapes; paragon tests deleted) · `Tests.Components` (chip, spectrum, widget at each
size) · `Tests.Integration` (repository + migration).

### 7.6 Backfill

Seed-silently means rows appear on a player's next import, which would leave every existing player's
tier-list page empty until then. So existing players get backfilled — **both mixes in one pass**, P2
being 19 users and P1 being the audience, so splitting saves nothing.

**Admin-triggered, never automatic, never in the migration.** Computing an average per
(user, mix, type, level) across ~1,562 users × ~40 folders is the shape that took prod SQL down on
2026-07-10, and migrations run inside the gated deploy stage where there is nothing to throttle them.
It lands as a bus message published from an admin button, consumed in chunks — the same
press-it-post-deploy shape as Recalculate Ratings.

Backfilled rows are **silent**: they write state without emitting milestones, exactly like §5.3's
first-observation rule. Nobody gets a hundred Discord lines because the feature shipped.

### 7.7 Commit order

One PR. Every commit builds green; only 6 and 11 touch existing tests.

| # | Commit |
|---|---|
| 1 | `docs(design): fold the folder level design into the doc set` |
| 2 | `feat(progress): add the PlayerFolderLevel projection` — contracts, calculator, entity, repository, wiring, migration, schema doc |
| 3 | `feat(progress): write folder levels from the score-batch pipeline` — the §7.4 change, alone |
| 4 | `feat(progress): backfill folder levels for existing players` — admin button, chunked, both mixes |
| 5 | `feat(progress): emit FolderProgress milestones on tier and grade changes` |
| 6 | `feat(progress): retire Paragon level milestones` — enum removal forces every call site atomic |
| 7 | `feat(web): add FolderSpectrum and FolderLevelChip` |
| 8 | `feat(tierlists): show folder completion and grade` |
| 9 | `feat(home): add the folder-levels widget` |
| 10 | `feat(communities): split the folder completion strip by chart type` |
| 11 | `feat(communities): carry folder progress into Discord cards and highlights` — includes the schema bump |
| 12 | `feat(home): colour By-Level Breakdown clear progress by grade` |
| 13 | `chore(l10n): localize folder level strings` — one discrete pass, nine locales |

Localization lands last on purpose: it is the pass that corrupts silently when rushed, and sweeping
every new key at once beats touching nine resx files across six commits.

Also needs: a `PlayerFolderLevel` row in [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md), and a
[UX-GUIDELINES.md](../UX-GUIDELINES.md) entry if the spectrum counts as a new pattern.

## 8. Open

1. **Co-op on the community player page.** Two graphs are settled for singles and doubles; whether
   co-op earns a third (four columns, one of them a single chart) is undecided.
2. **Folder-level leaderboards.** Out of scope; if added, the anti-grind story needs its own pass.

## 9. Non-goals

Replacing Pumbility, replacing Phoenix 2 titles, and any change to Phoenix 1 Paragon *history* — the
producer retires, captured milestones stay readable.
