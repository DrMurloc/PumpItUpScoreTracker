using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     Competitive Level widget config (public contract via export/import, D19).
///     Null Mixes = follow the effective mix (D13). No Combined series — removed as an
///     option entirely (owner, 2026-07-12).
/// </summary>
public sealed record CompetitiveLevelConfig
{
    public List<MixEnum>? Mixes { get; set; }

    /// <summary>0 = all time.</summary>
    public int RangeMonths { get; set; } = 6;

    public bool ShowSingles { get; set; } = true;

    public bool ShowDoubles { get; set; } = true;
}
