using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ScoreLedger.Domain;

internal interface IXXChartAttemptRepository
{
    Task<XXChartAttempt?> GetBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default);
    Task RemoveBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default);

    Task SetBestAttempt(Guid userId, Chart chart, XXChartAttempt attempt, DateTimeOffset recordedOn,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<BestXXChartAttempt>> GetBestAttempts(Guid userId, IEnumerable<Chart> charts,
        CancellationToken cancellationToken);

    Task<IEnumerable<BestXXChartAttempt>> GetBestAttempts(Guid userId, CancellationToken cancellationToken = default);

    Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default);
}