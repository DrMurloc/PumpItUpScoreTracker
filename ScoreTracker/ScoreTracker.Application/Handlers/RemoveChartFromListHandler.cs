using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class RemoveChartFromListHandler : IRequestHandler<RemoveChartFromListCommand>
{
    private readonly IChartListRepository _chartLists;
    private readonly ICurrentUserAccessor _currentUser;

    public RemoveChartFromListHandler(ICurrentUserAccessor currentUser, IChartListRepository chartLists)
    {
        _currentUser = currentUser;
        _chartLists = chartLists;
    }

    public async Task<Unit> Handle(RemoveChartFromListCommand request, CancellationToken cancellationToken)
    {
        await _chartLists.RemoveChart(_currentUser.User.Id, request.ListType, request.ChartId, cancellationToken);

        return Unit.Value;
    }
}