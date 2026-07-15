using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.WeeklyChallenge.Contracts;

namespace ScoreTracker.WeeklyChallenge.Domain;

/// <summary>
///     Vertical-internal persistence port for the Daily Step board. Entries carry their
///     <see cref="DailyStepSource" /> (official import vs manual widget submission). The
///     cross-vertical slice — today's chart ids, consumed by the import Limbo hook — is the separate
///     published <see cref="ScoreTracker.Domain.SecondaryPorts.IDailyStepReader" />.
/// </summary>
internal interface IDailyStepRepository
{
    Task<DailyStepBoard?> GetCurrentChart(MixEnum mix, CancellationToken cancellationToken);
    Task RegisterDailyChart(MixEnum mix, DailyStepBoard board, CancellationToken cancellationToken);
    Task ClearBoard(MixEnum mix, CancellationToken cancellationToken);

    Task<IEnumerable<DailyStepEntry>> GetEntries(MixEnum mix, Guid? chartId,
        CancellationToken cancellationToken);

    Task SaveEntry(MixEnum mix, DailyStepEntry entry, CancellationToken cancellationToken);
    Task WriteHistories(MixEnum mix, IEnumerable<DailyStepPlacing> placings, CancellationToken cancellationToken);

    /// <summary>
    ///     The player's most recent finished days, newest first, with each day's board size —
    ///     the L6 history read (first surfaced by the challenges page).
    /// </summary>
    Task<IEnumerable<DailyStepHistoryRecord>> GetUserHistory(MixEnum mix, Guid userId, int take,
        CancellationToken cancellationToken);
}
