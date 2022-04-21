﻿using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartAttemptRepository
{
    Task<ChartAttempt?> GetBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default);
    Task RemoveBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default);

    Task SetBestAttempt(Guid userId, Chart chart, ChartAttempt attempt, DateTimeOffset recordedOn,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<BestChartAttempt>> GetBestAttempts(Guid userId, IEnumerable<Chart> charts,
        CancellationToken cancellationToken);

    Task<IEnumerable<BestChartAttempt>> GetBestAttempts(Guid userId, CancellationToken cancellationToken = default);
}