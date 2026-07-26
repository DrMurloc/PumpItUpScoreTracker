using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     Turns a folder roster plus a player's best scores into their folder standings
///     (docs/design/folder-level-progression.md §2.1). Pure: the roster and the scores both
///     arrive as arguments, so the same code serves the import pipeline and the backfill.
/// </summary>
internal static class FolderLevelCalculator
{
    /// <summary>
    ///     One record per folder in <paramref name="charts" />, including folders the player has
    ///     not touched — a 0% folder is a real standing, and the caller decides whether to store it.
    ///     Charts absent from <paramref name="scoresByChart" /> count toward the folder's size but
    ///     never toward its average.
    /// </summary>
    public static IReadOnlyList<FolderLevelRecord> Compute(MixEnum mix, IEnumerable<Chart> charts,
        IReadOnlyDictionary<Guid, int> scoresByChart)
    {
        return charts
            .GroupBy(c => (c.Type, Level: (int)c.Level))
            .Select(folder =>
            {
                var scores = folder
                    .Select(c => scoresByChart.TryGetValue(c.Id, out var score) ? score : (int?)null)
                    .Where(s => s != null)
                    .Select(s => s!.Value)
                    .ToArray();

                return new FolderLevelRecord(mix, folder.Key.Type, DifficultyLevel.From(folder.Key.Level),
                    folder.Count(), scores.Length, Average(scores));
            })
            .ToArray();
    }

    /// <summary>
    ///     The single folder <paramref name="type" />/<paramref name="level" /> names, or null when
    ///     the roster holds no such folder. The import pipeline recomputes only the folders a batch
    ///     touched, so it asks one at a time.
    /// </summary>
    public static FolderLevelRecord? ComputeOne(MixEnum mix, ChartType type, DifficultyLevel level,
        IEnumerable<Chart> charts, IReadOnlyDictionary<Guid, int> scoresByChart)
    {
        var folder = charts.Where(c => c.Type == type && (int)c.Level == (int)level).ToArray();
        if (folder.Length == 0) return null;
        return Compute(mix, folder, scoresByChart).SingleOrDefault();
    }

    // Rounded to the nearest point rather than truncated: an average is a display number here,
    // and truncation would drop a folder sitting exactly on a grade floor one rung.
    private static int Average(IReadOnlyCollection<int> scores) =>
        scores.Count == 0 ? 0 : (int)Math.Round(scores.Sum(s => (long)s) / (double)scores.Count,
            MidpointRounding.AwayFromZero);
}
