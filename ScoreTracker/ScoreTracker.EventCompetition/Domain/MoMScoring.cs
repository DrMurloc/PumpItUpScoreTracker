using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Domain;

/// <summary>
///     PUMBILITY+ as the season cycle seats it on a board (docs/design/march-of-murlocs.md §4, §5).
///     One builder for both mixes: Phoenix is <see cref="ScoringConfiguration.PumbilityPlus" />
///     verbatim plus the stamina bonus from 22 and time scaling, frozen since Winter 2025;
///     Phoenix 2 is the same structure graded on its own letter cutoffs with its own rows below
///     AAA+ (D41). A board prices one chart type, so <see cref="ForBoard" /> zeroes the other.
///     The published numbers in <c>Contracts.MoMRules</c> are read off these configurations,
///     never typed a second time.
/// </summary>
internal static class MoMScoring
{
    /// <summary>1 hour 45 minutes: the window a session's charts must start inside (§1).</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(45);

    /// <summary>
    ///     The stamina bonus stacked on the base rating from level 22. It grows faster than the
    ///     base does, which is what makes a hard chart worth more than two easy ones (§4, layer 3).
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int> LevelBonus = new Dictionary<int, int>
    {
        [22] = 50, [23] = 150, [24] = 300, [25] = 500, [26] = 750, [27] = 1050, [28] = 1400, [29] = 1800
    };

    /// <summary>The configuration a board of this mix is priced on, both chart types still open.</summary>
    public static ScoringConfiguration For(MixEnum mix)
    {
        // PumbilityPlus returns a fresh instance, so the MoM-only overrides below stay out of
        // PlayerRatingSaga's stored stat and the public v1 API (§9.5).
        var scoring = ScoringConfiguration.PumbilityPlus;
        scoring.AdjustToTime = true;
        foreach (var (level, bonus) in LevelBonus) scoring.LevelRatings[DifficultyLevel.From(level)] += bonus;

        if (mix == MixEnum.Phoenix2)
        {
            // Graded on Phoenix 2's own cutoffs (A from 800,000, AAA from 950,000). The two
            // Phoenix 2 pumbility rules stay off (§9.2): levels 1–9 keep their 10…90 and a
            // Single prices exactly as a Double does.
            scoring.Mix = MixEnum.Phoenix2;
            scoring.LetterGradeModifiers[PhoenixLetterGrade.APlus] = .70;
            scoring.LetterGradeModifiers[PhoenixLetterGrade.AA] = .80;
            scoring.LetterGradeModifiers[PhoenixLetterGrade.AAPlus] = .90;
            scoring.LetterGradeModifiers[PhoenixLetterGrade.AAAPlus] = 1.10;
        }

        return scoring;
    }

    /// <summary>The configuration for one board: every chart type but the board's pays nothing.</summary>
    public static ScoringConfiguration ForBoard(MixEnum mix, ChartType chartType)
    {
        var scoring = For(mix);
        foreach (var key in scoring.ChartTypeModifiers.Keys.ToArray())
        {
            if (key == chartType) continue;

            scoring.ChartTypeModifiers[key] = 0;
        }

        return scoring;
    }

    /// <summary>
    ///     The balanced level a chart is priced at for a season (§4, layer 4): the community
    ///     scoring level clamped to at most one level above the folder and never below the
    ///     folder's own + 0.5, which pays exactly the folder level's rating. A chart with no
    ///     scoring level sits at that floor.
    /// </summary>
    public static double BalancedLevel(int folderLevel, double? scoringLevel)
    {
        var floor = folderLevel + .5;
        return scoringLevel is { } level ? Math.Clamp(level, floor, folderLevel + 1.5) : floor;
    }
}
