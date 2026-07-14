using ScoreTracker.ScoreLedger.Contracts;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Ledger-internal port for the front door's volume reads. Implementations cache:
///     these serve the anonymous landing page, which must cost ~zero DB per hit
///     (docs/design/front-door.md D7).
/// </summary>
internal interface ILedgerStatsRepository
{
    Task<LedgerTotals> GetTotals(CancellationToken cancellationToken);

    /// <summary>
    ///     Journal rows per calendar day (UTC) from <paramref name="sinceUtc" />,
    ///     excluding backfill-seeded rows. Sparse — days with no activity are absent.
    /// </summary>
    Task<IReadOnlyList<LedgerDayVolume>> GetDailyVolumes(DateTimeOffset sinceUtc,
        CancellationToken cancellationToken);
}

internal sealed record LedgerTotals(long PhoenixRecords, long LegacyAttempts);
