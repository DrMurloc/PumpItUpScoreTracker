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
    string? ExternalKey);
