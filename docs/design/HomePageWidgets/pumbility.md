# Pumbility widget (W2)

Part of the home dashboard widget catalog — see [README.md](README.md) for architecture, locked
decisions (D1–D19), the widget registry contract, and the widget index. **Shipped in PR #2 (the shell
starter trio).** Consumes the Projections v2 work (README §5, shipped as PR #1/#141).

---

- **Data**: `GetPlayerStatsQuery` → `SkillRating` (the merged top-50; + `SinglesRating`/`DoublesRating`
  for the per-pool sub-line — note `SkillRating` is NOT their sum); `ProjectPumbilityGainsQuery` →
  `PumbilityProjection.ProjectedGains` → top entry(ies),
  chart names via `ChartCatalogCache`. Live pull, queries fanned with `Task.WhenAll`.
- **Config v1**: mix scope; show-projection toggle (default on); **dismissed-charts blacklist** (see below).
- **Sizes**: 1×1 (big number + top next-best-gain line), 2×1 (adds S/D pool breakdown + a **scrollable**
  target list — top ~15 by gain, inner scroll; never a hard snap to 3, players who are stuck on the top
  suggestions need to see past them — owner, 2026-07-12).
- **Target dismissal**: every target row carries a permanent-dismiss ✕ → chart id joins a blacklist in
  the widget's config JSON; the widget filters `ProjectedGains` against it. The config panel manages the
  list ("Dismissed charts (3) — restore"). Per-instance by design: your "Doubles push" Pumbility widget
  can dismiss different charts than your singles one. Distinct from the suggester's veto/feedback system —
  no feedback record, just personal noise control.
- **States**: no scores → CTA to `/UploadPhoenixScores`. Error isolated.
- **Mixes**: Phoenix, Phoenix 2 (additive formula live since PR #128; overall PUMBILITY corrected to a
  merged top-50 on 2026-07-13 — it is not the two per-type pools summed).
- **Data gap — sparkline/delta**: `PlayerRatingRecord` does **not** carry Pumbility, so there is no
  trend source today. Archaeology (484e58ad, June 2024): the `PlayerHistory` table was *born* with
  exactly today's columns — competitive levels, co-op rating, pass count — and has only ever fed the
  Competitive Level graph. Pumbility was never persisted; nothing was dropped in the vertical
  extraction. However `PlayerRatingsImprovedEvent` already carries `NewTop50` and `PlayerHistorySaga`
  simply doesn't persist it. **Capture lands in PR #1** (owner: "get that PR in first and I'll push it
  quick"): `SkillRating` on the history entity + record + saga mapping + migration — forward-only data;
  the widget renders its sparkline/weekly-delta conditionally once ≥ 2 points exist ("trend starts
  tracking from today" until then). Implementation note: `NewTop50` is the overall PUMBILITY
  (`SkillRating`, the merged top-50) — map it straight through. Optional backfill: Pumbility-as-of-date is reconstructible from the
  ScoreLedger journal, but only back to journal capture start (weeks) — likely not worth a job.
- **Projected targets consume Projections v2 (PR #1) from day one** — the merged P2 baseline (one mixed
  top-50, corrected 2026-07-13), skill-match chips, the plate curve. The widget's stats/pools render from `GetPlayerStatsQuery` regardless, and
  insufficient-data degradation stays (per-widget degradation, D14).
