namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     One banked numeric fact about a chart from an external step-analysis source.
///     MetricName is namespaced per kind ("badge_fraction:twist_90", "top3:run",
///     "practice_rank:drill", "nps", "difficulty_prediction", …); Grade carries the
///     rare letter-graded variants and is null for plain numbers.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ChartSkillMetric(
    Guid ChartId,
    string MetricName,
    decimal Value,
    string? Grade);
