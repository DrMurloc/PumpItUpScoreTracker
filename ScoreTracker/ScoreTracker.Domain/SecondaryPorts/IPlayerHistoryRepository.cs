using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IPlayerHistoryRepository
{
    Task WriteHistory(MixEnum mix, PlayerRatingRecord record, CancellationToken cancellationToken);

    /// <summary>
    ///     Every history row for a set of players, oldest first per player. One query for a
    ///     whole cohort: the PUMBILITY estimator needs each peer's competitive level at the
    ///     moment they set a score, and asking per player would be N round trips for what is
    ///     ~27 narrow rows each (docs/design/pumbility-overhaul.md §6.3).
    /// </summary>
    Task<IEnumerable<PlayerRatingRecord>> GetHistory(MixEnum mix, IEnumerable<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>Account-level wipe: clears the user's history across every mix.</summary>
    Task DeleteHistoryForUser(Guid userId, CancellationToken cancellationToken);
}