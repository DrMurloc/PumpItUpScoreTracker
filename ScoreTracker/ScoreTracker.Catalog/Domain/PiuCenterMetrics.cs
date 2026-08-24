namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     The metric-name vocabulary the crawl saga writes and the skill mapper reads.
///     Per-skill names are namespaced with the piucenter skill name after the colon.
/// </summary>
internal static class PiuCenterMetrics
{
    public const string Source = "PiuCenter";

    public const string DataVersion = "data_version";
    public const string Nps = "nps";

    // The note-shape counts behind the hold-tick decomposition
    // (docs/design/phoenix-score-calculator.md D13). TapRows is the reliable half —
    // hold ticks derive as our judged note count minus it. HoldTicks is THEIR
    // pre-Phoenix tick sum: diagnostics only, never display.
    public const string TapRows = "tap_rows";
    public const string HoldRows = "hold_rows";
    public const string HoldTicks = "hold_ticks";
    public const string SustainTime = "sustain_time";
    public const string TimeUnderTension = "time_under_tension";
    public const string DifficultyPrediction = "difficulty_prediction";

    public const string Top3Prefix = "top3:";
    public const string BadgeFractionPrefix = "badge_fraction:";
    public const string LastSegmentPrefix = "last_segment_badge:";
    public const string LastSegmentIsPeak = "last_segment_is_peak";
    public const string PracticeRankPrefix = "practice_rank:";
    public const string RarePrefix = "rare:";
}
