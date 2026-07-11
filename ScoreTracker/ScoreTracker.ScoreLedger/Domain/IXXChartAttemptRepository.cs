using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Legacy (grade + broken + optional era-score) best attempts, one per
///     (user, chart, mix). Chart-taking methods key the mix off Chart.Mix — pass a
///     chart materialized for the mix being recorded (docs/design/legacy-mixes.md).
/// </summary>
internal interface IXXChartAttemptRepository
{
    Task<XXChartAttempt?> GetBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default);
    Task RemoveBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default);

    Task SetBestAttempt(Guid userId, Chart chart, XXChartAttempt attempt, DateTimeOffset recordedOn,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<BestXXChartAttempt>> GetBestAttempts(Guid userId, IEnumerable<Chart> charts,
        CancellationToken cancellationToken);

    Task<IEnumerable<BestXXChartAttempt>> GetBestAttempts(Guid userId, MixEnum mix,
        CancellationToken cancellationToken = default);

    Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default);
}