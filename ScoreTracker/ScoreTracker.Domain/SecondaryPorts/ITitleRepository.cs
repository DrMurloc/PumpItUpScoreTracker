using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface ITitleRepository
    {
        Task SaveTitles(Guid userId, IEnumerable<TitleAchievedRecord> acquiredTitles,
            CancellationToken cancellationToken);

        Task SetHighestDifficultyTitle(Guid userId, Name title, DifficultyLevel level,
            CancellationToken cancellationToken);

        Task<IEnumerable<TitleAchievedRecord>> GetCompletedTitles(Guid userId, CancellationToken cancellationToken);
        Task<DifficultyLevel> GetCurrentTitleLevel(Guid userId, CancellationToken cancellationToken);
        Task<IEnumerable<TitleAggregationRecord>> GetTitleAggregations(CancellationToken cancellationToken);
        Task<int> CountTitledUsers(CancellationToken cancellationToken);
        Task<IEnumerable<TitleAchievedRecord>> GetUsersWithTitle(Name title, CancellationToken cancellationToken);

        /// <summary>User ids whose highest difficulty title sits exactly on this level (tier-list cohorts).</summary>
        Task<IEnumerable<Guid>> GetUserIdsOnHighestLevel(DifficultyLevel level, CancellationToken cancellationToken);

        Task DeleteHighestTitle(Guid userId, CancellationToken cancellationToken);
    }
}
