using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;

namespace ScoreTracker.Identity.Application;

internal sealed class UpdateUserGameProfileHandler : IRequestHandler<UpdateUserGameProfileCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public UpdateUserGameProfileHandler(ICurrentUserAccessor currentUser, IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task Handle(UpdateUserGameProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetUser(_currentUser.User.Id, cancellationToken)
                   ?? throw new UserNotFoundException(_currentUser.User.Id);
        await _users.SaveUser(
            user with { GameTag = request.GameTag, ProfileImage = request.AvatarUrl },
            cancellationToken);
    }
}
