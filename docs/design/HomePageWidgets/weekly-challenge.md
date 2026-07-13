# Weekly Challenge widget (W3)

Part of the home dashboard widget catalog — see [README.md](README.md) for architecture, locked
decisions (D1–D19), the widget registry contract, and the widget index. **Shipped in PR #2 (the shell
starter trio).**

---

- **Data**: `GetWeeklyChartsQuery(mix)` → `WeeklyTournamentChart(ChartId, ExpirationDate)` —
  **expiration is per chart** (staggered rotation, not one board reset; the mock's single countdown was
  a simplification). `GetWeeklyChartEntriesQuery(mix)` → my entries **and** per-chart entrant totals;
  `GetUserWeeklyPlacementsQuery(userId, mix, chartIds)` → `(ChartId, Place)`. Percentile =
  `1 − place/total` → `ThemeScales.RarityStyle` (never hand-rolled bands). Live pull.
- **Config v1**: mix scope (boards are parallel per mix); **board filter mode** (owner, 2026-07-12):
  - **Match my range** (default) — reuses `WeeklyChartSuggestionPolicy.GetSuggestedCharts`, the exact
    logic behind the WeeklyCharts page's competitive filter (including its "only when both competitive
    levels ≥ 10" auto-enable; below that, fall back to all charts).
  - **Custom preset** — a filter the player wants every week: chart types (S/D/co-op), level range,
    and it applies to every future board without re-configuring.
  - **All charts** — the whole board.
- **Sizes**: 1×1 compact (name + placement rows, soonest expiry as "Next rotation in …"), 2×1 (art
  cards, `ScoreBreakdown` of my entry, per-chart expiry chips, link to `/WeeklyCharts`).
- **States**: board gap → "New board soon." Unplayed charts are content, not emptiness (muted "—").
  Error isolated.
- **Mixes**: per board availability.
