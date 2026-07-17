using MediatR;

namespace ScoreTracker.OfficialMirror.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record DismissRenameProposalCommand(int ProposalId) : IRequest;
