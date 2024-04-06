using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class SaveUserUiSettingHandler : IRequestHandler<SaveUserUiSettingCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public SaveUserUiSettingHandler(IUserRepository users, ICurrentUserAccessor currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task Handle(SaveUserUiSettingCommand request, CancellationToken cancellationToken)
    {
        var settings = await _users.GetUserUiSettings(_currentUser.User.Id, cancellationToken);
        settings[request.SettingName] = request.NewValue;
        await _users.SaveUserUiSettings(_currentUser.User.Id, settings, cancellationToken);
        
    }
}