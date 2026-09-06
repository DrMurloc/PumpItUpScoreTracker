using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Identity.Application;

internal sealed class GetUsersByIdsHandler : IRequestHandler<GetUsersByIdsQuery, IReadOnlyList<User>>
{
    private readonly IUserRepository _users;

    public GetUsersByIdsHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<IReadOnlyList<User>> Handle(GetUsersByIdsQuery request, CancellationToken cancellationToken)
    {
        if (request.UserIds.Count == 0) return Array.Empty<User>();

        return (await _users.GetUsers(request.UserIds, cancellationToken)).ToArray();
    }
}
