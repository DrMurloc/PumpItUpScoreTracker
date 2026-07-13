using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     Account Stats widget config (public contract via export/import, D19). Null Mix =
///     follow the current mix, falling back to Phoenix 2 on pre-Phoenix mixes (owner,
///     2026-07-13 — differs from the other widgets, which fall back to Phoenix 1).
///     MatchDimension picks the competitive level the closest-matches list ranks on;
///     null = combined. (TypeId stays "pumbility" — public export vocabulary, never renamed.)
/// </summary>
public sealed record PumbilityConfig
{
    public MixEnum? Mix { get; set; }

    public ChartType? MatchDimension { get; set; }
}
