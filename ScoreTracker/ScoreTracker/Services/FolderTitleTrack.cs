using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <summary>How the track's one caption reads for the folder you're standing in.</summary>
public enum FolderTrackMode
{
    /// <summary>Your grade here already clears it — just play more: "~N more charts in this folder".</summary>
    OnPace,

    /// <summary>You'd need to score higher: "Pass N charts in this folder with {grade} or better".</summary>
    GradeUp,

    /// <summary>
    ///     The folder is above your level: a chart here helps, but even the whole folder maxed can't
    ///     finish your next title on its own. Names the count at the cheapest contributing grade plus
    ///     the folder's real size — "Pass N charts at A or better (only M exist in this folder)".
    /// </summary>
    Reach
}

/// <summary>
///     The render-ready read of one folder against your Phoenix 2 PUMBILITY title ladder — the
///     tier-list-page track that replaces the Phoenix 1 folder-title bars. <see cref="Show" /> is
///     false when the folder is beneath your top 50 (even a perfect clear falls short); the bar
///     hides but <see cref="ServesTitle" /> still whispers where the folder sits.
/// </summary>
public sealed record FolderTitleTrackResult(
    bool Show,
    string FromTitle,
    string TargetTitle,
    double Progress,
    FolderTrackMode Mode,
    int ChartsLeft,
    PhoenixLetterGrade NeededGrade,
    string? ServesTitle,
    bool ServesAbove,
    int FolderChartCount);

/// <summary>
///     Pure per-folder title read for Phoenix 2 (no I/O — unit-tested in ScoreTracker.Tests). Turns
///     your scores + the catalog into the track's state: which title you're chasing, the rung-to-rung
///     progress, and the single actionable caption. Singles and doubles each gate on their own
///     top-50 pool; a chart's "floor" is your 50th-best contribution in that pool. See
///     docs/design/pumbility-title-track.md for the model.
/// </summary>
public static class FolderTitleTrack
{
    // The "serves" yardstick is an AA (925k) clear; a chart's ceiling is SSS+ (1.50) on a Perfect
    // Game plate (+0.020). Both come straight from the shipped Phoenix2PumbilityScoring config.
    private const double AaModifier = 1.36;
    private const double PgCeiling = 1.52;

