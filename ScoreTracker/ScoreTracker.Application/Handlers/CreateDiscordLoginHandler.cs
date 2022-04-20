using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class CreateDiscordLoginHandler : IRequestHandler<CreateDiscordLoginCommand>
{
    private readonly IUserRepository _user;

    public CreateDiscordLoginHandler(IUserRepository user)
    {
        _user = user;
    }

    public async Task<Unit> Handle(CreateDiscordLoginCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await _user.GetUserByDiscordLogin(request.DiscordId, cancellationToken);
        if (existingUser != null)
            throw new Exception($"A discord id {request.DiscordId} already exists for user {existingUser.Id}");

        await _user.CreateDiscordLogin(request.UserId, request.DiscordId, cancellationToken);

        return Unit.Value;
    }
}