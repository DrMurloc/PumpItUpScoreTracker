namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The recommendation engine's categories, addressable individually so callers (the
///     Suggested Charts widget's goal bundles) can request a subset instead of paying for
///     all seven. Null on the query means "everything" — the legacy WhatShouldIPlay page.
/// </summary>
public enum RecommendationCategory
{
    /// <summary>Charts toward the player's pushing difficulty title.</summary>
    PushLevel,
    SkillTitles,
    PushPGs,
    ImproveTop50,
    RevisitOldScores,
    FillScores,
    WeeklyCharts
}

/// <summary>
///     The category names stamped on <c>ChartRecommendation.Category</c> and used as the
///     feedback store's per-category hide keys. <see cref="RecommendationCategory.PushLevel" />
///     has no constant — its category name is the pushing title's own name, by design.
/// </summary>
[ExcludeFromCodeCoverage]
public static class RecommendationCategories
{
    public const string SkillTitles = "Skill Title Charts";
    public const string PushPGs = "Push PGs";
    public const string ImproveTop50 = "Improve Your Top 50";
    public const string RevisitOldScores = "Revisit Old Scores";
    public const string FillScores = "Fill Scores";
    public const string WeeklyCharts = "Weekly Charts";
}
