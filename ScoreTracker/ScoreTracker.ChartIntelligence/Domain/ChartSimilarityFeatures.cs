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
///     SustainFraction and BurstFraction are the two halves of time under tension — the
///     grind and the spikes — as fractions of the song's length. They are disjoint by
///     construction, which is the point: sustain is a subset of tension, so carrying
///     tension itself alongside sustain would count the grind twice and leave the spikes
///     with no dimension of their own.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ChartSimilarityFeatures(
    Guid ChartId,
    Name SongName,
    int Level,
    IReadOnlyDictionary<string, double> BadgeCoverage,
    double? Nps,
    double? SustainFraction,
    double? BurstFraction);
