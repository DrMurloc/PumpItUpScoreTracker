using MassTransit;
using MediatR;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Identity.Application;

internal sealed class CreateExternalLoginHandler : IRequestHandler<CreateExternalLoginCommand>
{
    private readonly IBus _bus;
    private readonly IUserRepository _user;

    public CreateExternalLoginHandler(IUserRepository user, IBus bus)
    {
        _user = user;
        _bus = bus;
    }

    public async Task Handle(CreateExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var existingUser =
            await _user.GetUserByExternalLogin(request.LoginProviderName, request.ExternalId, cancellationToken);
        if (existingUser != null)
            throw new Exception(
                $"An external id {request.ExternalId} in provider {request.LoginProviderName} already exists for user {existingUser.Id}");

        await _user.CreateExternalLogin(request.UserId, request.LoginProviderName, request.ExternalId,
            cancellationToken);

        // The mirror of the unlink event. Linking is the last step of ordinary onboarding — the
        // community join and the server join both happen before there is an account to find — so
        // without this the player waits for a nightly sweep they cannot trigger themselves.
        await _bus.Publish(new ExternalLoginAddedEvent(request.UserId, request.LoginProviderName),
            cancellationToken);
    }
}