using MediatR;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Identity.Application;

internal sealed class ClearUserUiSettingHandler : IRequestHandler<ClearUserUiSettingCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public ClearUserUiSettingHandler(IUserRepository users, ICurrentUserAccessor currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task Handle(ClearUserUiSettingCommand request, CancellationToken cancellationToken)
    {
        var settings = await _users.GetUserUiSettings(_currentUser.User.Id, cancellationToken);

        // Nothing to remove is a success, not a write: the blob is one row per player and
        // rewriting it unchanged would evict every reader's cache for no reason.
        if (!settings.Remove(request.SettingName)) return;

        await _users.SaveUserUiSettings(_currentUser.User.Id, settings, cancellationToken);
    }
}
