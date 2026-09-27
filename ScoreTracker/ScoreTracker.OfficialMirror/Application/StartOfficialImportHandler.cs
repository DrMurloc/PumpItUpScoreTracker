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
    private readonly IImportConcurrencyGuard _guard;
    private readonly IDateTimeOffsetAccessor _dateTime;

    public StartOfficialImportHandler(IOfficialSiteClient officialSite, IMediator mediator, IBus bus,
        ICurrentUserAccessor currentUser, IImportConcurrencyGuard guard, IDateTimeOffsetAccessor dateTime)
    {
        _officialSite = officialSite;
        _mediator = mediator;
        _bus = bus;
        _currentUser = currentUser;
        _guard = guard;
        _dateTime = dateTime;
    }

    public async Task<ImportStartResult> Handle(StartOfficialImportCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        // Before the credential and the sign-in, so a press refused here costs piugame nothing.
        var slot = _guard.TryBegin(userId, request.Mix, _dateTime.Now, cooldownApplies: true);
        if (slot.Outcome == ImportSlotOutcome.AlreadyRunning)
            return new ImportStartResult(ImportStartOutcome.AlreadyRunning);
        if (slot.Outcome == ImportSlotOutcome.CoolingDown)
            return new ImportStartResult(ImportStartOutcome.CoolingDown, slot.RetryAfter);

        // The slot is held until the background job releases it; only the pre-flight failure
        // paths below hand it back, so a second scrape can't start while one is in flight.
        var handedOff = false;
        try
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
                new RunOfficialImportCommand(userId, request.Mix, sid, request.CardId,
                    request.ExpectedGameTag, request.IncludeBroken), cancellationToken);
            handedOff = true;
            _guard.Started(userId, request.Mix, _dateTime.Now);
            return new ImportStartResult(ImportStartOutcome.Started);
        }
        finally
        {
            if (!handedOff) _guard.End(userId);
        }
    }
}
