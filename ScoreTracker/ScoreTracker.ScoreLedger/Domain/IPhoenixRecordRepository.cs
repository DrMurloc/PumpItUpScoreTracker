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

    /// <summary>
    ///     Every player's full best on one chart — source and judgements included, unlike the
    ///     named read above — straight off the chart index. The caller narrows to the players it
    ///     may see; a several-hundred-GUID list is not something SQL Server should be asked to plan.
    /// </summary>
    Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> GetRecordedScoresForChart(MixEnum mix,
        Guid chartId, CancellationToken cancellationToken = default);

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
    ///     How many of this player's records on a mix are <em>imported</em> failed runs rather
    ///     than passes. A record is broken only when they have no pass on that chart, so this is
    ///     also the count of charts that would go back to having no record at all.
    ///     <para>
    ///         Manual and CSV breaks are excluded, and so are the null-Source rows that predate
    ///         capture — see <see cref="DeleteBrokenRecords" />. The count and the delete share
    ///         one predicate so the number on the button is the number that goes.
    ///     </para>
    /// </summary>
    Task<int> CountBrokenRecords(MixEnum mix, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes this player's imported broken records on a mix and returns how many went. The
    ///     journal is not touched: the runs happened, and they stay in each chart's history — only
    ///     their standing as the record is withdrawn.
    ///     <para>
    ///         <b>Imported only.</b> A hand-entered or CSV-uploaded break is the player's own
    ///         submission, and the cleanup's promise is that turning the setting back on and
    ///         importing again restores what it took — which is true of nothing a human typed. A
    ///         null Source predates capture, so its origin is unknown and it is left alone on the
    ///         same reasoning.
    ///     </para>
    /// </summary>
    Task<int> DeleteBrokenRecords(MixEnum mix, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user's records and per-score stats. Null mix means every mix.</summary>
    Task DeleteAllForUser(Guid userId, MixEnum? mix = null, CancellationToken cancellationToken = default);

    /// <summary>Every player holding at least one judged record in the mix — the backfill's work list.</summary>
    Task<IReadOnlyList<Guid>> GetUsersWithJudgedRecords(MixEnum mix, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes re-solved combos onto one player's records in one mix, keyed by chart, and drops
    ///     the player's cached scores so the next read carries them.
    /// </summary>
    Task SetMaxCombos(MixEnum mix, Guid userId, IReadOnlyList<(Guid ChartId, int? MaxCombo)> combos,
        CancellationToken cancellationToken = default);
}
