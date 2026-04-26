using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class SetUserContentLockHandler : IRequestHandler<SetUserContentLockCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IBus _bus;

    public SetUserContentLockHandler(IUserRepository users, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor clock, IBus bus)
    {
        _users = users;
        _currentUser = currentUser;
        _clock = clock;
        _bus = bus;
    }

    public async Task Handle(SetUserContentLockCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.User.IsAdmin)
            throw new NotAuthorizedException("manage content locks");

        var existing = await _users.GetUser(request.UserId, cancellationToken)
                       ?? throw new UserNotFoundException(request.UserId);

        var newName = request.IsLocked
            ? request.OverrideName ?? existing.GameTag
              ?? throw new InvalidNameException(
                  "A name must be provided when locking a user without a GameTag.")
            : existing.Name;

        var locked = new User(existing.Id, newName, existing.IsPublic, existing.GameTag, existing.ProfileImage,
            existing.Country, request.IsLocked, _clock.Now);

        await _users.SaveUser(locked, cancellationToken);
        await _bus.Publish(new UserUpdatedEvent(existing.Id, existing.Country, existing.IsPublic), cancellationToken);
    }
}
