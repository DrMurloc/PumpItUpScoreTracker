using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     The widget's identity (D10): one goal per instance, drawer presets for
///     discoverability. The skill-gaps goal is HELD pending owner iteration on the
///     deviation approach — do not add it without a fresh decision.
/// </summary>
public enum SuggestedGoal
{
    /// <summary>Pushing-title charts + skill titles one SSS away.</summary>
    TitleHunt,

    /// <summary>Closest PGs, top-50 picks, and old scores due a revisit.</summary>
    ScorePush,

    /// <summary>Approachable unpassed charts around (default: below) your level.</summary>
    FillGaps
}

public enum SuggestedLevelMode
{
    /// <summary>No level filter — each category keeps its own natural range.</summary>
    Any,

    /// <summary>[CL − SpreadBelow, CL + SpreadAbove]; moves with the player.</summary>
    Dynamic,

    /// <summary>Pinned MinLevel–MaxLevel; never drifts.</summary>
    Static
}

/// <summary>
///     The optional Fill Gaps data point (owner, field-test round 3): unpassed charts
///     have no score to show, so the right column can carry a tier-list difficulty
///     instead — community by default, the player's personalized blend on request.
/// </summary>
public enum SuggestedDifficultyLens
{
    None,
    PassDifficulty,
    ScoreDifficulty
}

/// <summary>Suggested Charts widget config (public contract via export/import, D19).</summary>
public sealed record SuggestedChartsConfig
{
    public SuggestedGoal Goal { get; set; } = SuggestedGoal.TitleHunt;

    public MixEnum? Mix { get; set; }

    /// <summary>Null = both singles and doubles.</summary>
    public ChartType? ChartType { get; set; }

    /// <summary>Categories of the chosen goal the player switched off (advanced row).</summary>
    public List<RecommendationCategory> DisabledCategories { get; set; } = new();

    /// <summary>
    ///     Only meaningful for Score Push and Fill Gaps — Title Hunt's categories pin
    ///     their own levels (the pushing title / the skill-title charts).
    /// </summary>
    public SuggestedLevelMode LevelMode { get; set; } = SuggestedLevelMode.Any;

    public int SpreadBelow { get; set; } = 2;

    public int SpreadAbove { get; set; } = 2;

    public int? MinLevel { get; set; }

    public int? MaxLevel { get; set; }

    public RecommendationLevelBasis LevelBasis { get; set; } = RecommendationLevelBasis.ChartLevel;

    /// <summary>Fill Gaps only: the tier-list difficulty shown on each card/row.</summary>
    public SuggestedDifficultyLens DifficultyLens { get; set; } = SuggestedDifficultyLens.None;

    /// <summary>Bend the lens with the player's personalized blend (tier-list machinery).</summary>
    public bool PersonalizedLens { get; set; }
}

public static class SuggestedGoals
{
    /// <summary>The goal bundles (owner, 2026-07-12). Order = section display order.</summary>
    public static IReadOnlyList<RecommendationCategory> CategoriesFor(SuggestedGoal goal)
    {
        return goal switch
        {
            SuggestedGoal.ScorePush => new[]
            {
                RecommendationCategory.PushPGs, RecommendationCategory.ImproveTop50,
                RecommendationCategory.RevisitOldScores
            },
            SuggestedGoal.FillGaps => new[] { RecommendationCategory.FillScores },
            _ => new[] { RecommendationCategory.PushLevel, RecommendationCategory.SkillTitles }
        };
    }

    /// <summary>
    ///     The goal's categories minus the player's disables; falls back to the full
    ///     bundle if config managed to disable everything (imports are tolerant, D19).
    /// </summary>
    public static IReadOnlyList<RecommendationCategory> EffectiveCategories(SuggestedChartsConfig config)
    {
        var categories = CategoriesFor(config.Goal);
        var enabled = categories.Where(c => !config.DisabledCategories.Contains(c)).ToArray();
        return enabled.Any() ? enabled : categories;
    }

    public static RecommendationLevelWindow? BuildWindow(SuggestedChartsConfig config)
    {
        return config.LevelMode switch
        {
            SuggestedLevelMode.Dynamic => RecommendationLevelWindow.Dynamic(
                config.SpreadBelow, config.SpreadAbove, config.LevelBasis),
            SuggestedLevelMode.Static => RecommendationLevelWindow.Static(
                config.MinLevel ?? 1, config.MaxLevel ?? DifficultyLevel.Max, config.LevelBasis),
            _ => null
        };
    }
}
