using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetRenameProposalsQuery(MixEnum Mix) : IQuery<IReadOnlyList<RenameProposalRecord>>;
