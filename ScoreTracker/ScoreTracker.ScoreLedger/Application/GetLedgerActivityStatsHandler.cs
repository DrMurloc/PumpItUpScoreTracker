using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class GetLedgerActivityStatsHandler
    : IRequestHandler<GetLedgerActivityStatsQuery, LedgerActivityStats>
{
    /// <summary>Pulse window in days, today (UTC) inclusive.</summary>
    public const int PulseDays = 30;

    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ILedgerStatsRepository _stats;

    public GetLedgerActivityStatsHandler(ILedgerStatsRepository stats, IDateTimeOffsetAccessor clock)
    {
        _stats = stats;
        _clock = clock;
    }

    public async Task<LedgerActivityStats> Handle(GetLedgerActivityStatsQuery request,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.Now.UtcDateTime);
        var windowStart = today.AddDays(-(PulseDays - 1));
        var sinceUtc = new DateTimeOffset(windowStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var totals = await _stats.GetTotals(cancellationToken);
        var sparse = (await _stats.GetDailyVolumes(sinceUtc, cancellationToken))
            .ToDictionary(v => v.Day, v => v.Count);

        // Journal capture is younger than the 30-day window for its first weeks, so a
        // full window would front-load ~18 empty bars that read as "brand new". Start
        // the pulse at the first day that actually has activity (never before the 30-day
        // floor); once the trail is older than the window this is just windowStart.
        var firstDay = sparse.Count > 0 && sparse.Keys.Min() > windowStart
            ? sparse.Keys.Min()
            : windowStart;
        var dayCount = today.DayNumber - firstDay.DayNumber + 1;

        // Dense window: one entry per day, quiet days at zero, oldest first — the
        // page renders one bar per element without any date math of its own.
        var days = Enumerable.Range(0, dayCount)
            .Select(offset => firstDay.AddDays(offset))
            .Select(day => new LedgerDayVolume(day, sparse.GetValueOrDefault(day)))
            .ToArray();

        return new LedgerActivityStats(totals.PhoenixRecords, totals.LegacyAttempts, days);
    }
}
