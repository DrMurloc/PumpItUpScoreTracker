using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

internal class GetUserByDiscordIdHandler : IRequestHandler<GetUserByDiscordLoginQuery, User?>
{
    private readonly IUserRepository _user;

    public GetUserByDiscordIdHandler(IUserRepository user)
    {
        _user = user;
    }

    public async Task<User?> Handle(GetUserByDiscordLoginQuery request, CancellationToken cancellationToken)
    {
        return await _user.GetUserByDiscordLogin(request.DiscordId, cancellationToken);
    }
}