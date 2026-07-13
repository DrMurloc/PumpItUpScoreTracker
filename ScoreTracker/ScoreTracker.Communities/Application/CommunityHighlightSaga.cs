using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Materializes the community big-wins feed (docs/design/home-page-widgets.md §7). A SECOND
///     consumer of <see cref="ScoreHighlightsCapturedEvent" /> beside <c>CommunitySaga</c>'s Discord
///     card: classifies the batch's community-scoped big wins and writes one summary row per community
///     the winner belongs to. The population aggregates the classifier needs ride a per-mix memory
///     cache so the busy import path never recomputes them. Failure-isolated — a feed write must never
///     disturb the import pipeline (same contract as the recap saga).
/// </summary>
internal sealed class CommunityHighlightSaga : IConsumer<ScoreHighlightsCapturedEvent>
{
    private static readonly TimeSpan RarityCacheTtl = TimeSpan.FromHours(3);

    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly ICommunityHighlightRepository _highlights;
    private readonly ILogger<CommunityHighlightSaga> _logger;
    private readonly IScoreReader _scores;
    private readonly ITitleRepository _titles;

    public CommunityHighlightSaga(IChartRepository charts, IScoreReader scores, ITitleRepository titles,
        ICommunityHighlightRepository highlights, IMemoryCache cache, ILogger<CommunityHighlightSaga> logger)
    {
        _charts = charts;
        _scores = scores;
        _titles = titles;
        _highlights = highlights;
        _cache = cache;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ScoreHighlightsCapturedEvent> context)
    {
        var e = context.Message;
        if (e.Changes.Count == 0 && e.Milestones.Count == 0) return;

        try
        {
            var charts = (await _charts.GetCharts(e.Mix,
                    chartIds: e.Changes.Select(c => c.ChartId).Distinct(),
                    cancellationToken: context.CancellationToken))
                .ToDictionary(c => c.Id);

            var snapshot = await GetRaritySnapshot(e.Mix, context.CancellationToken);
            var wins = CommunityHighlightPolicy.Classify(e, charts, snapshot);
            if (wins.Count == 0) return;

            await _highlights.AddForUserCommunities(e.EventId, e.UserId, e.Mix, e.OccurredAt, e.SessionId, wins,
                context.CancellationToken);
        }
        catch (Exception ex)
        {
            // A dropped community-feed row is survivable; a disrupted import is not.
            _logger.LogWarning(ex, "Community highlight capture failed for user {UserId} on {Mix}", e.UserId, e.Mix);
        }
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
