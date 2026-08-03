using MassTransit;
using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Messages;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Identity.Application;

/// <summary>
///     The deep-scan allowance: a balance on the account, spent one at a time and refilled monthly.
///     A balance rather than a usage count keyed to a date, so "give this player three more" is a
///     single UPDATE and the reset is one statement across every row.
/// </summary>
internal sealed class DeepScanAllowanceHandlers :
    IRequestHandler<SpendDeepScanCommand, bool>,
    IRequestHandler<GetDeepScansRemainingQuery, int>,
    IConsumer<ResetDeepScansCommand>
{
    /// <summary>
    ///     Deep scans an account gets each month. Lives here, next to the reset that grants them —
    ///     the Official Mirror only ever asks whether it got one.
    /// </summary>
    public const int MonthlyAllowance = DeepScanAllowance.PerMonth;

    private readonly IUserRepository _users;

    public DeepScanAllowanceHandlers(IUserRepository users)
    {
        _users = users;
    }

    public Task<bool> Handle(SpendDeepScanCommand request, CancellationToken cancellationToken)
    {
        return _users.TrySpendDeepScan(request.UserId, cancellationToken);
    }

    public Task<int> Handle(GetDeepScansRemainingQuery request, CancellationToken cancellationToken)
    {
        return _users.GetDeepScansRemaining(request.UserId, cancellationToken);
    }

    public Task Consume(ConsumeContext<ResetDeepScansCommand> context)
    {
        return _users.ResetDeepScans(MonthlyAllowance, context.CancellationToken);
    }
}
