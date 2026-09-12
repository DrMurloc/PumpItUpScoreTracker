using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface ITitleRepository
    {
        Task SaveTitles(MixEnum mix, Guid userId, IEnumerable<TitleAchievedRecord> acquiredTitles,
            CancellationToken cancellationToken);

        Task SetHighestDifficultyTitle(MixEnum mix, Guid userId, Name title, DifficultyLevel level,
            CancellationToken cancellationToken);

        Task<IEnumerable<TitleAchievedRecord>> GetCompletedTitles(MixEnum mix, Guid userId,
            CancellationToken cancellationToken);

        Task<DifficultyLevel> GetCurrentTitleLevel(MixEnum mix, Guid userId, CancellationToken cancellationToken);
        Task<IEnumerable<TitleAggregationRecord>> GetTitleAggregations(MixEnum mix, CancellationToken cancellationToken);
        Task<int> CountTitledUsers(CancellationToken cancellationToken);

        Task<IEnumerable<TitleAchievedRecord>> GetUsersWithTitle(MixEnum mix, Name title,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Holders of any of these titles, in one read. Used to place players on a ladder:
        ///     everyone who holds a rung, so a caller can tell who is standing on it from who
        ///     has already climbed past.
        /// </summary>
        Task<IEnumerable<TitleAchievedRecord>> GetUsersWithTitles(MixEnum mix, IEnumerable<Name> titles,
            CancellationToken cancellationToken);

        /// <summary>User ids whose highest difficulty title sits exactly on this level (tier-list cohorts).</summary>
        Task<IEnumerable<Guid>> GetUserIdsOnHighestLevel(MixEnum mix, DifficultyLevel level,
            CancellationToken cancellationToken);

        /// <summary>
        ///     The players whose highest difficulty title is this one — Phoenix 1's cohort for the
        ///     Breakdown card (docs/design/pumbility-overhaul.md D68). Distinct from
        ///     <see cref="GetUsersWithTitle" />, which answers everyone who has ever earned it.
        /// </summary>
        Task<IEnumerable<Guid>> GetUserIdsWithHighestTitle(MixEnum mix, Name title,
            CancellationToken cancellationToken);

        /// <summary>
        ///     The viewer's own highest difficulty title, which is the band the Breakdown card reads
        ///     Phoenix 1's cohort for (docs/design/pumbility-overhaul.md D68). Null when they have
        ///     never earned one.
        /// </summary>
        Task<Name?> GetHighestTitle(MixEnum mix, Guid userId, CancellationToken cancellationToken);

        Task DeleteHighestTitle(MixEnum mix, Guid userId, CancellationToken cancellationToken);
    }
}
