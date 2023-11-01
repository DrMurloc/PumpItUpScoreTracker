
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IOfficialSiteClient
    {
        Task<IEnumerable<SongTierListEntry>> GetScoresLeaderboard(CancellationToken cancellationToken);
    }
}
