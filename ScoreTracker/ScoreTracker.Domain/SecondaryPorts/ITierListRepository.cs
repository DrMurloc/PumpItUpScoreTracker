
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface ITierListRepository
    {
        Task SaveEntry(MixEnum mix, SongTierListEntry entry, CancellationToken cancellationToken);

        Task<IEnumerable<Guid>> GetUsersOnLevel(MixEnum mix, DifficultyLevel level,
            CancellationToken cancellationToken, bool requireActive = false);

        Task<IEnumerable<SongTierListEntry>> GetAllEntries(MixEnum mix, Name tierListName,
            CancellationToken cancellationToken);

        Task SaveEntries(MixEnum mix, IEnumerable<SongTierListEntry> entry, CancellationToken cancellationToken);
    }
}
