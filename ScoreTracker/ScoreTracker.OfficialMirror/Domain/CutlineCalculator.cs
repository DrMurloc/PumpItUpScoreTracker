using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     The "what does it take" math: a PUMBILITY tier value inverts into the lowest chart
///     level where 50 uniform plays clear it, per grade identity (AAA / S / SS / SSS),
///     always assuming SG plates — the same default the site's computed ratings use. Board
///     values come positionally: the r-th best row is rank r's value regardless of Olympic
///     tie numbering.
/// </summary>
internal static class CutlineCalculator
{
    /// <summary>The site serves at most this many players per PUMBILITY board — a full board's floor is the entry bar.</summary>
    public const int BoardCapacity = 1000;

    public const int ChartsCounted = 50;

    /// <summary>Every 100 through the board, every 10 through the top 100, every seat in the top 10.</summary>
    public static readonly int[] TierLadder =
        Enumerable.Range(1, 10).Select(i => i * 100).Reverse()
            .Concat(Enumerable.Range(1, 9).Select(i => i * 10).Reverse())
            .Concat(Enumerable.Range(1, 9).Reverse())
            .ToArray();

    public static readonly (string Label, PhoenixLetterGrade Grade)[] Grades =
    {
        ("AAA", PhoenixLetterGrade.AAA),
        ("S", PhoenixLetterGrade.S),
        ("SS", PhoenixLetterGrade.SS),
        ("SSS", PhoenixLetterGrade.SSS)
    };

    /// <summary>Rank r's value from a board ordered best-first; null when the board is shallower.</summary>
    public static decimal? ValueAtRank(IReadOnlyList<PlacementRow> orderedBoard, int rank)
    {
        return orderedBoard.Count >= rank ? orderedBoard[rank - 1].Score : null;
    }

    /// <summary>
    ///     The lowest level whose 50× uniform grade (SG plates) clears the tier — null when
    ///     no level does (the summit's seats can't be bought with volume at that grade).
    /// </summary>
    public static int? LevelFor(ScoringConfiguration scoring, ChartType chartType, PhoenixLetterGrade grade,
        decimal tierValue)
    {
        var perChart = (double)tierValue / ChartsCounted;
        foreach (var level in DifficultyLevel.All.OrderBy(l => (int)l))
            if (scoring.GetScore(chartType, level, grade.GetMinimumScore(), PhoenixPlate.SuperbGame) >= perChart)
                return level;

        return null;
    }
}
