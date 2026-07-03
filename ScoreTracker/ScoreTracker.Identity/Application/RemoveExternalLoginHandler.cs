using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;

namespace ScoreTracker.Identity.Application;

internal sealed class RemoveExternalLoginHandler : IRequestHandler<RemoveExternalLoginCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public RemoveExternalLoginHandler(ICurrentUserAccessor currentUser, IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task Handle(RemoveExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var logins = (await _users.GetExternalLogins(userId, cancellationToken)).ToArray();
        var target = logins.FirstOrDefault(l =>
            l.LoginProviderName.Equals(request.LoginProviderName, StringComparison.OrdinalIgnoreCase)
            && l.ExternalId == request.ExternalId);
        if (target == null) return;

        if (logins.Length == 1) throw new CannotRemoveLastExternalLoginException();

        await _users.RemoveExternalLogin(userId, target.LoginProviderName, target.ExternalId, cancellationToken);
    }
}
