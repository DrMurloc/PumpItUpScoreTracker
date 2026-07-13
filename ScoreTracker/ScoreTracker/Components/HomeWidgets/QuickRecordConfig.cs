using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     Quick Record widget config (public contract via export/import, D19). Mix scope is
///     the only knob — the arcade recorder is deliberately tiny (docs/design/home-page-widgets.md
///     §4.2): no ClearAfterSave (it always clears), no size variants, no feedback machinery.
/// </summary>
public sealed record QuickRecordConfig
{
    public MixEnum? Mix { get; set; }
}
