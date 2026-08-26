namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     A chart's banked piucenter step analysis, shaped for display. Skill names are
///     piucenter's own vocabulary (raw, underscore-separated) — they render as
///     attributed source data, not through our Skill enum.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartStepAnalysisRecord(
    IReadOnlyList<string> TopSkills,
    IReadOnlyDictionary<string, decimal> BadgeFractions,
    decimal? Nps,
    decimal? SustainTimeSeconds,
    decimal? TimeUnderTensionSeconds,
    decimal? DifficultyPrediction,
    string? ExternalKey,
    ChartCruxRecord? Crux = null,
    ChartGeometryRecord? Geometry = null);

/// <summary>
///     Where the body goes (docs/design/chart-identity.md §4b), replayed from the arrows. Shares
///     are 0–1; the pad shares are null on singles, where "all middle" is trivially true and
///     would mean nothing.
///     <para>
///         <see cref="Diagonal" /> is here and on no chip anywhere: the median chart sits near
///         78% at every level, because the pad's corners make a diagonal stance simply what
///         walking in PIU looks like. It earns a drawer row beside the two shares that do
///         discriminate, and nothing louder.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartGeometryRecord(
    decimal? PadShareMid4,
    decimal? PadShareMid6,
    decimal Diagonal,
    decimal SideOn,
    decimal Crossed,
    decimal BracketRowShare);

/// <summary>
///     The chart at its hardest (docs/design/chart-identity.md §4): what its peak stretch is
///     made of, how far over the printed level it runs, where it sits and how long it lasts.
///     <para>
///         <see cref="Peakiness" /> is signed and both signs are identity — positive is a
///         spike, negative is a chart whose difficulty is duration rather than any one
///         passage. <see cref="Position" /> is 0–1 across the played span.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartCruxRecord(
    IReadOnlyList<string> Badges,
    decimal? Peakiness,
    decimal Position,
    decimal DurationSeconds);
