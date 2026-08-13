using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The tier-list blend, moved out of the page (tier-lists overhaul C2, design doc
///     §6 Tier 3): weighted combination of the stored tier lists — with, on the two
///     lenses that personalize, the score projection (Score) or the viewer's own
///     cohort's pool counts (PUMBILITY). The source computation lives in
///     <see cref="TierListBlendBuilder" /> (shared with the Personalized Breakdown
///     query); this handler owns lens validation, the final combine, and the cache.
/// </summary>
internal sealed class BlendedTierListHandler : IRequestHandler<GetBlendedTierListQuery, TierListResult>
{
    private readonly TierListBlendBuilder _builder;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserAccessor _currentUser;

    public BlendedTierListHandler(IMediator mediator, IChartRepository charts,
        ICurrentUserAccessor currentUser, IMemoryCache cache, IScoreProjector projector,
        ITierListRepository tierLists, ITitleRepository titles, IPlayerStatsReader playerStats,
        IScoreReader scores)
    {
        _builder = new TierListBlendBuilder(mediator, charts, projector, tierLists, titles, playerStats, scores);
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<TierListResult> Handle(GetBlendedTierListQuery request, CancellationToken cancellationToken)
    {
        var lens = request.Lens.ToString();
        if (!TierListBlendBuilder.IsKnownLens(lens))
            throw new ArgumentOutOfRangeException(nameof(request.Lens), lens, "Unknown tier list lens");

        var userId = request.Personalized ? request.UserId ?? _currentUser.User.Id : (Guid?)null;
        var cacheKey =
            $"{nameof(BlendedTierListHandler)}_{request.Mix}_{lens}_{request.ChartType}_{request.Level}_{userId?.ToString() ?? "community"}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            var computation = await _builder.Compute(request.ChartType, request.Level, lens, userId,
                request.Mix, cancellationToken);
            // An empty PUMBILITY answer is cached for a minute, not six hours — the same rule
            // PumbilityFoldersHandler applies, for the same reason: empty almost always means
            // the lists have not been built for this mix (or this viewer's cohort) yet, and a
            // six-hour hold turns "press Rebuild" into "press Rebuild and wait" for every
            // folder anyone viewed before pressing it.
            if (computation.Pumbility is { Entries.Count: 0 })
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            var entries = computation.FolderCharts
                .Select(c => TierListBlendBuilder.Combine("Final", c.Id, computation.Sources,
                    computation.Modifiers))
                .ToList();
            return new TierListResult(entries, computation.IsProvisionalFallback,
                computation.Projection?.PeerCount ?? computation.Pumbility?.CohortSize ?? 0,
                computation.Pumbility?.Appearances);
        }) ?? throw new InvalidOperationException("Blended tier list could not be built");
    }
}
