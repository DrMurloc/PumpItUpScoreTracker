using System.Security.Authentication;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Application;

// Runs the completeness check off the request circuit, mirroring RunOfficialImportConsumer —
// including the per-user slot it must release however the run ends.
internal sealed class RunImportCheckConsumer : IConsumer<RunImportCheckCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IImportConcurrencyGuard _guard;
    private readonly IMediator _mediator;

    public RunImportCheckConsumer(IMediator mediator, ICurrentUserAccessor currentUser,
        IImportConcurrencyGuard guard)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _guard = guard;
    }

    public async Task Consume(ConsumeContext<RunImportCheckCommand> context)
    {
        var message = context.Message;
        try
        {
            // A bus consumer has no HttpContext, so establish the job's user for this scope.
            // SetScopedUser (not SetCurrentUser) so we never issue a cookie — a request context
            // can flow into the consumer, and signing it out would drop the live user's session.
            var user = await _mediator.Send(new GetUserByIdQuery(message.UserId), context.CancellationToken);
            if (user != null) _currentUser.SetScopedUser(user);

            await _mediator.Send(new ExecuteImportCheckCommand(message.UserId, message.Mix, message.Sid,
                message.CardId, message.ExpectedGameTag, message.DeepScan), context.CancellationToken);
        }
        catch (InvalidCredentialException)
        {
            await _mediator.Publish(
                new ImportStatusErrorEvent(message.UserId, "Invalid Login Information", message.Mix),
                context.CancellationToken);
        }
        catch (NoGameAccountAssociatedException)
        {
            await _mediator.Publish(
                new ImportStatusErrorEvent(message.UserId,
                    "No game profile is associated with this account yet.", message.Mix),
                context.CancellationToken);
        }
        finally
        {
            _guard.End(message.UserId);
        }
    }
}
