using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class UpdateUserHandler : IRequestHandler<UpdateUserCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public UpdateUserHandler(IUserRepository users,
        ICurrentUserAccessor currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = _currentUser.User;
        var existing = await _users.GetUser(user.Id, cancellationToken);

        var newUser = new User(user.Id, request.newName, request.newIsPublic, existing?.GameTag,
            existing?.ProfileImage ??
            new Uri("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png", UriKind.Absolute));
        await _users.SaveUser(newUser, cancellationToken);
        return Unit.Value;
    }
}