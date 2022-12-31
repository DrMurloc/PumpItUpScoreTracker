using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class SaveChartToListHandler : IRequestHandler<SaveChartToListCommand>
{
    private readonly IChartListRepository _chartLists;
    private readonly ICurrentUserAccessor _currentUser;

    public SaveChartToListHandler(ICurrentUserAccessor currentUser, IChartListRepository chartLists)
    {
        _currentUser = currentUser;
        _chartLists = chartLists;
    }

    public async Task<Unit> Handle(SaveChartToListCommand request, CancellationToken cancellationToken)
    {
        await _chartLists.SaveChart(_currentUser.User.Id, request.ListType, request.ChartId, cancellationToken);

        return Unit.Value;
    }
}