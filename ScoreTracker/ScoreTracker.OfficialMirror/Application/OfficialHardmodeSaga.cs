using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     The Official Boards side of Hardmode (docs/design/hardmode-leaderboard.md §4): the board
///     players' three pool totals, repriced against the week's chart list.
///     <para>
///         It lives here because the pricing does: a board row carries a score and no plate, so
///         the plate has to be inferred, and that inference is the mirror's own. It reads the
///         list through <see cref="IHardmodeChartReader" /> rather than a ChartIntelligence
///         contract — this vertical sits at the other end of the reference chain.
///     </para>
/// </summary>
internal sealed class OfficialHardmodeSaga :
    IConsumer<HardmodeChartsRebuiltEvent>,
    IRequestHandler<GetOfficialHardmodeBoardQuery, IReadOnlyList<OfficialHardmodeRow>>
{
    private const int PoolSize = 50;

    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IHardmodeChartReader _hardmodeCharts;
    private readonly ILogger<OfficialHardmodeSaga> _logger;
    private readonly IOfficialPoolSource _pools;
    private readonly IOfficialHardmodeRatingRepository _ratings;

    public OfficialHardmodeSaga(IOfficialHardmodeRatingRepository ratings, IHardmodeChartReader hardmodeCharts,
        IOfficialPoolSource pools, IDateTimeOffsetAccessor clock, ILogger<OfficialHardmodeSaga> logger)
    {
        _ratings = ratings;
        _hardmodeCharts = hardmodeCharts;
        _pools = pools;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<HardmodeChartsRebuiltEvent> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;
        var qualifying = await _hardmodeCharts.GetQualifyingCharts(mix, cancellationToken);
        if (qualifying.Count == 0)
        {
            _logger.LogInformation("Official Hardmode ratings skipped for {Mix}: the list is empty", mix);
            return;
        }

        // Only the charts the census counted, priced by the mirror's own read — a board row
        // carries no plate, so the value is the one this vertical inferred, not one recomputed
        // from a list that holds no scores.
        var qualifyingIds = qualifying.Select(c => c.ChartId).ToHashSet();
        var rows = new List<OfficialHardmodeRating>();
        foreach (var player in await _pools.GetPricedPools(mix, cancellationToken))
        {
            var valued = player.Charts.Where(c => qualifyingIds.Contains(c.ChartId)).ToArray();
            if (valued.Length == 0) continue;
            var singles = valued.Where(v => v.ChartType == ChartType.Single).Select(v => v.Value).ToArray();
            var doubles = valued.Where(v => v.ChartType == ChartType.Double).Select(v => v.Value).ToArray();
            rows.Add(new OfficialHardmodeRating(player.OfficialPlayerId,
                Top(valued.Select(v => v.Value)), Top(singles), Top(doubles),
                Math.Min(PoolSize, valued.Length), Math.Min(PoolSize, singles.Length),
                Math.Min(PoolSize, doubles.Length)));
        }

        await _ratings.Replace(mix, rows, _clock.Now, cancellationToken);
        _logger.LogInformation("Official Hardmode ratings for {Mix}: {Players} board players", mix, rows.Count);
    }

    public Task<IReadOnlyList<OfficialHardmodeRow>> Handle(GetOfficialHardmodeBoardQuery request,
        CancellationToken cancellationToken)
    {
        return _ratings.GetBoard(request.Mix, request.Pool, cancellationToken);
    }

    private static double Top(IEnumerable<double> values)
    {
        return values.OrderByDescending(v => v).Take(PoolSize).Sum();
    }
}
