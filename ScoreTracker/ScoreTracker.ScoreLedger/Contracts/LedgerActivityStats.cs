namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     Ledger volume for the front door. <see cref="DailyVolumes" /> is dense: exactly
///     one entry per day of the pulse window, oldest first, quiet days at zero — the
///     page renders one bar per element without date math.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LedgerActivityStats(
    long PhoenixRecordCount,
    long LegacyAttemptCount,
    IReadOnlyList<LedgerDayVolume> DailyVolumes)
{
    /// <summary>The headline number: best attempts across both score models.</summary>
    public long TotalRecords => PhoenixRecordCount + LegacyAttemptCount;
}

/// <summary>Scores recorded on one calendar day (UTC), backfill excluded.</summary>
[ExcludeFromCodeCoverage]
public sealed record LedgerDayVolume(DateOnly Day, int Count);
