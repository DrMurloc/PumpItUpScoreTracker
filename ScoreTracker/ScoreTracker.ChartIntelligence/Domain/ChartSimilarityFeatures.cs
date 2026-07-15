using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     Everything the similarity calculator may know about one chart
///     (docs/design/chart-similarity.md). Every member describes the chart itself — what
///     it is made of, never how anyone fares against it: difficulty is the shelf's
///     ordering and metadata is its filters, both applied at read time, neither part of
///     what "similar" means. BadgeCoverage is piucenter's raw badge name → segment-
///     coverage fraction, banked as measured rather than mapped onto the display
///     vocabulary. SongName is carried only to gate siblings out; Level only to bound
///     reach and to pick the z-score cohort. The scalars are null when piucenter banked
///     no step analysis, which costs the chart its edges — both signals are mandatory.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ChartSimilarityFeatures(
    Guid ChartId,
    Name SongName,
    int Level,
    IReadOnlyDictionary<string, double> BadgeCoverage,
    double? Nps,
    double? SustainFraction,
    double? TensionFraction);
