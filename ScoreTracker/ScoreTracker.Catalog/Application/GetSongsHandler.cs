using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     Projected off the chart catalog rather than read separately: a song is in a mix exactly when
///     it carries a chart there, so the chart read already answers the question and a second query
///     would need its own definition of membership to disagree with.
/// </summary>
internal sealed class GetSongsHandler : IRequestHandler<GetSongsQuery, IReadOnlyList<SongRecord>>
{
    private readonly IChartRepository _charts;

    public GetSongsHandler(IChartRepository charts)
    {
        _charts = charts;
    }

    public async Task<IReadOnlyList<SongRecord>> Handle(GetSongsQuery request, CancellationToken cancellationToken)
    {
        var charts = await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken);
        return charts
            .GroupBy(c => c.Song.Name)
            .OrderBy(g => g.Key.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(g => SongRecord.From(g.First().Song))
            .ToArray();
    }
}
