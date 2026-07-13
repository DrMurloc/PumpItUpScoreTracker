using System.Security.Authentication;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Application;

internal sealed class StartOfficialImportHandler : IRequestHandler<StartOfficialImportCommand, ImportStartResult>
{
    private readonly IOfficialSiteClient _officialSite;
    private readonly IMediator _mediator;
    private readonly IBus _bus;
    private readonly ICurrentUserAccessor _currentUser;

    public StartOfficialImportHandler(IOfficialSiteClient officialSite, IMediator mediator, IBus bus,
        ICurrentUserAccessor currentUser)
    {
        _officialSite = officialSite;
        _mediator = mediator;
        _bus = bus;
        _currentUser = currentUser;
    }

    public async Task<ImportStartResult> Handle(StartOfficialImportCommand request,
        CancellationToken cancellationToken)
    {
        string username;
        string password;
        switch (request.Source)
        {
            case TypedCredentialSource typed:
                username = typed.Username;
                password = typed.Password;
                break;
            case StoredCredentialSource stored:
                var revealed =
                    await _mediator.Send(new RevealImportCredentialQuery(stored.KeyId, stored.Ciphertext),
                        cancellationToken);
                if (revealed == null)
                    return new ImportStartResult(ImportStartOutcome.CredentialUnlockFailed);
                username = revealed.Username;
                password = revealed.Password;
                break;
            default:
                return new ImportStartResult(ImportStartOutcome.CredentialUnlockFailed);
        }

        string sid;
        try
        {
            sid = await _officialSite.SignIn(request.Mix, username, password, cancellationToken);
        }
        catch (InvalidCredentialException)
        {
            return new ImportStartResult(ImportStartOutcome.InvalidCredentials);
        }

        await _bus.Publish(
            new RunOfficialImportCommand(_currentUser.User.Id, request.Mix, sid, request.CardId,
                request.ExpectedGameTag, request.IncludeBroken, request.SyncPiuTracker), cancellationToken);
        return new ImportStartResult(ImportStartOutcome.Started);
    }
}
