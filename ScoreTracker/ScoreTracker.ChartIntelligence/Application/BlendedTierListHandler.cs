using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The tier-list blend, moved out of the page (tier-lists overhaul C2, design doc
///     §6 Tier 3): weighted combination of the stored tier lists with, when
///     personalized, the player's skill estimates and the similar-players aggregation.
///     The source computation lives in <see cref="TierListBlendBuilder" /> (shared
///     with the Personalized Breakdown query); this handler owns lens validation,
///     the final combine, and the cache.
/// </summary>
internal sealed class BlendedTierListHandler : IRequestHandler<GetBlendedTierListQuery, TierListResult>
{
    private readonly TierListBlendBuilder _builder;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserAccessor _currentUser;

    public BlendedTierListHandler(IMediator mediator, IChartRepository charts, IScoreReader scores,
        IPlayerStatsReader playerStats, IUserTierListRepository userTierLists,
        ICurrentUserAccessor currentUser, IMemoryCache cache)
    {
        _builder = new TierListBlendBuilder(mediator, charts, scores, playerStats, userTierLists);
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
            var entries = computation.FolderCharts
                .Select(c => TierListBlendBuilder.Combine("Final", c.Id, computation.Sources,
                    computation.Modifiers))
                .ToList();
            return new TierListResult(entries, computation.IsProvisionalFallback);
        }) ?? throw new InvalidOperationException("Blended tier list could not be built");
    }
}
