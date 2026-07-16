using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain
{
    // Read side only: the stored world-ranking recalculation retired with the snapshot
    // model (the hub computes rankings live from the latest sealed snapshot). These reads
    // serve the legacy pages until the hub replaces them.
    internal interface IWorldRankingService
    {
        Task<IEnumerable<RecordedPhoenixScore>> GetTop50(MixEnum mix, Name username, string type,
            CancellationToken cancellationToken);

        Task<IEnumerable<RecordedPhoenixScore>> GetAll(MixEnum mix, Name username,
            CancellationToken cancellationToken);
    }
}
