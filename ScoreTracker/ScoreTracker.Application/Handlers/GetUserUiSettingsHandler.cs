using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetUserUiSettingsHandler : IRequestHandler<GetUserUiSettingsQuery, IDictionary<string, string>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public GetUserUiSettingsHandler(IUserRepository users, ICurrentUserAccessor currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<IDictionary<string, string>> Handle(GetUserUiSettingsQuery request,
        CancellationToken cancellationToken)
    {
        return await _users.GetUserUiSettings(request.UserId ?? _currentUser.User.Id, cancellationToken);
    }
}