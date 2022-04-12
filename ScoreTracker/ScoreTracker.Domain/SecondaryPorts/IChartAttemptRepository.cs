using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartAttemptRepository
{
    Task<ChartAttempt?> GetBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default);

    Task SetBestAttempt(Guid userId, Chart chart, ChartAttempt attempt, DateTimeOffset recordedOn,
        CancellationToken cancellationToken = default);
}