using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Domain;

internal interface IPhoenixRecordRepository
{
    Task UpdateBestAttempt(MixEnum mix, Guid userId, RecordedPhoenixScore score,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(MixEnum mix, Guid userId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<(Guid UserId, Guid ChartId)>> GetPgUsers(MixEnum mix, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(MixEnum mix, IEnumerable<Guid> userIds,
        ChartType chartType,
        DifficultyLevel minimumLevel,
        DifficultyLevel maximumLevel,
        CancellationToken cancellationToken);

    Task<RecordedPhoenixScore?> GetRecordedScore(MixEnum mix, Guid userId, Guid chartId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<UserPhoenixScore>> GetRecordedUserScores(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartScoreAggregate>> GetAllChartScoreAggregates(MixEnum mix,
        CancellationToken cancellationToken);

    Task<IEnumerable<UserPhoenixScore>> GetPlayerScores(MixEnum mix, IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken = default);

    Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetPlayerScores(MixEnum mix,
        IEnumerable<Guid> userIds,
        ChartType chartType,
        DifficultyLevel difficulty, CancellationToken cancellationToken = default);

    Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetAllPlayerScores(MixEnum mix, ChartType chartType,
        DifficultyLevel difficulty, CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartScoreAggregate>> GetMeaningfulScoresCount(MixEnum mix, ChartType chartType,
        DifficultyLevel difficulty,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<UserPhoenixScore>> GetPhoenixScores(MixEnum mix, IEnumerable<Guid> userIds, Guid chartId,
        CancellationToken cancellationToken = default);

    Task<int> GetClearCount(MixEnum mix, Guid userId, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default);

    // Account purge spans mixes by design — no mix parameter.
    Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default);
}
