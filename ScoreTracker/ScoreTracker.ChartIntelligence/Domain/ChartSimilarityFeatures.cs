using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     Everything the similarity calculator may know about one chart
///     (docs/design/chart-similarity.md). Null members mean "data unavailable" — the
///     calculator renormalizes signal weights over whatever a pair actually has, so
///     sparse charts still get neighbors. Percentiles and fractions are 0–1;
///     ResidualByUser is each scorer's delta from the chart's population average at
///     that scorer's competitive-level bucket.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ChartSimilarityFeatures(
    Guid ChartId,
    Name SongName,
    int Level,
    IReadOnlyDictionary<Skill, double> SkillWeights,
    TierListCategory? PassTier,
    TierListCategory? ScoreTier,
    IReadOnlyDictionary<ParagonLevel, double>? LetterPercentiles,
    double? ScoringLevel,
    double? Nps,
    double? SustainFraction,
    double? TensionFraction,
    double? NoteCount,
    Name? StepArtist,
    SongType SongType,
    double? BpmAverage,
    MixEnum DebutMix,
    IReadOnlyDictionary<Guid, double> ResidualByUser);
