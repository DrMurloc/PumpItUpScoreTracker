using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     Player identity on the mirror: the rename-proposal lifecycle (admin accept =
///     history merge; nothing merges automatically) and the account-merge follow-through —
///     when Identity retires an account into a survivor, every mirror player linked to the
///     retired account re-points at the survivor.
/// </summary>
internal sealed class PlayerIdentitySaga :
    IRequestHandler<AcceptRenameProposalCommand>,
    IRequestHandler<DismissRenameProposalCommand>,
    IRequestHandler<GetRenameProposalsQuery, IReadOnlyList<RenameProposalRecord>>,
    IConsumer<AccountsMergedEvent>
{
    private readonly IBus _bus;
    private readonly IOfficialPlayerIdentityRepository _identity;
    private readonly ILogger _logger;

    public PlayerIdentitySaga(IOfficialPlayerIdentityRepository identity, ILogger<PlayerIdentitySaga> logger,
        IBus bus)
    {
        _identity = identity;
        _logger = logger;
        _bus = bus;
    }

    public Task Handle(AcceptRenameProposalCommand request, CancellationToken cancellationToken) =>
        Merge(request.ProposalId,
            request.Unattended ? ProposalStatuses.AutoAccepted : ProposalStatuses.Accepted,
            cancellationToken);

    /// <summary>
    ///     The one merge path, whoever asked for it. An admin's accept and the sweep's
    ///     unattended merge differ by the status they leave behind and nothing else — a second
    ///     implementation would be a second place for the announce-after-the-merge ordering to
    ///     be got wrong.
    /// </summary>
    private async Task Merge(int proposalId, string resolution, CancellationToken cancellationToken)
    {
        var proposal = await _identity.GetProposal(proposalId, cancellationToken);
        if (proposal is not { Status: ProposalStatuses.Pending, NewPlayerId: { } newPlayerId })
        {
            _logger.LogWarning("Rename finding {ProposalId} is not a pending pair; nothing merged", proposalId);
            return;
        }

        var outcome = await _identity.MergePlayers(proposal.OldPlayerId, newPlayerId, cancellationToken);
        if (outcome != MergeOutcome.Merged)
        {
            // Nothing moved. The finding is stale rather than wrong, so it leaves the queue
            // instead of sitting there offering the same impossible merge every week.
            _logger.LogWarning("Refused to merge {Old} into {New} ({Outcome}); finding {ProposalId} dismissed",
                proposal.OldUsername, proposal.NewUsername, outcome, proposal.Id);
            await _identity.SetProposalStatus(proposal.Id, ProposalStatuses.Dismissed, cancellationToken);
            return;
        }

        await _identity.SetProposalStatus(proposal.Id, resolution, cancellationToken);
        // The merge deletes the old dimension row, so anything that stored the old tag is now
        // pointing at nothing. Announced after the merge lands, never before.
        if (proposal is { Mix: { } mix, NewUsername: { } newUsername })
            await _bus.Publish(new OfficialPlayerRenamedEvent(mix, proposal.OldUsername, newUsername),
                cancellationToken);
        _logger.LogInformation("Merged {Old} into {New} ({Resolution}, finding {ProposalId})",
            proposal.OldUsername, proposal.NewUsername, resolution, proposal.Id);
    }

    public async Task Handle(DismissRenameProposalCommand request, CancellationToken cancellationToken)
    {
        var proposal = await _identity.GetProposal(request.ProposalId, cancellationToken);
        if (proposal == null || proposal.Status != ProposalStatuses.Pending) return;

        await _identity.SetProposalStatus(proposal.Id, ProposalStatuses.Dismissed, cancellationToken);
    }

    public async Task<IReadOnlyList<RenameProposalRecord>> Handle(GetRenameProposalsQuery request,
        CancellationToken cancellationToken)
    {
        return (await _identity.GetFindings(request.Mix, request.UnresolvedOnly, cancellationToken))
            .Select(p => new RenameProposalRecord(p.Id, p.OldUsername, p.NewUsername, p.Verdict, p.Status,
                p.Evidence.OldPlacements, p.Evidence.BoardsPresent, p.Evidence.ExactNonPgMatches,
                p.Evidence.ExactPerfectGames, p.Evidence.RunnerUpExactMatches, p.Evidence.SuspiciousAbsences,
                p.Evidence.AvatarMatched))
            .ToArray();
    }

    public async Task Consume(ConsumeContext<AccountsMergedEvent> context)
    {
        await _identity.RelinkUser(context.Message.RetiredUserId, context.Message.SurvivorUserId,
            context.CancellationToken);
    }
}
