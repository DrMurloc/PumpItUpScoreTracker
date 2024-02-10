using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Services.Contracts
{
    public interface IWorldRankingService
    {
        Task CalculateWorldRankings(CancellationToken cancellationToken);

        Task<IEnumerable<RecordedPhoenixScore>> GetTop50(Name username, string type,
            CancellationToken cancellationToken);
    }
}
