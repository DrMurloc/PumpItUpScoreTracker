using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, User>
{
    private readonly IUserRepository _user;
    private readonly IBus _bus;

    public CreateUserHandler(IUserRepository user, IBus bus)
    {
        _user = user;
        _bus = bus;
    }

    public async Task<User> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User(Guid.NewGuid(), request.Name, false, null,
            new Uri("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png", UriKind.Absolute));
        await _user.SaveUser(user, cancellationToken);
        await _bus.Publish(new UserCreatedEvent(user.Id), cancellationToken);
        return user;
    }
}