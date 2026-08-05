using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <param name="UnresolvedOnly">
///     False returns the whole population of vanished tags, the self-merged ones included.
///     The desk wants that: a rule that has quietly stopped detecting renames looks identical
///     to a quiet week if all you ever see is what it could not decide.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record GetRenameProposalsQuery(MixEnum Mix, bool UnresolvedOnly = false)
    : IQuery<IReadOnlyList<RenameProposalRecord>>;
