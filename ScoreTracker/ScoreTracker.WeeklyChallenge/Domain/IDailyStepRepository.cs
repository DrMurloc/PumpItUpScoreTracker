using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.WeeklyChallenge.Contracts;

namespace ScoreTracker.WeeklyChallenge.Domain;

/// <summary>
///     Vertical-internal persistence port for the Daily Step board. Reuses Weekly's
///     <see cref="WeeklyTournamentEntry" /> for entries (identical shape). The cross-vertical slice
///     — today's chart ids, consumed by the import Limbo hook — is the separate published
///     <see cref="ScoreTracker.Domain.SecondaryPorts.IDailyStepReader" />.
/// </summary>
internal interface IDailyStepRepository
{
    Task<DailyStepBoard?> GetCurrentChart(MixEnum mix, CancellationToken cancellationToken);
    Task RegisterDailyChart(MixEnum mix, DailyStepBoard board, CancellationToken cancellationToken);
    Task ClearBoard(MixEnum mix, CancellationToken cancellationToken);

    Task<IEnumerable<WeeklyTournamentEntry>> GetEntries(MixEnum mix, Guid? chartId,
        CancellationToken cancellationToken);

    Task SaveEntry(MixEnum mix, WeeklyTournamentEntry entry, CancellationToken cancellationToken);
    Task WriteHistories(MixEnum mix, IEnumerable<DailyStepPlacing> placings, CancellationToken cancellationToken);
}
