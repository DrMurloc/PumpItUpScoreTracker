using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, User>
{
    private readonly IUserRepository _user;

    public CreateUserHandler(IUserRepository user)
    {
        _user = user;
    }

    public async Task<User> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User(Guid.NewGuid(), request.Name, false);
        await _user.SaveUser(user, cancellationToken);
        return user;
    }
}