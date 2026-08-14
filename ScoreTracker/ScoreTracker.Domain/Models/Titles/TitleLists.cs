using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Models.Titles;

/// <summary>
///     Mix-level facts about the shipped title taxonomy, derived from the lists themselves so
///     they can never drift from what actually ships.
/// </summary>
public static class TitleLists
{
    // The TYPE is the test, matching the recommendation engine's own emptiness condition —
    // Phoenix 2's "Difficulty" CATEGORY names its pumbility titles, so a category check would
    // wrongly say P2 has a level ladder to hunt.
    private static readonly bool PhoenixHasDifficulty =
        PhoenixTitleList.BuildList().Any(t => t is PhoenixDifficultyTitle);

    private static readonly bool Phoenix2HasDifficulty =
        Phoenix2TitleList.BuildList().Any(t => t is PhoenixDifficultyTitle);

    /// <summary>
    ///     Whether the mix has chart-level difficulty titles to push toward. Phoenix does;
    ///     Phoenix 2 does not — its 272 titles are pumbility ladders, grade badges and play
    ///     counts. If Andamiro ships P2 difficulty titles and the list gains them, this flips
    ///     on its own.
    /// </summary>
    public static bool HasDifficultyTitles(MixEnum mix)
    {
        return mix switch
        {
            MixEnum.Phoenix => PhoenixHasDifficulty,
            MixEnum.Phoenix2 => Phoenix2HasDifficulty,
            _ => false
        };
    }
}
