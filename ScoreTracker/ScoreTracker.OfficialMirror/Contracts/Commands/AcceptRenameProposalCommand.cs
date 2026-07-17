using MediatR;

namespace ScoreTracker.OfficialMirror.Contracts.Commands;

/// <summary>
///     Admin acceptance of a detected rename: the old tag's board history re-points onto
///     the new tag's player and the old dimension row is deleted. The proposal row keeps
///     both usernames as the audit trail.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AcceptRenameProposalCommand(int ProposalId) : IRequest;