    public static FolderTitleTrackResult? Compute(
        MixEnum mix, ChartType folderType, DifficultyLevel folderLevel,
        IDictionary<Guid, Chart> allCharts,
        IDictionary<Guid, RecordedPhoenixScore> scores)
    {
        // Phoenix 2 PUMBILITY titles only, and only the two pooled types — co-op never counts.
        if (mix != MixEnum.Phoenix2) return null;
        if (folderType is not (ChartType.Single or ChartType.Double)) return null;
        // Charts below level 10 price at zero in Phoenix 2 (ScoringConfiguration line 167), so a
        // sub-10 folder contributes nothing to the pool — there's no title progress to show.
        if ((int)folderLevel < 10) return null;

        var pool = folderType == ChartType.Single ? PumbilityPool.Singles : PumbilityPool.Doubles;
        var ladder = Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>()
            .Where(t => t.Pool == pool)
            .OrderBy(t => t.CompletionRequired)
            .ToArray();
        if (ladder.Length == 0) return null;

        var config = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

        // Per-chart contributions for this type, split into this folder vs the rest of the pool.
        var folder = new List<double>();
        var rest = new List<double>();
        foreach (var (chartId, score) in scores)
        {
            if (score.Score == null || !allCharts.TryGetValue(chartId, out var chart)) continue;
            if (chart.Type != folderType) continue;
            var value = config.GetScore(chart.Type, chart.Level, score.Score.Value,
                score.Plate ?? PhoenixPlate.RoughGame, score.IsBroken);
            if (value <= 0) continue;
            if ((int)chart.Level == (int)folderLevel) folder.Add(value);
            else rest.Add(value);
        }

        var poolSorted = rest.Concat(folder).OrderByDescending(v => v).ToArray();
        var poolValue = poolSorted.Take(50).Sum();
        var floor = poolSorted.Length >= 50 ? poolSorted[49] : 0.0;

        // The title you're chasing is the first rung above your pool; the one below it is the floor
        // the bar measures from. Past the top rung there's nothing to track.
        var targetIndex = Array.FindIndex(ladder, t => t.CompletionRequired > poolValue);
        if (targetIndex < 0) return null;
        var target = ladder[targetIndex];
        var from = targetIndex > 0 ? ladder[targetIndex - 1] : null;
        var floorThreshold = from?.CompletionRequired ?? 0;
        var span = target.CompletionRequired - floorThreshold;
        var progress = span <= 0 ? 0 : Math.Clamp((poolValue - floorThreshold) / (double)span, 0, 1);

        // "serves" — the rung a folder of AA-clears lands on, and whether it outranks your target.
        var effLevel = folderType == ChartType.Single
            ? Math.Min((int)folderLevel + 1, (int)DifficultyLevel.Max)
            : (int)folderLevel;
        var effBase = ScoringConfiguration.Phoenix2BaseRating(DifficultyLevel.From(effLevel));
        var servesPoolValue = 50.0 * effBase * AaModifier;
        var servesTitle = ladder.LastOrDefault(t => t.CompletionRequired <= servesPoolValue);
        var servesName = servesTitle?.Name.ToString();
        var servesAbove = servesTitle != null && servesTitle.CompletionRequired > target.CompletionRequired;

        // "Beneath your top 50" is the one true hide: a chart here can't crack your pool even maxed
        // out (SSS+ on a Perfect Game), so the folder is genuinely below your level. A small folder
        // ABOVE your level (high base, few charts) clears this test and keeps its bar — it must not
        // read as "behind you" just because it's too thin to finish a title single-handed.
        var folderCount = allCharts.Values.Count(c => c.Type == folderType && (int)c.Level == (int)folderLevel);
        if (effBase * PgCeiling <= floor)
            return new FolderTitleTrackResult(false, from?.Name.ToString() ?? "", target.Name.ToString(),
                progress, FolderTrackMode.GradeUp, 0, PhoenixLetterGrade.A, servesName, servesAbove, folderCount);

        // How many charts at what grade close the gap to the title. Take the grades that both clear a
        // pass (A — grinding to a fail means nothing) and actually beat your floor, cheapest first.
        // Each chart at grade `per` evicts your weakest pool chart, netting per − floor.
        var deficit = target.CompletionRequired - poolValue;
        var passFloor = config.LetterGradeModifiers[PhoenixLetterGrade.A];
        var contributing = Enum.GetValues<PhoenixLetterGrade>()
            .Where(g => config.LetterGradeModifiers[g] >= passFloor)
            .OrderBy(g => config.LetterGradeModifiers[g])
            .Select(g => (grade: g, per: effBase * config.LetterGradeModifiers[g]))
            .Where(x => x.per > floor)
            .ToArray();

        // The cheapest grade that still beats your floor is the baseline "or better" ask. If even
        // SSS+ on grade alone can't beat it, only a Perfect Game plate can — fall to SSS+ on the ceiling.
        var (baseGrade, basePer) = contributing.Length > 0
            ? contributing[0]
            : (PhoenixLetterGrade.SSSPlus, effBase * PgCeiling);
        var baseCount = Math.Max(1, (int)Math.Ceiling(deficit / (basePer - floor)));

        // The first grade whose full-folder count fits the folder is the achievable ask — we only
        // name a higher grade when a lower one would need more charts than the folder holds.
        var fitGrade = baseGrade;
        var fitPerChart = basePer;
        var fitCount = baseCount;
        var fits = false;
        foreach (var (grade, per) in contributing)
        {
            var count = (int)Math.Ceiling(deficit / (per - floor));
            if (count > folderCount) continue;
            fitGrade = grade;
            fitPerChart = per;
            fitCount = Math.Max(1, count);
            fits = true;
            break;
        }

        // A chart here helps, but even the whole folder maxed can't finish the title on its own — a
        // folder above your level. Name the count at the cheapest contributing grade and the folder's
        // real size, so "only N exist" carries why this folder alone can't get you there.
        if (!fits)
            return new FolderTitleTrackResult(true, from?.Name.ToString() ?? "", target.Name.ToString(),
                progress, FolderTrackMode.Reach, baseCount, baseGrade, servesName, servesAbove, folderCount);

        // On pace when you already score at least that grade here (5+ charts for a stable median):
        // "~N more charts" at your own pace. Otherwise it's "pass N at {grade} or better". A grade
        // that fits always beats the floor, so median ≥ fitPerChart keeps the denominator positive.
        var median = folder.Count >= 5 ? Median(folder) : (double?)null;
        if (median is { } m && m > floor && m >= fitPerChart)
        {
            var onPace = (int)Math.Ceiling(deficit / (m - floor));
            return new FolderTitleTrackResult(true, from?.Name.ToString() ?? "", target.Name.ToString(),
                progress, FolderTrackMode.OnPace, Math.Max(1, onPace), fitGrade, servesName, servesAbove, folderCount);
        }

        return new FolderTitleTrackResult(true, from?.Name.ToString() ?? "", target.Name.ToString(),
            progress, FolderTrackMode.GradeUp, fitCount, fitGrade, servesName, servesAbove, folderCount);
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
