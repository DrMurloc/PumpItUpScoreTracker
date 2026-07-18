using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
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
    private readonly IOfficialPlayerIdentityRepository _identity;
    private readonly ILogger _logger;

    public PlayerIdentitySaga(IOfficialPlayerIdentityRepository identity, ILogger<PlayerIdentitySaga> logger)
    {
        _identity = identity;
        _logger = logger;
    }

    public async Task Handle(AcceptRenameProposalCommand request, CancellationToken cancellationToken)
    {
        var proposal = await _identity.GetProposal(request.ProposalId, cancellationToken);
        if (proposal == null || proposal.Status != ProposalStatuses.Pending)
        {
            _logger.LogWarning("Rename proposal {ProposalId} is not pending; nothing merged",
                request.ProposalId);
            return;
        }

        await _identity.MergePlayers(proposal.OldPlayerId, proposal.NewPlayerId, cancellationToken);
        await _identity.SetProposalStatus(proposal.Id, ProposalStatuses.Accepted, cancellationToken);
        _logger.LogInformation("Merged {Old} into {New} (proposal {ProposalId})", proposal.OldUsername,
            proposal.NewUsername, proposal.Id);
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
        return (await _identity.GetProposals(request.Mix, ProposalStatuses.Pending, cancellationToken))
            .Select(p => new RenameProposalRecord(p.Id, p.OldUsername, p.NewUsername, p.AvatarMatched,
                p.Top50Overlap))
            .ToArray();
    }

    public async Task Consume(ConsumeContext<AccountsMergedEvent> context)
    {
        await _identity.RelinkUser(context.Message.RetiredUserId, context.Message.SurvivorUserId,
            context.CancellationToken);
    }
}
