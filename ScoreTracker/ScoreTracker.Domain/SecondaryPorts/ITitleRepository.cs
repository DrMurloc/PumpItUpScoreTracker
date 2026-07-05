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

        /// <summary>User ids whose highest difficulty title sits exactly on this level (tier-list cohorts).</summary>
        Task<IEnumerable<Guid>> GetUserIdsOnHighestLevel(MixEnum mix, DifficultyLevel level,
            CancellationToken cancellationToken);

        Task DeleteHighestTitle(MixEnum mix, Guid userId, CancellationToken cancellationToken);
    }
}
