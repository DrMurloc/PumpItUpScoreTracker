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
    // Their "Sustain time" is max(len(range)) over the eNPS ranges of interest — the chart's
    // LONGEST unbroken run, not a total. TimeUnderTension is the sum of all of them.
    public const string SustainTime = "sustain_time";
    public const string TimeUnderTension = "time_under_tension";
    public const string DifficultyPrediction = "difficulty_prediction";

    public const string Top3Prefix = "top3:";
    public const string BadgeFractionPrefix = "badge_fraction:";
    public const string LastSegmentPrefix = "last_segment_badge:";
    public const string LastSegmentIsPeak = "last_segment_is_peak";
    public const string PracticeRankPrefix = "practice_rank:";
    public const string RarePrefix = "rare:";

    // The chart at its hardest (docs/design/chart-identity.md §4). The crux is the FIRST
    // segment reaching the chart's maximum modelled level; peakiness is that level against
    // the level the game prints, so a positive value is a spike and a negative one is a
    // chart whose difficulty is duration rather than any single passage.
    public const string CruxLevel = "crux_level";
    public const string CruxPeakiness = "crux_peakiness";
    public const string CruxPosition = "crux_position";
    public const string CruxDuration = "crux_duration";
    public const string CruxEnps = "crux_enps";
    public const string CruxBadgePrefix = "crux_badge:";

    // Where the body goes (docs/design/chart-identity.md §4b), replayed from the arrows rather
    // than read off a badge. These are the only measures in the corpus that can say whether a
    // doubles chart ever leaves the middle, or how far a chart turns you — the badge vocabulary
    // has no word for either. BracketRowShare is also the veto on piucenter's bracket badges,
    // which are a limb-assignment guess and read ordinary jumps as brackets.
    public const string PadShareMid4 = "pad_share_mid4";
    public const string PadShareMid6 = "pad_share_mid6";
    public const string StanceDiagonal = "stance_diagonal";
    public const string StanceSideOn = "stance_side_on";
    public const string StanceCrossed = "stance_crossed";
    public const string BracketRowShare = "bracket_row_share";

    // How much of the chart is the same single panel twice in a row — the physical precondition
    // for a footswitch, measured from the arrows rather than inferred from a limb guess.
    public const string RepeatedPanelShare = "repeated_panel_share";

    // First segment start to last segment end, in seconds — what the longest run is a share of.
    public const string ChartSpan = "chart_span";

    // DERIVED, never banked: what fraction of the chart's judgements are held rather than
    // stepped, computed where a profile meets a mix's judged note count (the count is per-mix,
    // so the value cannot live in this table). Reserved here so the folder-baseline row and the
    // chip that reads it share one name (docs/design/chart-identity.md §3.9).
    public const string HoldShare = "hold_share";

    // Which mix's simfile the analysis was actually run against, as 1 or 0 for "Phoenix".
    // Piucenter's corpus is stepcharts, and only 28.6% of it is Phoenix-era: the rest describes
    // whatever that song looked like in XX, PRIME, FIESTA and so on. Usually identical, because
    // most charts do not change between mixes — but not always, and without this there is no way
    // to ask which chips are describing a chart nobody can play any more.
    public const string PackIsPhoenix = "pack_is_phoenix";
}
