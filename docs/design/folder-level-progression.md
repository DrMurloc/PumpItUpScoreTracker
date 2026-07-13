# Folder Level Progression — Workshop Capture

**Status: NOT designed. Workshop pending.** This doc captures the 2026-07-10 workshop state so the tier-lists overhaul ([tier-lists-overhaul.md](tier-lists-overhaul.md)) can ship without it. Nothing here is decided except the constraints.

## Problem

Phoenix 1's per-folder progress display leans on level-based titles (Intermediate/Advanced/Expert Lv.X): the tier-list page shows title progress and "remaining charts" math per folder. Phoenix 2's title set (272 titles, landed in PR #128) is **not level-based**, so that display has no P2 equivalent. The folder progress strip needs a successor concept.

## Constraints (owner-locked)

- **Paragon/title progress remains for Phoenix 1 unchanged.** Whatever ships here is additive for P2 (and possibly retrofits P1 later), never a P1 regression.
- **Folder growth**: folders gain charts over time (new songs, new mixes of content). Count-based progress regresses when content lands — this was already a sore point with the old title system. Any design must state its growth behavior explicitly.
- Whatever the score is, it should be cheap to compute from data the site already has (scores per folder), and it feeds future consumers: Discord session cards, season recap, possibly leaderboards.

## Candidate directions (from workshop, unranked)

1. **Percentage lamps** — folder progress as % thresholds on the existing lamp ladder (Pass %, AA %, … PG %).
   - *Growth behavior*: honest but demotivating — percentages drop when songs land.
2. **Top-N folder points ("folder Pumbility")** — sum of best N chart ratings within the folder, fixed N.
   - *Growth behavior*: immune by construction (same trick as Pumbility's top-50; matches the P2 additive formula players are learning).
   - Owner invented Pumbility; strongest mathematical footing.
3. **Threshold tiers ("folder plates")** — Bronze/Silver/Gold/Sapphire folder tiers at fixed pass-%/grade gates, **achieved-forever** (trophy semantics dodge regression).
   - Maps directly onto the plate metal ladder and rarity ramp tokens from the 2026-07 theme system; renders as one glanceable strip.
   - Weakest math (thresholds are arbitrary), best display language.

These compose: **top-N points as the score, plate tiers as the display** was the workshop's tentative favorite shape, but it has not been pressure-tested.

## Open questions for the workshop

1. Achieved-forever vs. live-recomputed — do folder levels ever go down?
2. What is N per folder (folder sizes vary wildly across levels)?
3. Do CoOp folders (grouped by player count, not level) participate?
4. Is this per-mix, or does a folder level carry across mixes the way Pumbility doesn't?
5. Where does it surface beyond the tier-list strip — profile, Discord cards, recap?
6. Does any leaderboard attach to it (folder-level rankings), and if so what's the anti-grind story?

## Non-goals

Replacing Pumbility, replacing P2 titles, or shipping anything before the tier-lists overhaul lands.
