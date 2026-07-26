using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <param name="Folders">"S19", or both types for a merged-pool title.</param>
/// <param name="Grade">The reference grade the answer assumes.</param>
/// <param name="Plate">The reference plate the answer assumes.</param>
public sealed record SuggestedLevel(IReadOnlyList<string> Folders, PhoenixLetterGrade Grade, PhoenixPlate Plate);

/// <summary>
///     Where a Phoenix 2 PUMBILITY title sits on the folder ladder — the tier-list page's
///     "serves" read pointed forward: the lowest folder whose fifty charts reach the title's
///     threshold.
///     <para>
///         Deliberately NOT personalised. The tier-list track answers "what does this folder do
///         for me" and needs your pool, your floor and your median to do it; this answers "which
///         folder is this title", which is a property of the title. One fixed reference
///         performance keeps the answer stable, comparable between titles, and true for a player
///         who has imported nothing.
///     </para>
/// </summary>
public static class SuggestedTitleLevel
{
    /// <summary>
    ///     An average-good clear rather than a heroic one: AAA on a Talented Game plate. The
    ///     grade multiplier and plate bonus both come from the shipped Phoenix 2 config, so a
    ///     formula correction moves this page with it.
    /// </summary>
    private const PhoenixLetterGrade ReferenceGrade = PhoenixLetterGrade.AAA;

    private const PhoenixPlate ReferencePlate = PhoenixPlate.TalentedGame;

    private const int PoolSize = 50;

    /// <summary>Below level 10 a Phoenix 2 chart prices at zero, so no folder there ever serves.</summary>
    private const int LowestScoringLevel = 10;

    public static SuggestedLevel? For(Title title)
    {
        if (title is not Phoenix2PumbilityTitle pumbility) return null;

        var config = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var perChart = config.LetterGradeModifiers[ReferenceGrade] + config.PlateModifiers[ReferencePlate];

        var types = pumbility.Pool switch
        {
            PumbilityPool.Singles => new[] { ChartType.Single },
            PumbilityPool.Doubles => new[] { ChartType.Double },
            // A merged pool can be filled from either side, so it names both.
            _ => new[] { ChartType.Single, ChartType.Double }
        };

        var folders = types
            .Select(type => Folder(type, title.CompletionRequired, perChart))
            .Where(f => f != null)
            .Select(f => f!)
            .ToArray();

        return folders.Length == 0 ? null : new SuggestedLevel(folders, ReferenceGrade, ReferencePlate);
    }

    private static string? Folder(ChartType type, int threshold, double perChart)
    {
        for (var level = LowestScoringLevel; level <= (int)DifficultyLevel.Max; level++)
        {
            // Singles price one level up the base curve, so the level a player stands in is
            // one below the level their score is worth.
            var effective = type == ChartType.Single
                ? Math.Min(level + 1, (int)DifficultyLevel.Max)
                : level;
            var pool = PoolSize * ScoringConfiguration.Phoenix2BaseRating(DifficultyLevel.From(effective)) * perChart;
            if (pool >= threshold) return $"{type.GetShortHand()}{level}";
        }

        return null;
    }
}
