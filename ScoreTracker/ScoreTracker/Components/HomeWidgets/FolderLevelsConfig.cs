using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     One folder the widget tracks. Level carries the player count for co-op, matching
///     Chart.Level's own convention. Type is the folder's category — its JSON values are
///     Single / Double / CoOp either way, so configs written before the category read as-is.
/// </summary>
public sealed record FolderLevelsTarget
{
    public ChartTypeCategory Type { get; set; } = ChartTypeCategory.Single;

    public int Level { get; set; } = 20;
}

/// <summary>
///     Folder Levels widget config (public contract via export/import, D19). Folders are picked
///     rather than derived: the widget answers "the folders I am grinding", and the By-Level
///     Breakdown widget already answers "all of them at once".
/// </summary>
public sealed record FolderLevelsConfig
{
    /// <summary>Null follows the currently-selected mix.</summary>
    public MixEnum? Mix { get; set; }

    public List<FolderLevelsTarget> Folders { get; set; } = new();
}
