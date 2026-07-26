using ScoreTracker.Domain.Models;
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
            .Select(folder => FolderLevelRecord.For(mix, folder.Key.Type,
                DifficultyLevel.From(folder.Key.Level), folder.ToArray(), scoresByChart))
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

    /// <summary>
    ///     The milestone a folder's movement earns, or null when it earned none.
    ///     <para>
    ///         Null when <paramref name="previous" /> is null — the seed-silently rule. A folder
    ///         being observed for the first time writes its row and announces nothing, which is
    ///         what stops a first import of a few thousand scores emitting a milestone per folder
    ///         (docs/design/folder-level-progression.md §5.3).
    ///     </para>
    ///     Only improvements count. A folder gaining charts pushes completion down, and a weak new
    ///     pass can nudge the average down; neither is news.
    /// </summary>
    public static PlayerMilestoneWrite? Diff(FolderLevelRecord? previous, FolderLevelRecord current,
        Guid? sessionId, DateTimeOffset occurredAt)
    {
        if (previous == null) return null;

        var tierMoved = current.Tier > previous.Tier;
        var gradeMoved = current.Grade != null && (previous.Grade == null || current.Grade > previous.Grade);
        if (!tierMoved && !gradeMoved) return null;

        var detail = new FolderProgressDetail(current.Folder, current.Tier, current.Grade,
            tierMoved ? previous.Tier : null,
            gradeMoved ? previous.Grade : null);
        return new PlayerMilestoneWrite(MilestoneKind.FolderProgress, sessionId, occurredAt,
            Detail: detail.Format());
    }

    /// <inheritdoc cref="FolderLevelRecord.PassedScores" />
    public static IReadOnlyDictionary<Guid, int> PassedScores(IEnumerable<RecordedPhoenixScore> bests) =>
        FolderLevelRecord.PassedScores(bests);
}
