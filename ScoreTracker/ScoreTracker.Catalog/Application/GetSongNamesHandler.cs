using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Catalog.Application;

internal sealed class GetSongNamesHandler : IRequestHandler<GetSongNamesQuery, IEnumerable<Name>>
{
    private readonly IChartRepository _charts;

    public GetSongNamesHandler(IChartRepository charts)
    {
        _charts = charts;
    }

    public async Task<IEnumerable<Name>> Handle(GetSongNamesQuery request, CancellationToken cancellationToken)
    {
        return await _charts.GetSongNames(request.Mix, cancellationToken);
    }
}