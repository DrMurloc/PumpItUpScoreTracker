using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface ITitleRepository
    {
        Task SaveTitles(Guid userId, IEnumerable<Name> acquiredTitles, CancellationToken cancellationToken);

        Task SetHighestDifficultyTitle(Guid userId, Name title, DifficultyLevel level,
            CancellationToken cancellationToken);

        Task<IEnumerable<Name>> GetCompletedTitles(Guid userId, CancellationToken cancellationToken);
        Task<DifficultyLevel> GetCurrentTitleLevel(Guid userId, CancellationToken cancellationToken);
    }
}
