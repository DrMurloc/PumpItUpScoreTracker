using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     Pumbility widget config (public contract via export/import, D19). Null Mix =
///     follow the effective mix (D13). DismissedCharts is the per-instance permanent
///     blacklist (owner, 2026-07-12): personal noise control, deliberately NOT the
///     suggester's feedback system.
/// </summary>
public sealed record PumbilityConfig
{
    public MixEnum? Mix { get; set; }

    public bool ShowProjections { get; set; } = true;

    public List<Guid> DismissedCharts { get; set; } = new();
}
