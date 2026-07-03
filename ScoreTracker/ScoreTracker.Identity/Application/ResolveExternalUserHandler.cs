using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;

namespace ScoreTracker.Identity.Application;

internal sealed class ResolveExternalUserHandler : IRequestHandler<ResolveExternalUserCommand, ExternalUserResolution>
{
    private readonly IMediator _mediator;
    private readonly IUserRepository _users;

    public ResolveExternalUserHandler(IMediator mediator, IUserRepository users)
    {
        _mediator = mediator;
        _users = users;
    }

    public async Task<ExternalUserResolution> Handle(ResolveExternalUserCommand request,
        CancellationToken cancellationToken)
    {
        User? user = null;
        foreach (var externalId in request.ExternalIds)
        {
            user = await _users.GetUserByExternalLogin(request.LoginProviderName, externalId, cancellationToken);
            if (user != null) break;
        }

        var isNew = user == null;
        if (user == null)
        {
            user = await _mediator.Send(new CreateUserCommand(request.DisplayName), cancellationToken);
            user = user with
            {
                GameTag = request.GameTag ?? user.GameTag,
                ProfileImage = request.ProfileImage ?? user.ProfileImage
            };
            await _users.SaveUser(user, cancellationToken);
        }

        foreach (var externalId in request.ExternalIds)
        {
            var owner = await _users.GetUserByExternalLogin(request.LoginProviderName, externalId, cancellationToken);
            if (owner == null)
                await _users.CreateExternalLogin(user.Id, request.LoginProviderName, externalId, cancellationToken);
        }

        return new ExternalUserResolution(user, isNew);
    }
}
