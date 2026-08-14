using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     The significant-win capture step: classify an event's wins and persist the summary once,
///     keyed by the event. Shared by the live consumer and the admin backfill, so both use the
///     identical policy + cached population snapshots.
///     <para>
///         Audiences are somebody else's problem now. This writes the payload and announces it;
///         Communities indexes it against its member sets, and Rivals fans in on read
///         (docs/design/rivals.md §2.4).
///     </para>
/// </summary>
internal interface IPlayerHighlightCapturer
{
    Task Capture(ScoreHighlightsCapturedEvent e, CancellationToken cancellationToken);
}

internal sealed class PlayerHighlightCapturer : IPlayerHighlightCapturer
{
    private static readonly TimeSpan RarityCacheTtl = TimeSpan.FromHours(3);

    private readonly IBus _bus;
    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly IPlayerHighlightRepository _highlights;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;

    public PlayerHighlightCapturer(IChartRepository charts, IScoreReader scores,
        IPlayerHighlightRepository highlights, IPlayerStatsReader playerStats, IMemoryCache cache, IBus bus)
    {
        _charts = charts;
        _scores = scores;
        _highlights = highlights;
        _playerStats = playerStats;
        _cache = cache;
        _bus = bus;
    }

    public async Task Capture(ScoreHighlightsCapturedEvent e, CancellationToken cancellationToken)
    {
        if (e.Changes.Count == 0 && e.Milestones.Count == 0) return;

        var charts = (await _charts.GetCharts(e.Mix,
                chartIds: e.Changes.Select(c => c.ChartId).Distinct(),
                cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        var snapshot = await GetRaritySnapshot(e.Mix, cancellationToken);
        var stats = await _playerStats.GetStats(e.Mix, e.UserId, cancellationToken);
        var wins = PlayerHighlightPolicy.Classify(e, charts, snapshot, stats);
        if (wins.Count == 0) return;

        var stored = await _highlights.Add(e.EventId, e.UserId, e.Mix, e.OccurredAt, e.SessionId, wins,
            cancellationToken);

        // Only on a genuinely new row: a redelivery that re-announced would have every audience
        // re-index the same event, and the announcement is what an audience acts on.
        if (stored)
            await _bus.Publish(new PlayerHighlightsStoredEvent(e.EventId, e.UserId, e.Mix, e.OccurredAt),
                cancellationToken);
    }

    private async Task<RaritySnapshot> GetRaritySnapshot(MixEnum mix, CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync($"player-highlight-rarity:{mix}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RarityCacheTtl;
            var pgHolders = (await _scores.GetChartScoreAggregates(mix, cancellationToken))
                .ToDictionary(a => a.ChartId, a => a.PgCount);
            var activePlayers = (await _scores.GetActiveUserIds(mix, DateTimeOffset.MinValue, cancellationToken)).Count;
            return new RaritySnapshot(pgHolders, activePlayers);
        }))!;
    }
}
