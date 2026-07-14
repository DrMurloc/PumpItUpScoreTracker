using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Identity.Application;

internal sealed class GetPlayerbaseStatsHandler : IRequestHandler<GetPlayerbaseStatsQuery, PlayerbaseCounts>
{
    private readonly IUserRepository _users;

    public GetPlayerbaseStatsHandler(IUserRepository users)
    {
        _users = users;
    }

    public Task<PlayerbaseCounts> Handle(GetPlayerbaseStatsQuery request, CancellationToken cancellationToken)
    {
        return _users.GetPlayerbaseCounts(cancellationToken);
    }
}
