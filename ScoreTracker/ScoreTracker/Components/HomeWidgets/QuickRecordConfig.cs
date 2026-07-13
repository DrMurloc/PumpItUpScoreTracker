using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     Quick Record widget config (public contract via export/import, D19). Mix scope is
///     the only knob — the arcade recorder is deliberately tiny (docs/design/home-page-widgets.md
///     §4.2): no ClearAfterSave (it always clears), no size variants, no feedback machinery.
///     <para>
///         <see cref="Mix"/> null = follow the current mix; a value pins Phoenix / Phoenix 2.
///         <see cref="AllMixes"/> overrides both: the widget shows a runtime mix picker over
///         every mix and records through the mix's scoring model (Phoenix plate path, or the
///         legacy letter-grade path for XX and older). When true, Mix is ignored.
///     </para>
/// </summary>
public sealed record QuickRecordConfig
{
    public MixEnum? Mix { get; set; }

    public bool AllMixes { get; set; }
}
