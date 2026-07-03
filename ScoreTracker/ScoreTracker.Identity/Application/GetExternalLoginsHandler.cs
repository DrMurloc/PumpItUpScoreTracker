using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Identity.Application;

internal sealed class GetExternalLoginsHandler : IRequestHandler<GetExternalLoginsQuery, IEnumerable<ExternalLoginRecord>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public GetExternalLoginsHandler(ICurrentUserAccessor currentUser, IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<IEnumerable<ExternalLoginRecord>> Handle(GetExternalLoginsQuery request,
        CancellationToken cancellationToken)
    {
        return await _users.GetExternalLogins(_currentUser.User.Id, cancellationToken);
    }
}
