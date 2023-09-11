using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IPhoenixRecordRepository
{
    Task UpdateBestAttempt(Guid userId, RecordedPhoenixScore score, CancellationToken cancellationToken = default);

    Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecordedPhoenixScore?> GetRecordedScore(Guid userId, Guid chartId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<UserPhoenixScore>> GetRecordedUserScores(Guid chartId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartScoreAggregate>> GetAllChartScoreAggregates(CancellationToken cancellationToken);
}