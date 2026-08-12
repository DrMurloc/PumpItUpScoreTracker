using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The folder's projected scores on their own, for surfaces that want to print the number
///     rather than rank by it. Same projector and same competitive window the personalized Score
///     list uses, so the two can never quote different figures for the same chart.
/// </summary>
internal sealed class ProjectedScoresHandler
    : IRequestHandler<GetProjectedScoresQuery, IReadOnlyDictionary<Guid, PhoenixScore>>
{
    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IScoreProjector _projector;

    public ProjectedScoresHandler(IChartRepository charts, IScoreProjector projector,
        ICurrentUserAccessor currentUser, IMemoryCache cache)
    {
        _charts = charts;
        _projector = projector;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<Guid, PhoenixScore>> Handle(GetProjectedScoresQuery request,
        CancellationToken cancellationToken)
    {
        var none = (IReadOnlyDictionary<Guid, PhoenixScore>)new Dictionary<Guid, PhoenixScore>();
        var userId = request.UserId ?? (_currentUser.IsLoggedIn ? _currentUser.User.Id : (Guid?)null);
        if (userId == null) return none;

        // Its own entry rather than a read of the blend's: this is asked for on views where no
        // blend has computed a projection, and the alternative is computing the sweep twice for
        // one folder. Same shape as the blend's cache, for the same reason — peers' play moving
        // under a six-hour-old answer is not something a reader can tell.
        var cacheKey =
            $"{nameof(ProjectedScoresHandler)}_{request.Mix}_{request.ChartType}_{request.Level}_{userId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            entry.SlidingExpiration = TimeSpan.FromHours(1);

            var folder = (await _charts.GetCharts(request.Mix, request.Level, request.ChartType,
                cancellationToken: cancellationToken)).ToArray();
            if (folder.Length == 0) return none;

            return (await _projector.Project(new ScoreProjectionRequest(request.Mix, request.ChartType,
                    userId.Value,
                    folder.Select(c => new ProjectionTarget(c.Id, (int)c.Level)).ToArray(),
                    TierListBlendBuilder.ProjectionCompetitiveWindow),
                cancellationToken)).Scores;
        }) ?? none;
    }
}
