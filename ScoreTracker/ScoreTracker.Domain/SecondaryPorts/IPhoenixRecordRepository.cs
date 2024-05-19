using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

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

    Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetAllPlayerScores(ChartType chartType,
        DifficultyLevel difficulty, CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartScoreAggregate>> GetMeaningfulScoresCount(ChartType chartType, DifficultyLevel difficulty,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<UserPhoenixScore>> GetPhoenixScores(IEnumerable<Guid> userIds, Guid chartId,
        CancellationToken cancellationToken = default);
}