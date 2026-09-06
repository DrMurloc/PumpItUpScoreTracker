using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>
///     The published PUMBILITY+ numbers: what the Rules page renders
///     (docs/design/march-of-murlocs.md §11.11). Every value is read off the configuration the
///     season cycle seats on a board, so the page and the ladder cannot disagree.
/// </summary>
public static class MoMRules
{
    /// <summary>1 hour 45 minutes: a chart counts if it starts inside it.</summary>
    public static TimeSpan Window => MoMScoring.Window;

    /// <summary>Song length scales a chart's value against this baseline.</summary>
    public static readonly TimeSpan LengthBaseline = TimeSpan.FromMinutes(2);

    /// <summary>Every level a chart can carry, 1 to 29.</summary>
    public static IReadOnlyList<int> Levels { get; } = DifficultyLevel.All.Select(l => (int)l).ToArray();

    /// <summary>What a chart at exactly its level pays: the base rating plus the stamina bonus.</summary>
    public static int LevelValue(MixEnum mix, int level)
    {
        return MoMScoring.For(mix).LevelRatings[DifficultyLevel.From(level)];
    }

    /// <summary>The stamina bonus stacked on a level from 22 up; nothing below.</summary>
    public static int LevelBonus(int level)
    {
        return MoMScoring.LevelBonus.TryGetValue(level, out var bonus) ? bonus : 0;
    }

    /// <summary>What a perfect 1,000,000 pays on top of the grade ladder.</summary>
    public static double PerfectGameMultiplier(MixEnum mix)
    {
        return MoMScoring.For(mix).PgLetterGradeModifier;
    }

    /// <summary>
    ///     The grade's multiplier under regular Phoenix PUMBILITY, the table every player knows:
    ///     the comparison row that shows what PUMBILITY+ changed.
    /// </summary>
    public static double RegularPumbilityMultiplier(PhoenixLetterGrade grade)
    {
        return ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false).LetterGradeModifierFor(grade, ChartType.Double);
    }

    /// <summary>What a perfect game pays under regular Phoenix PUMBILITY.</summary>
    public static double RegularPerfectGameMultiplier =>
        ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false).PgLetterGradeModifier;

    /// <summary>
    ///     The grade rungs from A to SSS+: the score each starts at on this mix and the
    ///     multiplier it pays there. Grades below A pay nothing.
    /// </summary>
    public static IReadOnlyList<MoMGradeRow> GradeRows(MixEnum mix)
    {
        var scoring = MoMScoring.For(mix);
        return Enum.GetValues<PhoenixLetterGrade>()
            .Where(grade => grade >= PhoenixLetterGrade.A)
            .OrderBy(grade => grade)
            .Select(grade => new MoMGradeRow(grade, (int)grade.GetMinimumScoreFor(mix),
                scoring.LetterGradeModifierFor(grade, ChartType.Double)))
            .ToArray();
    }

    /// <summary>
    ///     The multiplier a score earns on this mix: a straight line between rungs, nothing
    ///     below A, the perfect-game value at 1,000,000 (§2.8).
    /// </summary>
    public static double MultiplierAt(MixEnum mix, PhoenixScore score)
    {
        var scoring = MoMScoring.For(mix);
        var level = DifficultyLevel.From(20);
        return scoring.GetScore(ChartType.Double, level, score, PhoenixPlate.RoughGame) / scoring.LevelRatings[level];
    }

    /// <summary>The level a chart is priced at for a season: its folder level lifted by up to one, from how hard it is to score.</summary>
    public static double BalancedLevel(int folderLevel, double? scoringLevel)
    {
        return MoMScoring.BalancedLevel(folderLevel, scoringLevel);
    }
}

/// <summary>One rung of the grade ladder: the grade, the score it starts at on the mix, and its multiplier.</summary>
[ExcludeFromCodeCoverage]
public sealed record MoMGradeRow(PhoenixLetterGrade Grade, int FromScore, double Multiplier);
