using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetSavedChartsHandler : IRequestHandler<GetSavedChartsQuery, IEnumerable<SavedChartRecord>>
{
    private readonly IChartListRepository _chartLists;
    private readonly ICurrentUserAccessor _currentUser;

    public GetSavedChartsHandler(ICurrentUserAccessor currentUser, IChartListRepository chartLists)
    {
        _currentUser = currentUser;
        _chartLists = chartLists;
    }

    public async Task<IEnumerable<SavedChartRecord>> Handle(GetSavedChartsQuery request,
        CancellationToken cancellationToken)
    {
        return await _chartLists.GetSavedChartsByUser(_currentUser.User.Id, cancellationToken);
    }
}