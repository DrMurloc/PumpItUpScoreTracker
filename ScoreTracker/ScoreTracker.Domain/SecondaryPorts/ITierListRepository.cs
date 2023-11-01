
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface ITierListRepository
    {
        Task SaveEntry(SongTierListEntry entry, CancellationToken cancellationToken);
        Task<IEnumerable<SongTierListEntry>> GetAllEntries(Name tierListName, CancellationToken cancellationToken);
    }
}
 