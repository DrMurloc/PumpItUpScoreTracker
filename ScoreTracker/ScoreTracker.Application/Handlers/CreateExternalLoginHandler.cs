using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class CreateExternalLoginHandler : IRequestHandler<CreateExternalLoginCommand>
{
    private readonly IUserRepository _user;

    public CreateExternalLoginHandler(IUserRepository user)
    {
        _user = user;
    }

    public async Task<Unit> Handle(CreateExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var existingUser =
            await _user.GetUserByExternalLogin(request.LoginProviderName, request.ExternalId, cancellationToken);
        if (existingUser != null)
            throw new Exception(
                $"An external id {request.ExternalId} in provider {request.LoginProviderName} already exists for user {existingUser.Id}");

        await _user.CreateExternalLogin(request.UserId, request.LoginProviderName, request.ExternalId,
            cancellationToken);

        return Unit.Value;
    }
}