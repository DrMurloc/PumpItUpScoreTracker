# PUMBILITY Title Track (Phoenix 2 tier-list page)

Replaces the Phoenix 1 folder-title bars on `/TierLists` for Phoenix 2. Phoenix 1 folders had a
clean "get N grades in this folder" bar; Phoenix 2 titles are driven by your **top-50 PUMBILITY
pool** instead, so a folder no longer maps to one title. This reads your pool against the pooled
title ladder and shows one glowy bar plus a single folder-aware line.

Mocked and iterated with the owner (six passes) before build; the mock's final state is this doc.

## Where it lives

On the page, directly under the folder controls and **above the tier sections** — deliberately
**not** in the Folder Stats drawer (the fold belongs to the tier list, but this one strip earns its
place). Signed-in + Phoenix 2 only; Phoenix/legacy keep the difficulty-title bars in the drawer.
`PumbilityTitleTrack.razor` renders it; `FolderTitleTrack.Compute` (pure, in `Web/Services`) is the
read; wired from `ChartSkills.razor`.

## The element

- **The glowy bar** — your standing toward your next title, rung-to-rung (floored at the last
  earned rung, not zero). The **target title sits top-right** (bold, "NEXT"); the last-earned title
  is a quiet "from …" on the left. Singles folders read the `[S]` pool, doubles the `[D]` pool.
- **One caption**, where the Phoenix 1 percentage used to be, in three reads:
  - **On pace** — your grade here already clears it, so it's just volume: *"~4 more charts in this folder."*
  - **Grade up** — you'd need to score higher: *"Pass 8 charts in this folder with AAA or better"* —
    a count at a **pass (A) floor**, naming a higher grade only when the folder is too small to
    reach the title at A. Never names a fail grade.
  - **Behind you** — even a perfect folder falls short of the title: the bar disappears; only the
    "serves" whisper stays.
- **The serves banner** (▲) — shown only when the folder outranks your target: *"This folder serves
  Expert Lv.1"*, i.e. you can keep pushing past your current title here.

## The model (all from the shipped `Phoenix2PumbilityScoring`)

- **floor** = your 50th (weakest) contribution in the pool. A chart helps only if it beats the floor.
- **Count at a grade:** how many charts, at what grade, reach the title. Each chart at grade `per`
  evicts your weakest pool chart (nets `per − floor`), so `count = ceil((title − pool) / (per − floor))`.
  Scan grades from **A** (a pass — grinding to a fail means nothing) upward; take the lowest whose
  `count` fits the folder's chart total. Name a higher grade only when the folder's too small at A.
  - If even the **whole folder at PG** (`× 1.52`) can't reach the title → **hide the bar**
    (`Show = false`); the serves whisper stays. Self-handles folder size — a thin folder needs a
    higher grade, so it hides sooner.
  - `fitGrade ≤ what you already score here` (median, needs 5+ scored charts) → **on pace**;
    *"~N more charts"* at your own pace.
  - otherwise → **grade up**: *"Pass N charts … with {fitGrade} or better."*
- **serves** = the rung a folder of **AA** clears lands on (`50 × Base(effL) × 1.36`). AA (925k) is
  the realistic average pass — not A (900k). Reference grade is deliberately different from the
  personalized median: "serves" is the folder's objective ladder slot, the caption is yours.
- **Base curve:** `Phoenix2BaseRating` (linear +5/level, kink to +10 above 24); singles price one
  level up (`effL = level + 1`); a chart's ceiling is SSS+ on a Perfect Game plate (`× 1.52`).

Why grades barely move the caption: the P2 grade multipliers are compressed (A→SSS+ is only
1.28→1.50, +17%, vs Phoenix's 0.8→1.50), so clearing higher folders is the dominant lever — which
is why the caption leads on charts/folder, and the "serves" ladder spans the whole title list on AA.

## Tests

`FolderTitleTrackTests` (ScoreTracker.Tests.Components) covers the null cases (off-Phoenix-2,
co-op), a live pool's bounded progress, the hide-below-floor rule, serves-above, and the
identical-contributions guard (median == floor must not divide by zero).
