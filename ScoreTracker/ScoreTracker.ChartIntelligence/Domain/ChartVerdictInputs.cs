using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     The evidence the verdict engine reads (docs/design/chart-verdicts.md), assembled
///     by the handler from persisted analytics and the chart's own score population.
///     Null / empty members mean the data doesn't exist — the corresponding facets stay
///     silent. Percentiles and fractions are 0–1; plate values are the
///     <see cref="PhoenixPlate" /> ladder.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ChartVerdictInputs(
    TierListCategory? PassTier,
    TierListCategory? ScoreTier,
    IReadOnlyDictionary<ParagonLevel, double>? LetterPercentiles,
    IReadOnlyList<LevelAverage> ScoreAverageByLevel,
    IReadOnlyList<LevelPasses> PassCountByLevel,
    IReadOnlyList<PhoenixPlate> ClearPlates,
    int? MedianClearScore,
    int ScoresTracked,
    int PassCount,
    IReadOnlyDictionary<Skill, double> SkillWeights,
    double? TensionFraction,
    MixEnum CurrentMix,
    MixEnum DebutMix,
    IReadOnlyList<MixLevel> MixLevels);

[ExcludeFromCodeCoverage]
internal sealed record LevelAverage(int Level, double AverageScore);

[ExcludeFromCodeCoverage]
internal sealed record LevelPasses(int Level, int Passes);

[ExcludeFromCodeCoverage]
internal sealed record MixLevel(MixEnum Mix, int Level);
