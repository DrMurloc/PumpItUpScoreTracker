using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface ITitleRepository
    {
        Task SaveTitles(Guid userId, IEnumerable<Name> acquiredTitles, CancellationToken cancellationToken);
        Task<IEnumerable<Name>> GetCompletedTitles(Guid userId, CancellationToken cancellationToken);
    }
}
