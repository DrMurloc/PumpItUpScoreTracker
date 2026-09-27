using System.Security.Authentication;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     Starts a completeness check: the Import button's run at a deeper depth. Everything after the
///     sign-in is the one import run (<see cref="RunOfficialImportConsumer" />), which saves whatever it
///     finds on the spot as a normal import — same session, same journal, same rating recalculation —
///     so the scores land on the player's sessions page and their Discord card like any other. Nobody
///     is asked to approve their own score from the official site, which is why the run hands back a
///     count rather than a verdict, and why leaving the page costs nothing.
/// </summary>
internal sealed class StartImportCheckHandler : IRequestHandler<StartImportCheckCommand, ImportCheckStartResult>
{
    private readonly IBus _bus;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IImportConcurrencyGuard _guard;
    private readonly IMediator _mediator;
    private readonly IOfficialSiteClient _officialSite;
    private readonly IDateTimeOffsetAccessor _dateTime;

    public StartImportCheckHandler(IBus bus, ICurrentUserAccessor currentUser, IImportConcurrencyGuard guard,
        IMediator mediator, IOfficialSiteClient officialSite, IDateTimeOffsetAccessor dateTime)
    {
        _bus = bus;
        _currentUser = currentUser;
        _guard = guard;
        _mediator = mediator;
        _officialSite = officialSite;
        _dateTime = dateTime;
    }

    public async Task<ImportCheckStartResult> Handle(StartImportCheckCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var left = await _mediator.Send(new GetDeepScansRemainingQuery(userId), cancellationToken);

        if (request.DeepScan && left == 0)
            return new ImportCheckStartResult(ImportCheckStartOutcome.NoDeepScansLeft, 0);
        // A deep scan never waits out the five minutes — the monthly allowance is its limit — but a
        // check does, like the Import button.
        var slot = _guard.TryBegin(userId, request.Mix, _dateTime.Now, cooldownApplies: !request.DeepScan);
        if (slot.Outcome == ImportSlotOutcome.AlreadyRunning)
            return new ImportCheckStartResult(ImportCheckStartOutcome.AlreadyRunning, left);
        if (slot.Outcome == ImportSlotOutcome.CoolingDown)
            return new ImportCheckStartResult(ImportCheckStartOutcome.CoolingDown, left, slot.RetryAfter);

        // The slot is held until the background job releases it; only the pre-flight failures
        // below hand it back.
        var handedOff = false;
        try
        {
            var credentials = await Resolve(request.Source, cancellationToken);
            if (credentials == null)
                return new ImportCheckStartResult(ImportCheckStartOutcome.CredentialUnlockFailed, left);

            string sid;
            try
            {
                sid = await _officialSite.SignIn(request.Mix, credentials.Value.Username,
                    credentials.Value.Password, cancellationToken);
            }
            catch (InvalidCredentialException)
            {
                return new ImportCheckStartResult(ImportCheckStartOutcome.InvalidCredentials, left);
            }

            // Spent only once the run is certain to start, so a mistyped password costs nothing.
            // The decrement is atomic in the database, which is what stops a second tab spending
            // the same last scan.
            if (request.DeepScan)
            {
                if (!await _mediator.Send(new SpendDeepScanCommand(userId), cancellationToken))
                    return new ImportCheckStartResult(ImportCheckStartOutcome.NoDeepScansLeft, 0);
                left--;
            }

            await _bus.Publish(new RunOfficialImportCommand(userId, request.Mix, sid, request.CardId,
                    request.ExpectedGameTag, request.IncludeBroken,
                    request.DeepScan ? ImportKind.DeepScan : ImportKind.Check),
                cancellationToken);
            handedOff = true;
            return new ImportCheckStartResult(ImportCheckStartOutcome.Started, left);
        }
        finally
        {
            if (!handedOff) _guard.End(userId);
        }
    }

    private async Task<(string Username, string Password)?> Resolve(ImportCredentialSource source,
        CancellationToken cancellationToken)
    {
        switch (source)
        {
            case TypedCredentialSource typed:
                return (typed.Username, typed.Password);
            case StoredCredentialSource stored:
                var revealed = await _mediator.Send(
                    new RevealImportCredentialQuery(stored.KeyId, stored.Ciphertext), cancellationToken);
                return revealed == null ? null : (revealed.Username, revealed.Password);
            default:
                return null;
        }
    }
}
