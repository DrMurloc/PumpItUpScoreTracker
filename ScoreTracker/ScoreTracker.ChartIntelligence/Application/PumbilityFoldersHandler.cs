using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Application;

internal sealed class PumbilityFoldersHandler
    : IRequestHandler<GetPumbilityFoldersQuery, IReadOnlyList<PumbilityFolderRecord>>
{
    private readonly TierListBlendBuilder _builder;
    private readonly IMemoryCache _cache;
    private readonly IPumbilityCensusRepository _census;
    private readonly ICurrentUserAccessor _currentUser;

    public PumbilityFoldersHandler(IMediator mediator, IChartRepository charts, IScoreProjector projector,
        IPumbilityCensusRepository census, ITitleRepository titles, IPlayerStatsReader playerStats,
        IScoreReader scores,
        ICurrentUserAccessor currentUser, IMemoryCache cache)
    {
        _builder = new TierListBlendBuilder(mediator, charts, projector, census, titles, playerStats, scores);
        _census = census;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<IReadOnlyList<PumbilityFolderRecord>> Handle(GetPumbilityFoldersQuery request,
        CancellationToken cancellationToken)
    {
        var userId = request.Personalized ? request.UserId ?? _currentUser.User.Id : (Guid?)null;
        var cacheKey = $"{nameof(PumbilityFoldersHandler)}_{request.Mix}_{userId?.ToString() ?? "community"}";
        if (_cache.TryGetValue<IReadOnlyList<PumbilityFolderRecord>>(cacheKey, out var cached) &&
            cached is { Count: > 0 })
            return cached;

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            // An empty answer is cached for a minute, not six hours. Empty almost always means
            // the census has not been built for this mix yet, and a six-hour hold turns "press
            // Rebuild" into "press Rebuild and wait until tomorrow" — with every folder in the
            // picker disabled in the meantime, since a folder with no census row is a folder
            // the lens cannot speak for.
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            // The cohort is per chart type on Phoenix 2, so both are asked and merged - a
            // folder is offered when the reader's cohort for THAT type can speak for it.
            var folders = new List<PumbilityFolderRecord>();
            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            {
                var cohortKey = userId == null
                    ? PumbilityCohortKeys.Community
                    : await _builder.ResolveViewerCohort(chartType, request.Mix, userId.Value,
                        cancellationToken);
                if (cohortKey == null) continue;
                folders.AddRange((await _census.GetFoldersWithData(request.Mix, cohortKey, cancellationToken))
                    .Where(f => f.ChartType == chartType)
                    .Select(f => new PumbilityFolderRecord(f.ChartType, f.Level)));
            }

            if (folders.Count == 0) entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return (IReadOnlyList<PumbilityFolderRecord>)folders;
        }) ?? Array.Empty<PumbilityFolderRecord>();
    }
}
