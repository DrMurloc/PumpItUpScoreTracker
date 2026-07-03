using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;

namespace ScoreTracker.Identity.Application;

internal sealed class DisconnectExternalProviderHandler : IRequestHandler<DisconnectExternalProviderCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public DisconnectExternalProviderHandler(ICurrentUserAccessor currentUser, IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task Handle(DisconnectExternalProviderCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var logins = (await _users.GetExternalLogins(userId, cancellationToken)).ToArray();
        var mine = logins.Where(l =>
                l.LoginProviderName.Equals(request.LoginProviderName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (!mine.Any()) return;

        var providerCount = logins.Select(l => l.LoginProviderName.ToLowerInvariant()).Distinct().Count();
        if (providerCount == 1) throw new CannotRemoveLastExternalLoginException();

        foreach (var login in mine)
            await _users.RemoveExternalLogin(userId, login.LoginProviderName, login.ExternalId, cancellationToken);
    }
}
