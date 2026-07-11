using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

internal interface IUserTierListRepository
{
    /// <summary>
    ///     Replaces one user's materialized relative tier list for a folder: the given
    ///     entries are upserted and the user's stale rows for the folder's other charts
    ///     are removed. Idempotent — replaying a score event converges on the same rows.
    /// </summary>
    Task SaveUserFolder(MixEnum mix, Guid userId, IReadOnlyCollection<Guid> folderChartIds,
        IEnumerable<SongTierListEntry> entries, CancellationToken cancellationToken);

    /// <summary>
    ///     Every user's materialized categories for a set of charts (one folder) — the
    ///     set-based read that replaces the similar-players per-user query fan-out.
    /// </summary>
    Task<IEnumerable<UserTierListEntryRecord>> GetEntriesForCharts(MixEnum mix, IEnumerable<Guid> chartIds,
        CancellationToken cancellationToken);
}

internal sealed record UserTierListEntryRecord(Guid UserId, Guid ChartId, TierListCategory Category, int Order);
