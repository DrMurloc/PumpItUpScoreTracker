using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     One data-derived verdict about a chart (docs/design/chart-verdicts.md). The engine
///     returns structured FACTS in salience order — Web renders the localized sentences
///     (facet type → template, all nine locales). Facets quantize before templating and
///     only speak when their minimum-evidence bar is met; an absent facet means "not
///     enough evidence", never "neutral".
/// </summary>
[ExcludeFromCodeCoverage]
public abstract record ChartVerdictFacet;

/// <summary>The pass-vs-score 2×2: how the chart sits on both community tier lists.</summary>
[ExcludeFromCodeCoverage]
public sealed record PassVsScoreVerdict(TierListCategory PassTier, TierListCategory ScoreTier)
    : ChartVerdictFacet;

/// <summary>The interquartile competitive-level band of the players who pass it.</summary>
[ExcludeFromCodeCoverage]
public sealed record PassBandVerdict(int LowerLevel, int UpperLevel) : ChartVerdictFacet;

/// <summary>The competitive level where the population's average score crosses SS+ (975k).</summary>
[ExcludeFromCodeCoverage]
public sealed record YieldKneeVerdict(int KneeLevel) : ChartVerdictFacet;

/// <summary>
///     The steepest adjacent jump in the letter-grade percentile curve — "SS is routine,
///     SSS is top-decile". PercentileJump is 0–1.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LetterWallVerdict(ParagonLevel WallGrade, double PercentileJump) : ChartVerdictFacet;

/// <summary>
///     Median plate vs the plate the median score predicts
///     (<c>ScoringConfiguration.ExpectedPlateForScore</c> — the only in-repo plate model).
///     Negative steps = plates run WORSE than scores predict (kill-spot signature);
///     positive = smoother than predicted. The only plate verdict permitted — never a
///     lifebar/"gauge" claim (owner constraint, 2026-07-14).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlateResidualVerdict(int StepsVsExpected) : ChartVerdictFacet;

/// <summary>Dominant mapped skills (coverage ≥ 25%) plus the sustained-tension flag.</summary>
[ExcludeFromCodeCoverage]
public sealed record StyleFingerprintVerdict(
    IReadOnlyList<SkillCoverageRecord> TopSkills,
    bool IsSustained) : ChartVerdictFacet;

[ExcludeFromCodeCoverage]
public sealed record SkillCoverageRecord(Skill Skill, double Coverage);

/// <summary>Debut mix and the chart's level in every mix that carries it, era order.</summary>
[ExcludeFromCodeCoverage]
public sealed record HistoryVerdict(MixEnum DebutMix, IReadOnlyList<MixLevelRecord> Levels)
    : ChartVerdictFacet;

[ExcludeFromCodeCoverage]
public sealed record MixLevelRecord(MixEnum Mix, int Level);

/// <summary>How much evidence sits behind everything else on the page.</summary>
[ExcludeFromCodeCoverage]
public sealed record PopulationVerdict(int ScoresTracked, double PassRate) : ChartVerdictFacet;
