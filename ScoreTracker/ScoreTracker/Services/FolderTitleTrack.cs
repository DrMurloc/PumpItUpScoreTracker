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

    /// <summary>You'd need to score higher: "Get this folder to {grade}".</summary>
    GradeUp
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
    bool ServesAbove);

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

        // The one question, per grade: with the WHOLE folder at grade X, does the pool clear the
        // title? poolIf holds your non-folder charts fixed and drops the folder in at a flat value.
        var folderCount = allCharts.Values.Count(c => c.Type == folderType && (int)c.Level == (int)folderLevel);
        double PoolIf(double perChart) =>
            rest.Concat(Enumerable.Repeat(perChart, folderCount)).OrderByDescending(v => v).Take(50).Sum();

        // Beneath your top 50: even a perfect folder can't reach the title. Bar hides; serves stays.
        if (PoolIf(effBase * PgCeiling) < target.CompletionRequired)
            return new FolderTitleTrackResult(false, from?.Name.ToString() ?? "", target.Name.ToString(),
                progress, FolderTrackMode.GradeUp, 0, PhoenixLetterGrade.A, servesName, servesAbove);

        // How many charts at what grade. The floor is a PASS (A) — grinding a folder to a fail
        // means nothing — and we only name a higher grade when the folder is too small to reach
        // the title at A. Each chart at grade `per` evicts your weakest pool chart, netting
        // per − floor; count them against the deficit.
        var deficit = target.CompletionRequired - poolValue;
        var passFloor = config.LetterGradeModifiers[PhoenixLetterGrade.A];
        var fitGrade = PhoenixLetterGrade.SSSPlus;
        var fitPerChart = effBase * PgCeiling;
        var fitCount = folderCount;
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>()
                     .Where(g => config.LetterGradeModifiers[g] >= passFloor)
                     .OrderBy(g => config.LetterGradeModifiers[g]))
        {
            var perChart = effBase * config.LetterGradeModifiers[grade];
            if (perChart <= floor) continue;
            var count = (int)Math.Ceiling(deficit / (perChart - floor));
            if (count > folderCount) continue;
            fitGrade = grade;
            fitPerChart = perChart;
            fitCount = Math.Max(1, count);
            break;
        }

        // On pace when you already score at least that grade here (5+ charts for a stable median):
        // "~N more charts" at your own pace. Otherwise it's "pass N at {grade} or better". A grade
        // that fits always beats the floor, so median ≥ fitPerChart keeps the denominator positive.
        var median = folder.Count >= 5 ? Median(folder) : (double?)null;
        if (median is { } m && m > floor && m >= fitPerChart)
        {
            var onPace = (int)Math.Ceiling(deficit / (m - floor));
            return new FolderTitleTrackResult(true, from?.Name.ToString() ?? "", target.Name.ToString(),
                progress, FolderTrackMode.OnPace, Math.Max(1, onPace), fitGrade, servesName, servesAbove);
        }

        return new FolderTitleTrackResult(true, from?.Name.ToString() ?? "", target.Name.ToString(),
            progress, FolderTrackMode.GradeUp, fitCount, fitGrade, servesName, servesAbove);
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
