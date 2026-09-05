using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Whether a Phoenix 2 folder has title progress for you — the gate behind the tier-list page's
///     pointer to the PUMBILITY page (pure, no I/O — unit-tested in ScoreTracker.Tests.Components).
///     It is exactly the test the retired folder-title track applied before drawing its bar
///     (docs/design/pumbility-title-track.md), kept so the pointer appears only where the bar did:
///     the two pooled types at level 10 and up, a rung still above your pool, and a folder that is
///     not beneath your top 50. What the track went on to compute — progress, the caption, the
///     "serves" rung — is the PUMBILITY page's job now.
/// </summary>
public static class FolderTitleTrack
{
    // A chart's ceiling is SSS+ on a Perfect Game plate, read from the shipped Phoenix2PumbilityScoring
    // config for the folder's own chart type rather than hand-copied, because Phoenix 2 prices some
    // rungs differently on Singles than on Doubles and a copied number cannot follow that.
    private static double PgCeilingFor(ScoringConfiguration config, ChartType type)
    {
        return config.LetterGradeModifierFor(PhoenixLetterGrade.SSSPlus, type)
               + config.PlateModifierFor(PhoenixPlate.PerfectGame, type);
    }

    public static bool HasTitleProgress(
        MixEnum mix, ChartType folderType, DifficultyLevel folderLevel,
        IDictionary<Guid, Chart> allCharts,
        IDictionary<Guid, RecordedPhoenixScore> scores)
    {
        // Phoenix 2 PUMBILITY titles only, and only the two pooled types — co-op never counts.
        if (mix != MixEnum.Phoenix2) return false;
        if (folderType is not (ChartType.Single or ChartType.Double)) return false;
        // Charts below level 10 price at zero in Phoenix 2 (ScoringConfiguration line 167), so a
        // sub-10 folder contributes nothing to the pool — there's no title progress to point at.
        if ((int)folderLevel < 10) return false;

        var pool = folderType == ChartType.Single ? PumbilityPool.Singles : PumbilityPool.Doubles;
        var ladder = Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>()
            .Where(t => t.Pool == pool)
            .OrderBy(t => t.CompletionRequired)
            .ToArray();
        if (ladder.Length == 0) return false;

        var config = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

        // Your per-chart contributions for this type. The pool is the best fifty and the floor is
        // the fiftieth — a chart helps only if it beats the floor.
        var contributions = new List<double>();
        foreach (var (chartId, score) in scores)
        {
            if (score.Score == null || !allCharts.TryGetValue(chartId, out var chart)) continue;
            if (chart.Type != folderType) continue;
            var value = config.GetScore(chart.Type, chart.Level, score.Score.Value,
                score.Plate ?? PhoenixPlate.RoughGame, score.IsBroken);
            if (value > 0) contributions.Add(value);
        }

        var poolSorted = contributions.OrderByDescending(v => v).ToArray();
        var poolValue = poolSorted.Take(50).Sum();
        var floor = poolSorted.Length >= 50 ? poolSorted[49] : 0.0;

        // Past the top rung there is nothing left to chase.
        if (ladder.All(t => t.CompletionRequired <= poolValue)) return false;

        // "Beneath your top 50" is the one true hide: a chart here can't crack your pool even maxed
        // out (SSS+ on a Perfect Game), so the folder is genuinely below your level. A small folder
        // ABOVE your level (high base, few charts) clears this test — it must not read as "behind
        // you" just because it is thin (the D28/D29 field-test bug). Singles price one level up.
        var effLevel = folderType == ChartType.Single
            ? Math.Min((int)folderLevel + 1, (int)DifficultyLevel.Max)
            : (int)folderLevel;
        var effBase = ScoringConfiguration.Phoenix2BaseRating(DifficultyLevel.From(effLevel));
        return effBase * PgCeilingFor(config, folderType) > floor;
    }
}
