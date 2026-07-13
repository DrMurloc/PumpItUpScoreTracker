# Suggested Charts widget

Part of the home dashboard widget catalog — see [README.md](README.md) for architecture, locked
decisions (D1–D19), the widget registry contract, and the widget index. **Catalog-walk pick 1 — the
WSIP release blocker (shipped PR #144).**

---

One widget type, goal as config (D10). Owner decisions (2026-07-12): goal bundles with per-category
toggles; the deviation-powered **skill-gaps goal is HELD** pending owner iteration; **veto ✕ is
edit-mode only** (declutters browse — revisit if people complain); level config = **Any / Dynamic
(competitive level ± spread, follows the player) / Static (pinned range)** with a **chart-level vs
scoring-level basis** toggle (`GetChartScoringLevelsQuery`, printed-level fallback); **shuffle**
re-roll in the body meta row. The page's vestigial `LevelOffset` UI is superseded by the level modes.

- **Goal bundles** (`SuggestedGoal` → engine categories): *Title Hunt* = PushLevel + SkillTitles ·
  *Score Push* = PushPGs + ImproveTop50 + RevisitOldScores · *Fill Gaps* = FillScores. The Weekly
  category is dropped from the widget — the Weekly widget owns that board. Defaults per drawer preset:
  Score Push = Any level; Fill Gaps = Dynamic ±3.
- **Engine** (`RecommendedChartsSaga`): `GetRecommendedChartsQuery` gained additive `Categories` +
  `RecommendationLevelWindow` params — null = legacy, the WSIP page is untouched until cutover.
  An explicit window REPLACES the legacy per-category bands (fills CL−3..CL−1, old scores CL−2..CL)
  and filters the previously unbounded PG/Top-50 categories; title-driven categories ignore it.
  Category names live in `RecommendationCategories` consts (the pushing-title category's name is the
  title's own name).
- **Rendering** (through field-test rounds 2–3): wide sizes (columns > 1) render **horizontal card
  strips** — compact art-forward chart cards with hover-revealed pager arrows (delegated clicks in
  `dashboard-grid.js`, hidden on touch) and honest x-axis edge fades; 2x1 = one merged strip, 2x2 = a
  captioned strip per section. The tall 1x2 keeps `dash-targets` rows but **drops the song name**
  (art tooltip + details dialog carry it) — right column = `LetterGradeIcon` + score with per-category
  detail ("−1,656 to PG", "74 days old"). Fill Gaps rows never show scores (unpassed by definition);
  an optional **tier-list difficulty lens** (Pass/Score, `GetBlendedTierListQuery`, optional
  Personalized blend) fills the column instead, colored via `ThemeScales.DifficultyColor`. Dynamic
  level spreads are **asymmetric** (levels below / levels above). Instance titles follow the
  configured goal via `WidgetDescriptor.DynamicNameKey` so three rapid-fired presets wear distinct
  names. Sizes 1x2 / 2x1 / 2x2, default 1x2.
- **Feedback**: veto ✕ (edit mode) → WSIP's reason dialog (reason/notes/hide, hide default-on) →
  `SubmitFeedbackCommand` into the same per-category server-side store the engine already honors —
  deliberately NOT widget config. Thumbs-up = one-tap **Good Suggestion** in the shared
  ChartDetailsDialog, unlocked when the row click carries a suggestion category.
- **Shell extensions this widget introduced**: `WidgetDescriptor.DrawerPresets` (one add-drawer card
  per pre-filled config, D10) and `ChartClickContext` (the OnChartClick payload — chart + optional
  suggestion category; all widgets raise it).
- **Phoenix 2**: supported; the 272 P2 titles (PR #128) should light Title Hunt up — verify at field
  test, the old page-level P2 gate stays dead (D14).
