using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;

namespace ScoreTracker.Identity.Application;

internal sealed class LinkExternalAliasesHandler : IRequestHandler<LinkExternalAliasesCommand, ExternalLinkResult>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public LinkExternalAliasesHandler(ICurrentUserAccessor currentUser, IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<ExternalLinkResult> Handle(LinkExternalAliasesCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var unclaimed = new List<string>();
        foreach (var externalId in request.ExternalIds)
        {
            var owner = await _users.GetUserByExternalLogin(request.LoginProviderName, externalId,
                cancellationToken);
            if (owner == null) unclaimed.Add(externalId);
            else if (owner.Id != userId) return ExternalLinkResult.ConflictingAccount;
        }

        if (!unclaimed.Any()) return ExternalLinkResult.AlreadyLinked;

        foreach (var externalId in unclaimed)
            await _users.CreateExternalLogin(userId, request.LoginProviderName, externalId, cancellationToken);

        return ExternalLinkResult.Linked;
    }
}
