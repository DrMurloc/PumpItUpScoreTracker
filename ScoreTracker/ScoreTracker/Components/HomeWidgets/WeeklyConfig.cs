using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

public enum WeeklyBoardMode
{
    /// <summary>Reuses WeeklyChartSuggestionPolicy — the WeeklyCharts page's competitive filter.</summary>
    MatchMyRange,

    /// <summary>A standing filter applied to every future board (owner, 2026-07-12).</summary>
    Custom,

    All
}

/// <summary>Weekly Challenge widget config (public contract via export/import, D19).</summary>
public sealed record WeeklyConfig
{
    public MixEnum? Mix { get; set; }

    public WeeklyBoardMode Mode { get; set; } = WeeklyBoardMode.MatchMyRange;

    public bool IncludeSingles { get; set; } = true;

    public bool IncludeDoubles { get; set; } = true;

    public bool IncludeCoOp { get; set; } = true;

    public int? MinLevel { get; set; }

    public int? MaxLevel { get; set; }
}
