using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;

namespace ScoreTracker.Identity.Application;

internal sealed class LinkExternalAliasesHandler : IRequestHandler<LinkExternalAliasesCommand, ExternalLinkOutcome>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public LinkExternalAliasesHandler(ICurrentUserAccessor currentUser, IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<ExternalLinkOutcome> Handle(LinkExternalAliasesCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var unclaimed = new List<string>();
        foreach (var externalId in request.ExternalIds)
        {
            var owner = await _users.GetUserByExternalLogin(request.LoginProviderName, externalId,
                cancellationToken);
            if (owner == null) unclaimed.Add(externalId);
            else if (owner.Id != userId)
                return new ExternalLinkOutcome(ExternalLinkResult.ConflictingAccount, owner.Id);
        }

        if (!unclaimed.Any()) return new ExternalLinkOutcome(ExternalLinkResult.AlreadyLinked, null);

        foreach (var externalId in unclaimed)
            await _users.CreateExternalLogin(userId, request.LoginProviderName, externalId, cancellationToken);

        return new ExternalLinkOutcome(ExternalLinkResult.Linked, null);
    }
}
