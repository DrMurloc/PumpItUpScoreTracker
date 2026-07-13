using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     The community big-wins capture step (docs/design/home-page-widgets.md §7): classify an event's
///     wins and persist a summary per the winner's communities. Shared by the live consumer and the
///     admin backfill, so both use the identical policy + cached population snapshots.
/// </summary>
internal interface ICommunityHighlightCapturer
{
    Task Capture(ScoreHighlightsCapturedEvent e, CancellationToken cancellationToken);
}

internal sealed class CommunityHighlightCapturer : ICommunityHighlightCapturer
{
    private static readonly TimeSpan RarityCacheTtl = TimeSpan.FromHours(3);

    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly ICommunityHighlightRepository _highlights;
    private readonly IScoreReader _scores;
    private readonly ITitleRepository _titles;

    public CommunityHighlightCapturer(IChartRepository charts, IScoreReader scores, ITitleRepository titles,
        ICommunityHighlightRepository highlights, IMemoryCache cache)
    {
        _charts = charts;
        _scores = scores;
        _titles = titles;
        _highlights = highlights;
        _cache = cache;
    }

    public async Task Capture(ScoreHighlightsCapturedEvent e, CancellationToken cancellationToken)
    {
        if (e.Changes.Count == 0 && e.Milestones.Count == 0) return;

        var charts = (await _charts.GetCharts(e.Mix,
                chartIds: e.Changes.Select(c => c.ChartId).Distinct(),
                cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        var snapshot = await GetRaritySnapshot(e.Mix, cancellationToken);
        var wins = CommunityHighlightPolicy.Classify(e, charts, snapshot);
        if (wins.Count == 0) return;

        await _highlights.AddForUserCommunities(e.EventId, e.UserId, e.Mix, e.OccurredAt, e.SessionId, wins,
            cancellationToken);
    }

    private async Task<RaritySnapshot> GetRaritySnapshot(MixEnum mix, CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync($"community-highlight-rarity:{mix}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RarityCacheTtl;
            var pgHolders = (await _scores.GetChartScoreAggregates(mix, cancellationToken))
                .ToDictionary(a => a.ChartId, a => a.PgCount);
            var activePlayers = (await _scores.GetActiveUserIds(mix, DateTimeOffset.MinValue, cancellationToken)).Count;
            var titleHolders = (await _titles.GetTitleAggregations(mix, cancellationToken))
                .GroupBy(t => t.Title.ToString())
                .ToDictionary(g => g.Key, g => g.First().Count);
            var titledUsers = await _titles.CountTitledUsers(cancellationToken);
            return new RaritySnapshot(pgHolders, activePlayers, titleHolders, titledUsers);
        }))!;
    }
}
