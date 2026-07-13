# Competitive Level widget (W1)

Part of the home dashboard widget catalog — see [README.md](README.md) for architecture, locked
decisions (D1–D19), the widget registry contract, and the widget index. **Shipped in PR #2 (the shell
starter trio).**

---

- **Data**: `GetPlayerHistoryQuery(userId, mix)`, one call **per selected mix**. Live pull.
- **Multi-mix (owner, 2026-07-12)**: Phoenix and Phoenix 2 are selectable **together** — P2 lines start
  at 0 and climb (players enjoy the re-grind); Phoenix lines tell the pre-switch story. XX excluded.
- **No Combined series** — removed as an option entirely (owner). Series = Singles / Doubles only,
  per selected mix (max 4 lines).
- **Series encoding**: color = chart type (`--chart-singles`/`--chart-doubles` from the active theme);
  era = line *style* — current mix solid, the other mix dashed at reduced opacity. Style is a
  CVD-independent channel, so four series stay separable with two hues.
- **"Where you left off" marker**: when Phoenix is selected but all its points fall left of the visible
  range (typical post-P2-switch), render left-edge ghost ticks at Phoenix's final Singles/Doubles values
  with a small "Phoenix: S 20.4 · D 21.1" label — the regrind's reference line. Values are just the last
  records of the Phoenix history call; no new query.
- **Config v1**: mixes (multi-select: Phoenix / Phoenix 2; default = current mix); range
  (3/6/12 months/all — default 6); series toggles (Singles / Doubles, both on).
- **Sizes**: 2×1 (default), 2×2. No 1×1 — line charts need width; per-widget `SupportedSizes` exists
  for exactly this.
- **Render**: Blazor-ApexCharts (existing dependency; WSIP renders this chart today) with series colors
  from the new `MixPalette` chart pair (README §2.5). Legend + end-of-line labels; identity never color-alone.
- **States**: < 2 history points in every selected mix → "Your level history starts tracking with your
  next import." Error isolated.
- **Mixes**: Phoenix, Phoenix 2 (multi).
