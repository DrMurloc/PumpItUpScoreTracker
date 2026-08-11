using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

using ScoreTracker.ScoreLedger.Contracts;

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
    /// <summary>
    ///     Every mix that can hold scores, oldest first by the Mix table's own SortOrder, with
    ///     this player's count in each. Counts span BOTH score tables: legacy mixes record in
    ///     BestAttempt and are just as deletable as Phoenix — there is nothing read-only about an
    ///     old mix.
    /// </summary>
    Task<IReadOnlyList<MixScoreCount>> GetMixesWithScores(Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes one chart's record — the undo case where no earlier play survives.</summary>
    Task DeleteRecord(MixEnum mix, Guid userId, Guid chartId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     How many of this player's records on a mix are failed runs rather than passes. A
    ///     record is broken only when they have no pass on that chart, so this is also the count
    ///     of charts that would go back to having no record at all.
    /// </summary>
    Task<int> CountBrokenRecords(MixEnum mix, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes every broken record on a mix and returns how many went. The journal is not
    ///     touched: the runs happened, and they stay in each chart's history — only their standing
    ///     as the record is withdrawn.
    /// </summary>
    Task<int> DeleteBrokenRecords(MixEnum mix, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user's records and per-score stats. Null mix means every mix.</summary>
    Task DeleteAllForUser(Guid userId, MixEnum? mix = null, CancellationToken cancellationToken = default);
}
