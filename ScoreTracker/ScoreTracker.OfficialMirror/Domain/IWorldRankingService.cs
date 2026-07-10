using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain
{
    internal interface IWorldRankingService
    {
        Task CalculateWorldRankings(MixEnum mix, CancellationToken cancellationToken);

        Task<IEnumerable<RecordedPhoenixScore>> GetTop50(MixEnum mix, Name username, string type,
            CancellationToken cancellationToken);

        Task<IEnumerable<RecordedPhoenixScore>> GetAll(MixEnum mix, Name username,
            CancellationToken cancellationToken);
    }
}
