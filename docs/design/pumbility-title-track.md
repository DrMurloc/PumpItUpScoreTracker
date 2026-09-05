# PUMBILITY Title Track (Phoenix 2 tier-list page) — retired

**Retired 2026-09-05.** The Phoenix 2 tier list no longer draws the folder-title track. Title
tracking is the PUMBILITY page's job — the "Your PUMBILITY titles" rails on `/Pumbility/Breakdown`
([pumbility-overhaul.md](pumbility-overhaul.md)) — and a second reading of the same ladder on the
tier list was one surface too many (owner: *"PUMBILITY page is better suited for title tracking at
this point"*). Phoenix 1 is untouched; its Folder Levels bars stay in the Folder Stats drawer.

## What is there now

One line where the bar used to be, above the folder standing: **Looking for title progress? See
the PUMBILITY page** — a trophy icon, the sentence, the link to `/Pumbility/Breakdown`, and a ×
that hides it for good on this account. Boxed the way the track's "behind your top 50" whisper was,
with the × at the far end of the row (owner-approved mock, 2026-09-05). `TitleProgressPointer.razor`
renders it; the dismissal is the `TierLists__TitlePointerDismissed` UiSetting, read and written by
the component itself, so the page only supplies the gate.

It appears **only where the bar would have appeared**, which is the one piece of the track that
survives: `FolderTitleTrack.HasTitleProgress` (pure, `Web/Services`, `FolderTitleTrackTests`) is
the test the track applied before drawing — signed in, Phoenix 2, a Singles or Doubles folder,
level 10 or above, a rung still above your pool, and a folder that is not beneath your top 50 (a
chart here at SSS+ on a Perfect Game still could not crack your fifty). The track's whisper-only
case — beneath your top 50 — shows no pointer either: the rule is the bar, not the element (owner
ruling, 2026-09-05).

## What went

The glowing bar, the NEXT title and the "from …" whisper, the three-way caption (on pace / grade
up / reach), the "serves" chip and banner, the beneath-you whisper; `FolderTitleTrackResult`,
`FolderTrackMode`, the `.ptt-*` styles, and six locale keys across nine locales. The pointer has
its own component tests (`TitleProgressPointerTests`).

## What it was, for the record

Built 2026-07-21 (PR #180) after six mock passes and three field-test rounds; the full model is in
this file's history. In short: one glowing bar toward your next PUMBILITY title, rung to rung, with
the target title top-right; one caption — *"~N more charts in this folder"* when your grade here
already cleared it, *"Pass N charts in this folder with A or better"* when it did not (a count at a
pass floor, never a fail grade), *"(only N exist in this folder)"* for a folder above your level;
and a "serves" notifier reading the rung a folder of AA clears lands on. Phoenix 2's compressed
grade multipliers (A→SSS+ is only ×1.28→×1.50) made folder level the dominant lever, which is why
the caption led on charts rather than grades — and why the PUMBILITY page's per-chart ask
([pumbility-overhaul.md](pumbility-overhaul.md)) reads the same ladder more usefully.
