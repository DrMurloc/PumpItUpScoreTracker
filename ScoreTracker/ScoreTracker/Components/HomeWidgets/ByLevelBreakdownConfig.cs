using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>What dimension of each record the graph reads.</summary>
public enum BreakdownMetric
{
    Score,
    LetterGrade,
    Plate,
    Pass,

    /// <summary>Days since the score was recorded — "how stale are my scores in this folder".</summary>
    ChartAge
}

/// <summary>How a folder of records collapses to numbers per level (docs §by-level-breakdown).</summary>
public enum BreakdownAggregation
{
    /// <summary>Numeric stat lines over PLAYED charts (Score; ordinal average for Grade/Plate).</summary>
    Distribution,

    /// <summary>Stacked category counts (or 100%-normalized) — Grade / Plate / Pass.</summary>
    Breakdown,

    /// <summary>% of the WHOLE folder meeting each threshold (unplayed = not met).</summary>
    Completion
}

/// <summary>Which charts the graph covers. Singles/Doubles use the level x-axis; Co-Op uses player count.</summary>
public enum BreakdownChartScope
{
    SinglesDoubles,
    Singles,
    Doubles,
    CoOp
}

/// <summary>
///     What a *separated* Singles-vs-Doubles distribution shows: one stat line per type, or a shaded
///     range band per type. Multi-stat box plots overlaid for both types are unreadable (owner).
/// </summary>
public enum SeparateDisplay
{
    Average,
    Median,
    Min,
    Max,
    RangeIqr,
    RangeMinMax,
    RangeStdDev
}

/// <summary>Distribution stat series (Score). Ordinal metrics use Min / Average / Max only.</summary>
public enum DistributionSeries
{
    Min,
    P10,
    P25,
    Median,
    Average,
    P75,
    P90,
    Max,
    MinusSigma,
    PlusSigma
}

/// <summary>Optional shaded band between a pair of Distribution series (Score only).</summary>
public enum BreakdownBand
{
    None,
    InterQuartile,
    MinMax,
    StdDev
}

/// <summary>Which metric a completion threshold measures against.</summary>
public enum ThresholdKind
{
    Score,
    Grade,
    Plate,
    Pass,

    /// <summary>Recorded within N days — recent scores are the "met" side (Chart Age metric).</summary>
    Age
}

/// <summary>
///     One completion bar → one line. Discriminated by <see cref="Kind" />: Score → the
///     numeric floor as a string ("990000"), Grade/Plate → the enum name ("SSS", "PG"),
///     Pass → value ignored. Single {Kind, Value} shape keeps the exported JSON-Schema
///     (D19) clean and stable.
/// </summary>
public sealed record CompletionThreshold
{
    public ThresholdKind Kind { get; set; } = ThresholdKind.Pass;

    public string? Value { get; set; }
}

/// <summary>
///     By-Level Breakdown widget config — public contract via export/import and the
///     capability schema (D19), so the shape is breaking-change territory. Holds the
///     superset of options; the config panel and aggregator read only the fields relevant
///     to the chosen Metric x Aggregation. Old/garbled blobs fall back to these defaults
///     (WidgetConfigJson, §2.3).
/// </summary>
public sealed record ByLevelBreakdownConfig
{
    /// <summary>Null = follow the resolved mix (widget override → page default → current, D13).</summary>
    public MixEnum? Mix { get; set; }

    public BreakdownChartScope Scope { get; set; } = BreakdownChartScope.SinglesDoubles;

    /// <summary>Draw Singles and Doubles as separate series (SinglesDoubles scope only).</summary>
    public bool SeparateSinglesDoubles { get; set; } = true;

    /// <summary>What a separated distribution shows per type (single stat line or a shaded range band).</summary>
    public SeparateDisplay SeparateDisplay { get; set; } = SeparateDisplay.Median;

    public int MinLevel { get; set; } = 17;

    public int MaxLevel { get; set; } = 23;

    public int MinPlayers { get; set; } = 2;

    public int MaxPlayers { get; set; } = 5;

    public BreakdownMetric Metric { get; set; } = BreakdownMetric.Score;

    public BreakdownAggregation Aggregation { get; set; } = BreakdownAggregation.Distribution;

    // ---- Distribution ----

    /// <summary>Named stat series to draw. Ordinal metrics honor only Min / Average / Max.</summary>
    public List<DistributionSeries> Series { get; set; } = new()
    {
        DistributionSeries.P25, DistributionSeries.Median, DistributionSeries.P75
    };

    /// <summary>Custom percentiles (1–99), each an extra line (Score only).</summary>
    public List<int> CustomPercentiles { get; set; } = new();

    public BreakdownBand Band { get; set; } = BreakdownBand.None;

    // ---- Breakdown ----

    /// <summary>Normalize stacked bars to 100% instead of raw counts.</summary>
    public bool Normalize { get; set; }

    /// <summary>Show an "unplayed" segment (the folder remainder).</summary>
    public bool IncludeUnplayed { get; set; } = true;

    // ---- Completion ----

    /// <summary>Each threshold is one line; multi-threshold is allowed by design.</summary>
    public List<CompletionThreshold> Thresholds { get; set; } = new();
}
