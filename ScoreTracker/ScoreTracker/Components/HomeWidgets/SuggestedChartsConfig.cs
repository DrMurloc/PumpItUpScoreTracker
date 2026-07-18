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
    FillGaps,

    /// <summary>The projected-Pumbility targets, ranked by the rating each chart would add.</summary>
    PumbilityPush,

    /// <summary>Unpassed charts similar to recent plays that raised your competitive level.</summary>
    HotStreak
}

/// <summary>How far back Hot Streak draws its seeds from.</summary>
public enum SuggestedLookback
{
    Days30,
    Days90,
    Year1,
    AllTime
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

    /// <summary>Hot Streak: a seed must beat this percent of Peers (0 = the flag alone qualifies).</summary>
    public int HotStreakPeerPercentile { get; set; } = 80;

    /// <summary>Hot Streak: how far back seeds are drawn from.</summary>
    public SuggestedLookback HotStreakLookback { get; set; } = SuggestedLookback.Days30;

    /// <summary>Hot Streak: age-outlier scores count as unplayed targets.</summary>
    public bool HotStreakIncludeOldScores { get; set; }

    /// <summary>Hot Streak: one section per seed (off = one flat list with "≈ seed" details).</summary>
    public bool GroupBySeed { get; set; } = true;
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
            SuggestedGoal.PumbilityPush => new[] { RecommendationCategory.PushPumbility },
            SuggestedGoal.HotStreak => new[] { RecommendationCategory.HotStreak },
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
        // Hot Streak pins its own levels: the similarity graph's reach gate keeps
        // targets near the seeds, so a level window never applies.
        if (config.Goal == SuggestedGoal.HotStreak) return null;
        return config.LevelMode switch
        {
            SuggestedLevelMode.Dynamic => RecommendationLevelWindow.Dynamic(
                config.SpreadBelow, config.SpreadAbove, config.LevelBasis),
            SuggestedLevelMode.Static => RecommendationLevelWindow.Static(
                config.MinLevel ?? 1, config.MaxLevel ?? DifficultyLevel.Max, config.LevelBasis),
            _ => null
        };
    }

    public static HotStreakOptions? BuildHotStreakOptions(SuggestedChartsConfig config)
    {
        if (config.Goal != SuggestedGoal.HotStreak) return null;
        return new HotStreakOptions(
            Math.Clamp(config.HotStreakPeerPercentile, 0, 99),
            config.HotStreakLookback switch
            {
                SuggestedLookback.Days90 => 90,
                SuggestedLookback.Year1 => 365,
                SuggestedLookback.AllTime => null,
                _ => 30
            },
            config.HotStreakIncludeOldScores);
    }
}
